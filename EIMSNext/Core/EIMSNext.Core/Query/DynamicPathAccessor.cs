using System.Collections;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using EIMSNext.Common;
using EIMSNext.Core.Abstractions;

namespace EIMSNext.Core.Query
{
    /// <summary>
    /// 动态路径访问器。把 <c>"data.name"</c> / <c>"items.0.label"</c> 这类字符串路径
    /// 编译成 <c>x =&gt; x.Data["name"]</c> 形式的表达式树。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 这是 PostgreSQL 迁移中替代 Mongo 字符串字段过滤的关键件。Mongo 可以直接用字符串字段名
    /// 构造 filter，EF Core 只能接受表达式树，因此这里统一做一次「字符串 → 表达式」的翻译，
    /// 让 <see cref="DynamicFilter"/> 的使用方（25 个文件）无需改动调用形态。
    /// </para>
    /// <para>
    /// 路径语义约定（与 Mongo 时期的字段命名保持一致）：
    /// <list type="bullet">
    /// <item><description>普通属性按 PascalCase 解析（<c>code</c> → <c>Code</c>），大小写不敏感命中。</description></item>
    /// <item><description><c>data.xxx</c> 解析为一次对 <c>"eims_json_text"(Data, '$."xxx"')</c> 的调用，
    /// 即 jsonb 内部的键；静态类型按 <see cref="string"/> 处理（见 <see cref="PgJsonFunctions"/>）。
    /// 排序走 <see cref="TryBuildJsonbSortKey{T}"/>，改用返回 jsonb 的 <c>eims_json_sort</c>。</description></item>
    /// <item><description><c>.label</c> / <c>.value</c> / <c>.id</c> / <c>.code</c> 这类选项字段后缀，
    /// 会作为路径的后续段（<c>$."field"."label"</c>）。</description></item>
    /// <item><description>数字段在顶层属性数组上是 C# 数组下标；进入 jsonb 后则作为 JSONPath 数组下标
    /// （<c>$."items"[0]."name"</c>）处理。</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public static class DynamicPathAccessor
    {
        private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase;

        /// <summary>
        /// 构建返回 <see cref="object"/> 的路径访问表达式。
        /// </summary>
        /// <typeparam name="T">实体类型。</typeparam>
        /// <param name="field">字段路径。</param>
        /// <returns>访问表达式，形如 <c>x =&gt; (object)x.Data["name"]</c>。</returns>
        public static Expression<Func<T, object>> Build<T>(string field)
        {
            // 表达式体与 lambda 必须共用同一个参数实例：BuildBody 内部会新建参数，
            // 若这里再调一次 Parameter<T>()，lambda 声明的就是另一个自由变量，
            // EF Core 翻译时会直接报 “The LINQ expression 'x' could not be translated”。
            var parameter = Parameter<T>();
            var (body, _) = BuildBody<T>(field, parameter);
            return Expression.Lambda<Func<T, object>>(Expression.Convert(body, typeof(object)), parameter);
        }

        /// <summary>
        /// 构建指定返回类型的路径访问表达式。
        /// </summary>
        /// <typeparam name="T">实体类型。</typeparam>
        /// <typeparam name="TValue">返回值类型。</typeparam>
        /// <param name="field">字段路径。</param>
        /// <returns>访问表达式。</returns>
        public static Expression<Func<T, TValue>> Build<T, TValue>(string field)
        {
            var parameter = Parameter<T>();
            var (body, _) = BuildBody<T>(field, parameter);
            return Expression.Lambda<Func<T, TValue>>(ConvertTo(body, typeof(TValue)), parameter);
        }

        /// <summary>
        /// 构建路径访问的表达式体（不包裹 lambda），同时回传静态类型信息。
        /// </summary>
        /// <typeparam name="T">实体类型。</typeparam>
        /// <param name="field">字段路径。</param>
        /// <param name="parameter">
        /// 实体 lambda 参数。调用方若已在自己的表达式树里用同一个参数，必须传进来——
        /// EF Core 只认与查询根绑定的参数，参数实例不一致会被判为「无法翻译」。
        /// 为 <c>null</c> 时新建一个。
        /// </param>
        /// <returns>表达式体与静态类型；静态类型未知（jsonb 内部）时为 <see cref="object"/>。</returns>
        public static (Expression Body, Type StaticType) BuildBody<T>(string field, ParameterExpression? parameter = null)
        {
            var (body, staticType, _) = Resolve<T>(field, parameter);
            return (body, staticType);
        }

        /// <summary>
        /// 判断路径是否落在 jsonb 容器内部，并回传容器表达式与 JSONPath。
        /// </summary>
        /// <typeparam name="T">实体类型。</typeparam>
        /// <param name="field">字段路径。</param>
        /// <param name="parameter">实体 lambda 参数，语义同 <see cref="BuildBody{T}"/>。</param>
        /// <param name="resolution">解析结果；返回 false 时无意义。</param>
        /// <returns>路径进入 jsonb 容器时为 true。</returns>
        /// <remarks>
        /// 过滤场景需要这个信息：jsonb 内部键的比较要换成带谓词的 JSONPath
        /// （<c>jsonb_path_exists</c>）才能表达 Mongo 的「数组任意元素满足」语义，
        /// 而不是退化成一个标量取值的比较。
        /// </remarks>
        public static bool TryResolveJsonbPath<T>(
            string field,
            ParameterExpression? parameter,
            out JsonbPathResolution resolution)
        {
            var (_, _, jsonb) = Resolve<T>(field, parameter);
            resolution = jsonb ?? default;
            return jsonb is not null;
        }

        /// <summary>
        /// 构建 jsonb 内部路径的排序键表达式（取值时保留 jsonb 类型）。
        /// </summary>
        /// <typeparam name="T">实体类型。</typeparam>
        /// <param name="field">字段路径。</param>
        /// <param name="parameter">实体 lambda 参数，语义同 <see cref="BuildBody{T}"/>。</param>
        /// <param name="key">排序键表达式；返回 false 时无意义。</param>
        /// <returns>路径落在 jsonb 容器内部时为 true。</returns>
        /// <remarks>
        /// <para>
        /// 排序不能复用 <see cref="BuildBody{T}"/>：那条路对 jsonb 路径给的是
        /// <c>eims_json_text(...)</c>（text），排序会退化成字典序，<c>"10"</c> 排在 <c>"9"</c> 前面。
        /// 这里改用 <c>eims_json_sort(...)</c>（返回 jsonb），由数据库按 jsonb 的 btree 规则比较：
        /// 数字按数值、字符串按排序规则，字段缺失则落在最小值位置，与 Mongo 一致。
        /// </para>
        /// <para>
        /// 非 jsonb 路径返回 false，调用方回退到 <see cref="BuildBody{T}"/> 的强类型表达式
        /// （普通属性排序本来就是数据库列比较，无需干预）。
        /// </para>
        /// </remarks>
        public static bool TryBuildJsonbSortKey<T>(
            string field,
            ParameterExpression? parameter,
            out Expression key)
        {
            var (_, _, jsonb) = Resolve<T>(field, parameter);
            if (jsonb is null)
            {
                key = Expression.Constant(false, typeof(bool));
                return false;
            }

            key = BuildJsonPathSort(jsonb.Value.Container, jsonb.Value.Path);
            return true;
        }

        /// <summary>
        /// 把路径解析为表达式，并在进入 jsonb 容器时同时回传容器与 JSONPath。
        /// </summary>
        /// <typeparam name="T">实体类型。</typeparam>
        /// <param name="field">字段路径。</param>
        /// <param name="parameter">实体 lambda 参数。</param>
        /// <returns>表达式体、静态类型，以及 jsonb 解析结果（非 jsonb 路径时为 null）。</returns>
        private static (Expression Body, Type StaticType, JsonbPathResolution? Jsonb) Resolve<T>(
            string field,
            ParameterExpression? parameter)
        {
            var segments = SplitPath(field);
            if (segments.Count == 0)
            {
                throw new BadRequestException("过滤字段不能为空");
            }

            Expression current = parameter ?? Parameter<T>();
            var currentType = typeof(T);

            for (var i = 0; i < segments.Count; i++)
            {
                var segment = segments[i];

                // 数字段 + 数组属性 → 数组下标
                if (currentType.IsArray && int.TryParse(segment, out var index))
                {
                    current = Expression.ArrayIndex(current, Expression.Constant(index));
                    currentType = currentType.GetElementType()!;
                    continue;
                }

                var property = FindProperty(currentType, segment);
                if (property is not null)
                {
                    current = Expression.Property(current, property);
                    currentType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
                    continue;
                }

                // 进入 jsonb / 动态容器：剩余路径整体交给 SQL 函数按 JSONPath 取值。
                // 静态类型固定为 string——jsonb 内部值一律按文本取出。
                if (IsDynamicContainer(currentType) || TryGetEnumerableElementType(currentType, out _))
                {
                    var jsonPath = BuildJsonPath(segments.Skip(i));
                    var resolution = new JsonbPathResolution(current, jsonPath);
                    return (BuildJsonPathText(current, jsonPath), typeof(string), resolution);
                }

                // 属性不存在：Mongo 时期该条件等价于「字段缺失」，会匹配不到任何文档。
                // 这里同样保持「永不匹配」的语义，而不是抛异常泄露内部结构。
                return (Expression.Constant(false, typeof(bool)), typeof(bool), null);
            }

            return (current, currentType, null);
        }

        /// <summary>
        /// 把路径段拼成 JSONPath。
        /// </summary>
        /// <param name="segments">路径段。</param>
        /// <returns>形如 <c>$."f_1"."label"</c> / <c>$."items"[0]."name"</c> 的 JSONPath。</returns>
        /// <remarks>
        /// <para>
        /// 键名一律加引号，避免字段名里的连字符、空格之类破坏路径语法；
        /// 纯数字段按下标处理（与 Mongo 的 <c>a.0.b</c> 一致）。
        /// </para>
        /// <para>
        /// 不加 <c>strict</c> 前缀是刻意的：lax 模式下 JSONPath 会自动展开数组，
        /// 于是 <c>data.items.name</c> 能同时命中数组里每个元素的 <c>name</c>，
        /// 与 Mongo 的行为一致。详见 007 脚本头部的说明。
        /// </para>
        /// </remarks>
        public static string BuildJsonPath(IEnumerable<string> segments)
        {
            var builder = new StringBuilder("$");
            foreach (var segment in segments)
            {
                if (int.TryParse(segment, NumberStyles.None, CultureInfo.InvariantCulture, out var index))
                {
                    builder.Append('[').Append(index).Append(']');
                    continue;
                }

                builder.Append(".\"")
                    .Append(segment.Replace("\\", "\\\\").Replace("\"", "\\\""))
                    .Append('"');
            }

            return builder.ToString();
        }

        /// <summary>
        /// 把 jsonb 内部路径编译成一次 <see cref="PgJsonFunctions.JsonPath"/> 调用。
        /// </summary>
        /// <param name="container">jsonb 列（或已取出的 jsonb 值）表达式。</param>
        /// <param name="jsonPath">JSONPath。</param>
        /// <returns>取值调用的表达式，CLR 类型为 <see cref="string"/>。</returns>
        /// <remarks>
        /// 为什么不是 <c>MakeIndex</c> 链：<c>Data</c> 是挂了值转换器的 jsonb 标量属性，
        /// EF Core 看不到 <c>IDictionary&lt;string, object&gt;</c> 索引器的 SQL 对应物，
        /// 表达式树里出现索引器就会整体报「could not be translated」。
        /// 改走 <see cref="PgJsonFunctions.JsonPath"/>（映射到 <c>"eims_json_text"</c>）之后，
        /// 表达式树里只有一次普通函数调用，EF Core 能翻译成
        /// <c>eims_json_text("Data", '$."a"."b"')</c>，
        /// Where / OrderBy / Count / ExecuteUpdate / ExecuteDelete 全部可用。
        /// </remarks>
        private static Expression BuildJsonPathText(Expression container, string jsonPath)
            => Expression.Call(
                typeof(PgJsonFunctions).GetMethod(nameof(PgJsonFunctions.JsonPath))!,
                container,
                Expression.Constant(jsonPath, typeof(string)));

        /// <summary>
        /// 把 jsonb 内部路径编译成一次 <see cref="PgJsonFunctions.JsonSort"/> 调用（保留 jsonb 类型）。
        /// </summary>
        /// <param name="container">jsonb 列（或已取出的 jsonb 值）表达式。</param>
        /// <param name="jsonPath">JSONPath。</param>
        /// <returns>排序键表达式，CLR 类型为 <see cref="string"/> 但数据库侧是 jsonb。</returns>
        /// <remarks>
        /// CLR 类型为 string 只是为了给表达式树一个载体：该调用只出现在 ORDER BY 中，
        /// 不会被投影回客户端，因此数据库返回的 jsonb 不会被当作文本反序列化。
        /// </remarks>
        private static Expression BuildJsonPathSort(Expression container, string jsonPath)
            => Expression.Call(
                typeof(PgJsonFunctions).GetMethod(nameof(PgJsonFunctions.JsonSort))!,
                container,
                Expression.Constant(jsonPath, typeof(string)));

        /// <summary>
        /// 路径进入 jsonb 容器后的解析结果。
        /// </summary>
        /// <param name="Container">承载该路径的 jsonb 表达式（通常是某个属性访问）。</param>
        /// <param name="Path">从该容器出发的 JSONPath。</param>
        public readonly record struct JsonbPathResolution(Expression Container, string Path);

        /// <summary>
        /// 判断类型是否为可承载动态键的容器（<see cref="ExpandoObject"/> / <c>IDictionary&lt;string, object&gt;</c>）。
        /// </summary>
        /// <param name="type">类型。</param>
        /// <returns>是动态容器时为 true。</returns>
        public static bool IsDynamicContainer(Type type)
        {
            var underlying = Nullable.GetUnderlyingType(type) ?? type;
            if (underlying == typeof(object) || typeof(IDictionary<string, object>).IsAssignableFrom(underlying))
            {
                return true;
            }

            return underlying.GetInterfaces().Any(x =>
                x.IsGenericType
                && x.GetGenericTypeDefinition() == typeof(IDictionary<,>)
                && x.GetGenericArguments()[0] == typeof(string));
        }

        /// <summary>
        /// 获取集合的元素类型。
        /// </summary>
        /// <param name="type">类型。</param>
        /// <param name="elementType">元素类型。</param>
        /// <returns>是集合时为 true。</returns>
        public static bool TryGetEnumerableElementType(Type type, out Type elementType)
        {
            elementType = typeof(object);
            var underlying = Nullable.GetUnderlyingType(type) ?? type;
            if (underlying == typeof(string)) return false;

            if (underlying.IsArray)
            {
                elementType = underlying.GetElementType()!;
                return true;
            }

            var enumerable = underlying.IsGenericType && underlying.GetGenericTypeDefinition() == typeof(IEnumerable<>)
                ? underlying
                : underlying.GetInterfaces().FirstOrDefault(x =>
                    x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IEnumerable<>));

            if (enumerable is null) return false;

            elementType = enumerable.GetGenericArguments()[0];
            return true;
        }

        /// <summary>
        /// 按路径拆分段，支持点号与 Mongo 的 <c>&gt;</c>（元素匹配）两种分隔符。
        /// </summary>
        /// <param name="field">字段路径。</param>
        /// <returns>路径段集合。</returns>
        public static List<string> SplitPath(string field)
        {
            return field
                .Split(['.', '>'], StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)
                .ToList();
        }

        /// <summary>
        /// 查找属性，先精确再忽略大小写。
        /// </summary>
        /// <param name="type">类型。</param>
        /// <param name="name">属性名。</param>
        /// <returns>属性信息；未找到时为 null。</returns>
        /// <remarks>
        /// <para>
        /// <c>_id</c> 是 Mongo 时期的主键字段名（<see cref="Fields.BsonId"/>），调用方
        /// （控制器与 ApiService）仍在大量使用该常量构造 <see cref="DynamicFilter"/>。
        /// PostgreSQL 下主键列叫 <c>Id</c>，属性名也是 <c>Id</c>，因此这里把 <c>_id</c>
        /// 统一别名到 <c>Id</c>，避免去改动 20+ 个调用点。
        /// </para>
        /// <para>
        /// 该别名只用于顶层实体键。jsonb 内部（<c>Data["_id"]</c>）不走这里，
        /// 仍保持原样，因为历史表单数据里确实可能存了名为 <c>_id</c> 的业务字段。
        /// </para>
        /// </remarks>
        public static PropertyInfo? FindProperty(Type type, string name)
        {
            if (string.Equals(name, Fields.BsonId, StringComparison.Ordinal))
            {
                return type.GetProperty(Fields.Id, InstanceFlags);
            }

            return type.GetProperty(name, InstanceFlags)
                ?? type.GetProperties(InstanceFlags)
                    .FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 把表达式转换为目标类型。
        /// </summary>
        /// <param name="expression">源表达式。</param>
        /// <param name="target">目标类型。</param>
        /// <returns>转换后的表达式。</returns>
        public static Expression ConvertTo(Expression expression, Type target)
        {
            if (expression.Type == target) return expression;

            var underlyingTarget = Nullable.GetUnderlyingType(target) ?? target;
            var underlyingSource = Nullable.GetUnderlyingType(expression.Type) ?? expression.Type;

            // string → 值类型：Mongo 时期过滤值可能是字符串，这里显式转换，
            // 让 EF Core 生成带类型转换的 SQL 而不是报错。
            if (underlyingSource == typeof(string) && underlyingTarget != typeof(string))
            {
                var converted = TryConvertViaString(expression, underlyingTarget);
                if (converted is not null) return converted;
            }

            if (target.IsAssignableFrom(expression.Type)) return expression;

            if (underlyingTarget.IsEnum && underlyingSource != typeof(string))
            {
                return Expression.Convert(expression, target);
            }

            if (expression.Type.IsValueType && !target.IsValueType)
            {
                return Expression.Convert(expression, target);
            }

            if (!expression.Type.IsValueType && target.IsValueType)
            {
                return Expression.Convert(expression, target);
            }

            return Expression.Convert(expression, target);
        }

        /// <summary>
        /// 获取实体的 lambda 参数。
        /// </summary>
        /// <typeparam name="T">实体类型。</typeparam>
        /// <returns>参数表达式。</returns>
        public static ParameterExpression Parameter<T>() => Expression.Parameter(typeof(T), "x");

        /// <summary>
        /// 尝试通过 <c>object.ToString()</c> 再解析的方式转换（字符串过滤值 → 目标类型）。
        /// 无法在表达式树中表达时返回 null，由调用方走显式 <see cref="Expression.Convert(Expression, Type)"/>。
        /// </summary>
        /// <param name="expression">源表达式。</param>
        /// <param name="target">目标类型。</param>
        /// <returns>转换表达式或 null。</returns>
        private static Expression? TryConvertViaString(Expression expression, Type target)
        {
            var parseMethod = target.GetMethod("Parse", InstanceFlags, [typeof(string)]);
            if (parseMethod is not null && parseMethod.IsStatic)
            {
                return Expression.Call(parseMethod, expression);
            }

            if (target == typeof(Guid))
            {
                var guidParse = typeof(Guid).GetMethod(nameof(Guid.Parse), [typeof(string)])!;
                return Expression.Call(guidParse, expression);
            }

            return null;
        }
    }
}
