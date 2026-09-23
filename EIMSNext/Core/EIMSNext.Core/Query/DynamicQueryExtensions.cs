using System.Collections;
using System.Globalization;
using System.Linq.Expressions;
using System.Text.Json;
using System.Text.RegularExpressions;
using EIMSNext.Common;
using EIMSNext.Core.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace EIMSNext.Core.Query
{
    /// <summary>
    /// 动态筛选 / 排序 / 投影到 EF Core 表达式的转换扩展。
    /// </summary>
    /// <remarks>
    /// </remarks>
    public static class DynamicQueryExtensions
    {
        /// <summary>
        /// 将动态筛选条件转换为 EF Core 过滤表达式。
        /// </summary>
        /// <returns>过滤表达式；条件为空时返回 <c>x =&gt; true</c>。</returns>
        public static Expression<Func<T, bool>> ToPredicate<T>(this DynamicFilter? filter)
        {
            filter = DynamicFilterRules.Normalize(filter);
            if (filter is null)
            {
                return True<T>();
            }

            DynamicFilterValidator.Validate(filter);

            var parameter = DynamicPathAccessor.Parameter<T>();
            var body = BuildPredicateBody<T>(filter, parameter);
            return Expression.Lambda<Func<T, bool>>(body, parameter);
        }

        /// <summary>
        /// 将动态查询选项转换为 EF Core 仓储可消费的查询选项。
        /// </summary>
        /// <returns>等价的 <see cref="QueryFindOptions{T}"/>。</returns>
        /// <remarks>
        /// 过滤由 <see cref="ToPredicate{T}"/> 翻译，排序由 <see cref="ToSortDefinition{T}"/> 翻译，
        /// 分页沿用 <see cref="DynamicFindOptions{T}.GetEffectiveSkip"/> /
        /// <see cref="DynamicFindOptions{T}.GetEffectiveTake"/> 的归一化规则（Take &lt;= 0 视为默认 200）。
        /// </remarks>
        public static QueryFindOptions<T> ToQueryFindOptions<T>(this DynamicFindOptions<T>? options)
        {
            if (options is null) return new QueryFindOptions<T>();

            return new QueryFindOptions<T>(options.Filter.ToPredicate<T>())
            {
                Sort = options.Sort.ToSortDefinition<T>(),
                Skip = options.GetEffectiveSkip(),
                Take = options.GetEffectiveTake(),
            };
        }

        /// <summary>
        /// 将动态排序字段列表转换为排序描述。
        /// </summary>
        /// <returns>排序定义；列表为空时返回 null。</returns>
        public static DynamicSortDefinition? ToSortDefinition<T>(this DynamicSortList? sortList)
        {
            if (sortList is null || sortList.Count == 0) return null;

            var definition = new DynamicSortDefinition();
            foreach (var sort in sortList)
            {
                if (string.IsNullOrEmpty(sort.Field)) continue;
                var field = FormatSortField(sort.Field, sort.Type);
                definition.Then(sort.Dir == SortDir.Desc
                    ? DynamicOrderBy.Desc(field)
                    : DynamicOrderBy.Asc(field));
            }

            return definition.IsEmpty ? null : definition;
        }

        /// <summary>
        /// 按动态排序定义对查询排序。
        /// </summary>
        public static IQueryable<T> OrderBy<T>(this IQueryable<T> source, DynamicSortDefinition? sort, ParameterExpression? parameter = null)
        {
            if (sort is null || sort.IsEmpty) return source;

            var lambdaParameter = parameter ?? Expression.Parameter(typeof(T), "x");
            var isFirst = true;

            foreach (var item in sort.Items)
            {
                // 排序 lambda 也必须与表达式体共用同一个参数实例，理由同 ToPredicate。
                // jsonb 内部键（data.xxx）走返回 jsonb 的取值函数：用 eims_json_text 的话
                Expression body;
                if (DynamicPathAccessor.TryBuildJsonbSortKey<T>(item.Field, lambdaParameter, out var jsonbKey))
                {
                    body = jsonbKey;
                }
                else
                {
                    body = DynamicPathAccessor.BuildBody<T>(item.Field, lambdaParameter).Body;
                }

                // 字段不存在时 BuildBody 返回常量 false，此时跳过该排序项，
                if (body is ConstantExpression { Value: false }) continue;

                var lambda = Expression.Lambda(body, lambdaParameter);
                var methodName = (isFirst, item.Direction) switch
                {
                    (true, DynamicSortDirection.Ascending) => nameof(Queryable.OrderBy),
                    (true, DynamicSortDirection.Descending) => nameof(Queryable.OrderByDescending),
                    (false, DynamicSortDirection.Ascending) => nameof(Queryable.ThenBy),
                    _ => nameof(Queryable.ThenByDescending),
                };

                var call = Expression.Call(
                    typeof(Queryable),
                    methodName,
                    [typeof(T), body.Type],
                    source.Expression,
                    Expression.Quote(lambda));

                source = source.Provider.CreateQuery<T>(call);
                isFirst = false;
            }

            return source;
        }

        private static string FormatSortField(string field, string? fieldType)
        {
            if (string.IsNullOrEmpty(fieldType)) return field;

            switch (fieldType)
            {
                case FieldType.Select1:
                case FieldType.Select2:
                case FieldType.CheckBox:
                case FieldType.Radio:
                case FieldType.Employee1:
                case FieldType.Employee2:
                case FieldType.Department1:
                case FieldType.Department2:
                    if (!(field.EndsWith(".id", StringComparison.OrdinalIgnoreCase)
                        || field.EndsWith(".code", StringComparison.OrdinalIgnoreCase)
                        || field.EndsWith(".value", StringComparison.OrdinalIgnoreCase)
                        || field.EndsWith(".label", StringComparison.OrdinalIgnoreCase)))
                    {
                        return $"{field}.label";
                    }

                    break;
            }

            return field;
        }

        /// <summary>
        /// 构建筛选条件的表达式体。
        /// </summary>
        private static Expression BuildPredicateBody<T>(DynamicFilter filter, ParameterExpression parameter)
        {
            if (filter.IsGroup)
            {
                var subExpressions = new List<Expression>();
                foreach (var item in filter.Items!)
                {
                    var sub = BuildPredicateBody<T>(item, parameter);
                    // 恒真子条件不参与组合，避免 and 组里出现 `true and ...`
                    if (sub is ConstantExpression { Value: true }) continue;
                    subExpressions.Add(sub);
                }

                if (subExpressions.Count == 0) return Expression.Constant(true, typeof(bool));

                Func<Expression, Expression, Expression> combine =
                    string.Equals(filter.Rel, FilterRel.Or, StringComparison.OrdinalIgnoreCase)
                        ? Expression.OrElse
                        : Expression.AndAlso;

                var aggregated = subExpressions.Aggregate(combine);
                if (string.Equals(filter.Rel, FilterRel.Not, StringComparison.OrdinalIgnoreCase))
                {
                    var andAlso = subExpressions.Aggregate(Expression.AndAlso);
                    return Expression.Not(andAlso);
                }

                return aggregated;
            }

            var fieldPath = DynamicField.FormatFieldForFilter(filter.Field!, filter.Type);
            var operation = filter.Op!.ToLowerInvariant();
            if (string.Equals(operation, FilterOp.Text, StringComparison.OrdinalIgnoreCase))
            {
                fieldPath = FormatTextSearchField(fieldPath, filter.Type);
            }

            // 会在下面那条恒假分支里被直接吞掉，绕过 EnsureSafeValues 的拒绝逻辑。
            var values = NormalizeValues(filter.Value);
            EnsureSafeValues(values);

            // jsonb 内部键（data.xxx）：转成带谓词的 JSONPath 交给 jsonb_path_exists。
            // 这样「数组任意元素满足」的语义才能落到数据库端，且 ExecuteUpdate / ExecuteDelete 可用。
            if (DynamicPathAccessor.TryResolveJsonbPath<T>(fieldPath, parameter, out var jsonbPath))
            {
                if (filter.Value is null)
                    return BuildJsonbNullPredicate(jsonbPath, operation);
                return BuildJsonbPredicate(jsonbPath, operation, values);
            }

            // 语义等价于「沿路径进入子表列」，路径拆分已由 PathAccessor 处理。
            // 必须复用外层 lambda 的参数实例，否则 EF Core 会判定为无法翻译的自由变量。
            var (body, staticType) = DynamicPathAccessor.BuildBody<T>(fieldPath, parameter);
            if (body is ConstantExpression { Value: false } || staticType == typeof(bool) && body is ConstantExpression)
            {
                return Expression.Constant(false, typeof(bool));
            }

            if (filter.Value is null)
                return BuildNullComparison(body, staticType, operation);

            return BuildComparison(body, staticType, operation, values, staticType);
        }

        /// <summary>
        /// 构建 jsonb 内部键的过滤谓词。
        /// </summary>
        /// <remarks>
        /// </remarks>
        private static Expression BuildJsonbPredicate(
            DynamicPathAccessor.JsonbPathResolution jsonb,
            string op,
            List<object> values)
        {
            var container = jsonb.Container;
            var path = jsonb.Path;

            Expression Match(string jsonPath) => Expression.Call(
                typeof(PgJsonFunctions).GetMethod(nameof(PgJsonFunctions.JsonMatch))!,
                container,
                Expression.Constant(jsonPath, typeof(string)));

            switch (op)
            {
                case FilterOp.Empty:
                    return Expression.Not(Match($"{path} ? (@ != null)"));
                case FilterOp.NotEmpty:
                    return Match($"{path} ? (@ != null)");
                case FilterOp.Exists:
                    return Match(path);
                case FilterOp.Text:
                {
                    var keyword = values.Count > 0 ? values[0]?.ToString() : null;
                    if (string.IsNullOrWhiteSpace(keyword)) return Expression.Constant(true, typeof(bool));
                    // like_regex 的匹配串是 XQuery 正则：先按正则转义成字面量，再按 JSONPath
                    // 字符串字面量的规则转义（反斜杠要写成 \\）。
                    var pattern = ToJsonPathStringLiteral(Regex.Escape(keyword));
                    return Match($"{path} ? (@ like_regex {pattern} flag \"i\")");
                }

                case FilterOp.AllIn:
                    // 「所有给定值都必须出现」不能用单个过滤器表达（过滤器是逐元素求值的），
                    // 只能拆成若干个「存在等于该值的元素」再取与。
                    if (values.Count == 0) return Expression.Constant(false, typeof(bool));
                    return values
                        .Select(value => Match($"{path} ? (@ == {ToJsonLiteral(value)})"))
                        .Aggregate(Expression.AndAlso);

                case FilterOp.Ne:
                    // 不能用 @ != v：lax JSONPath 下 @ 会逐元素求反，把「恰好含有一个等于 v 的元素的数组」也判为命中，
                    if (values.Count == 0) return Expression.Constant(true, typeof(bool));
                    var neEq = Match($"{path} ? (@ == {ToJsonLiteral(values[0])})");
                    return Expression.AndAlso(Match(path), Expression.Not(neEq));

                case FilterOp.Nin:
                    // 必须写成「Exists AND NOT(任一元素 ∈ 集合)」，否则 @ != 集合 会被 lax 模式逐元素求反，
                    if (values.Count == 0) return Expression.Constant(true, typeof(bool));
                    var ninEqualities = string.Join(" || ", values.Select(v => $"@ == {ToJsonLiteral(v)}"));
                    var ninAny = Match($"{path} ? ({ninEqualities})");
                    return Expression.AndAlso(Match(path), Expression.Not(ninAny));
            }

            if (values.Count == 0)
            {
                // 与标量分支保持一致：in 家族的空集合匹配不到任何行，nin 家族恒真。
                return op is FilterOp.In
                    ? Expression.Constant(false, typeof(bool))
                    : Expression.Constant(true, typeof(bool));
            }

            // 运算符已在 DynamicFilterValidator 中校验；这里仅保留翻译器的最终兜底，
            // 防止未来新增操作符时静默删除业务条件。
            var predicate = BuildJsonbPredicateBody(op, values)
                ?? throw new BadRequestException($"不支持的过滤运算符: {op}");
            return Match($"{path} ? ({predicate})");
        }

        private static Expression BuildJsonbNullPredicate(
            DynamicPathAccessor.JsonbPathResolution jsonb,
            string op)
        {
            var container = jsonb.Container;
            var path = jsonb.Path;
            Expression Match(string jsonPath) => Expression.Call(
                typeof(PgJsonFunctions).GetMethod(nameof(PgJsonFunctions.JsonMatch))!,
                container,
                Expression.Constant(jsonPath, typeof(string)));

            var isNull = Match($"{path} ? (@ == null)");
            return op switch
            {
                FilterOp.Empty => Expression.Not(Match($"{path} ? (@ != null)")),
                FilterOp.NotEmpty => Match($"{path} ? (@ != null)"),
                FilterOp.Exists => Match(path),
                FilterOp.Ne or FilterOp.Nin
                    => Expression.AndAlso(Match(path), Expression.Not(isNull)),
                FilterOp.Eq => isNull,
                _ => throw new BadRequestException($"运算符 {op} 不接受空过滤值"),
            };
        }

        /// <summary>
        /// 构建 JSONPath 过滤器里的比较表达式文本；不支持的运算符返回 <c>null</c>。
        /// </summary>
        /// <returns>JSONPath 谓词文本，或 <c>null</c>。</returns>
        private static string? BuildJsonbPredicateBody(string op, List<object> values)
        {
            if (values.Count == 0) return null;

            var literals = values.Select(ToJsonLiteral).ToList();
            var equalities = string.Join(" || ", literals.Select(x => $"@ == {x}"));

            switch (op)
            {
                case FilterOp.Eq:
                    return $"@ == {literals[0]}";

                case FilterOp.Gt:
                    return $"@ > {literals[0]}";
                case FilterOp.Gte:
                    return $"@ >= {literals[0]}";
                case FilterOp.Lt:
                    return $"@ < {literals[0]} || @ == null";
                case FilterOp.Lte:
                    return $"@ <= {literals[0]} || @ == null";

                case FilterOp.In:
                    return equalities;

                case FilterOp.Between:
                    return values.Count < 2
                        ? $"@ >= {literals[0]}"
                        : $"@ >= {literals[0]} && @ <= {literals[1]}";

                default:
                    return null;
            }
        }

        /// <summary>
        /// 把值序列化为可嵌进 JSONPath 的字面量：字符串带引号、数字与布尔按 JSON 写法。
        /// </summary>
        private static string ToJsonLiteral(object? value) => JsonSerializer.Serialize(value);

        /// <summary>把字符串转义成 JSONPath 里的字符串字面量（含引号）。</summary>
        /// <returns>形如 <c>"abc"</c> 的字面量。</returns>
        private static string ToJsonPathStringLiteral(string value)
            => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

        /// <summary>构建空值过滤表达式。</summary>
        private static Expression BuildNullComparison(Expression body, Type staticType, string op)
        {
            if (op is not (FilterOp.Eq or FilterOp.Ne or FilterOp.Nin or FilterOp.Empty or FilterOp.NotEmpty or FilterOp.Exists))
                throw new BadRequestException($"运算符 {op} 不接受空过滤值");

            var isNullable = !body.Type.IsValueType || Nullable.GetUnderlyingType(body.Type) is not null;
            if (IsCollectionType(body.Type))
            {
                var elementType = GetElementType(body.Type);
                if (!elementType.IsValueType || Nullable.GetUnderlyingType(elementType) is not null)
                {
                    var element = Expression.Parameter(elementType, "e");
                    var anyNull = Expression.Call(
                        typeof(Enumerable),
                        nameof(Enumerable.Any),
                        [elementType],
                        body,
                        Expression.Lambda(
                            Expression.Equal(element, Expression.Constant(null, elementType)),
                            element));
                    return op is FilterOp.Ne or FilterOp.Nin
                        ? Expression.Not(anyNull)
                        : anyNull;
                }

                return op is FilterOp.Ne or FilterOp.Nin
                    ? Expression.Constant(true)
                    : Expression.Constant(false);
            }

            if (op == FilterOp.Empty)
                return isNullable
                    ? Expression.Equal(body, Expression.Constant(null, body.Type))
                    : Expression.Constant(false);
            if (op == FilterOp.NotEmpty)
                return isNullable
                    ? Expression.NotEqual(body, Expression.Constant(null, body.Type))
                    : Expression.Constant(true);
            if (op == FilterOp.Exists)
                return staticType == typeof(object) && isNullable
                    ? Expression.NotEqual(body, Expression.Constant(null, body.Type))
                    : Expression.Constant(true);

            Expression equalsNull = isNullable
                ? Expression.Equal(body, Expression.Constant(null, body.Type))
                : Expression.Constant(false);
            return op switch
            {
                FilterOp.Eq => equalsNull,
                FilterOp.Ne or FilterOp.Nin => Expression.Not(equalsNull),
                _ => throw new BadRequestException($"运算符 {op} 不接受空过滤值"),
            };
        }

        /// <summary>构建单个字段的比较表达式。</summary>
        private static Expression BuildComparison(Expression body, Type staticType, string op, List<object> values, Type elementType)
        {
            var isNullable = !body.Type.IsValueType || Nullable.GetUnderlyingType(body.Type) is not null;
            var dynamicField = staticType == typeof(object);

            switch (op)
            {
                case FilterOp.Empty:
                    if (dynamicField)
                    {
                        // jsonb 键不存在或为 null → 取值为 null。用 == null 判定。
                        return Expression.Equal(body, Expression.Constant(null, typeof(object)));
                    }

                    if (isNullable)
                    {
                        return Expression.Equal(body, Expression.Constant(null, body.Type));
                    }

                    return Expression.Constant(false, typeof(bool));

                case FilterOp.NotEmpty:
                    if (dynamicField)
                    {
                        return Expression.NotEqual(body, Expression.Constant(null, typeof(object)));
                    }

                    if (isNullable)
                    {
                        return Expression.NotEqual(body, Expression.Constant(null, body.Type));
                    }

                    return Expression.Constant(true, typeof(bool));

                case FilterOp.Exists:
                    return dynamicField
                        ? Expression.NotEqual(body, Expression.Constant(null, typeof(object)))
                        : Expression.Constant(true, typeof(bool));

                case FilterOp.Text:
                    return BuildTextComparison(body, staticType, values);

                case FilterOp.In:
                    return BuildInComparison(body, staticType, values);

                case FilterOp.Nin:
                    return Expression.Not(BuildInComparison(body, staticType, values));

                case FilterOp.Between:
                    return BuildBetween(body, staticType, values);

                case FilterOp.AllIn:
                    return BuildAllIn(body, staticType, values);

                case FilterOp.Eq:
                    if (op is FilterOp.Gt or FilterOp.Gte or FilterOp.Lt or FilterOp.Lte or FilterOp.Ne)
                    {
                        return BuildBinary(body, staticType, op, values, elementType);
                    }

                    return BuildEquality(body, staticType, values);

                case FilterOp.Gt:
                case FilterOp.Gte:
                case FilterOp.Lt:
                case FilterOp.Lte:
                case FilterOp.Ne:
                    return BuildBinary(body, staticType, op, values, elementType);

                default:
                    throw new BadRequestException($"不支持的过滤运算符: {op}");
            }
        }

        /// <summary>
        /// 构建等值比较。
        /// </summary>
        private static Expression BuildEquality(Expression body, Type staticType, List<object> values)
        {
            if (values.Count == 0) return Expression.Constant(false, typeof(bool));

            var target = staticType == typeof(object) ? typeof(object) : body.Type;
            // 值必须按目标列类型转换：jsonb 路径取值是 string，而过滤值可能是数字/布尔，
            // 直接 Expression.Constant(4442, typeof(string)) 会在构造表达式时抛错。
            var constant = Expression.Constant(ConvertValue(values[0], target), target);

            if (IsCollectionType(target))
            {
                return BuildContainsCall(body, values[0]);
            }

            return Expression.Equal(Convert(body, target), constant);
        }

        /// <summary>
        /// 构建比较运算（gt/gte/lt/lte/ne）。
        /// </summary>
        private static Expression BuildBinary(Expression body, Type staticType, string op, List<object> values, Type elementType)
        {
            if (values.Count == 0) return Expression.Constant(false, typeof(bool));

            // 集合字段的比较沿用 EF 可翻译的 Enumerable.Any 语义。
            if (IsCollectionType(body.Type))
            {
                // 元素参数只能建一次：表达式体与 lambda 用同一个实例，否则 lambda 声明的
                // 是另一个自由变量，EF Core 同样会报「无法翻译」。
                var element = Expression.Parameter(elementType, "e");
                var any = Expression.Call(
                    typeof(Enumerable),
                    nameof(Enumerable.Any),
                    [elementType],
                    body,
                    Expression.Lambda(
                        BuildValueComparison(
                            element,
                            Expression.Constant(values[0], elementType),
                            op),
                        element));
                return any;
            }

            if (staticType == typeof(object))
            {
                // jsonb 内取值后比较：EF Core 会翻译为 jsonb 字段的文本比较，
                // 对数值型键需要依靠 jsonb 的 ->> 与 CAST，这里保持 object 级别比较。
                var constant = Expression.Constant(values[0], typeof(object));
                return BuildValueComparison(body, constant, op);
            }

            // 与 BuildEquality 同理：过滤值可能是字符串/数字/布尔，必须按列类型转换后再成常量。
            var typedConstant = Expression.Constant(ConvertValue(values[0], body.Type), body.Type);
            return BuildValueComparison(body, typedConstant, op);
        }

        /// <summary>
        /// 根据运算符生成二元比较表达式。
        /// </summary>
        private static Expression BuildValueComparison(Expression left, Expression right, string op)
        {
            var leftConverted = Convert(left, right.Type);
            var rightConverted = Convert(right, left.Type);
            var comparisonType = leftConverted.Type;

            if (comparisonType == typeof(string))
            {
                var compare = typeof(string).GetMethod(nameof(string.Compare), [typeof(string), typeof(string)])!;
                var call = Expression.Call(compare, leftConverted, rightConverted);
                var zero = Expression.Constant(0);
                return op switch
                {
                    FilterOp.Gt => Expression.GreaterThan(call, zero),
                    FilterOp.Gte => Expression.GreaterThanOrEqual(call, zero),
                    FilterOp.Lt => Expression.LessThan(call, zero),
                    FilterOp.Lte => Expression.LessThanOrEqual(call, zero),
                    FilterOp.Ne => Expression.NotEqual(leftConverted, rightConverted),
                    _ => Expression.Equal(leftConverted, rightConverted),
                };
            }

            if (comparisonType == typeof(object))
            {
                // object 级别无法直接比较大小，退化为字符串比较，保证不抛异常。
                var toString = leftConverted;
                var leftText = Expression.Call(toString, typeof(object).GetMethod(nameof(ToString))!);
                var rightText = Expression.Call(
                    Expression.Convert(right, typeof(object)),
                    typeof(object).GetMethod(nameof(ToString))!);
                var compare = typeof(string).GetMethod(nameof(string.Compare), [typeof(string), typeof(string)])!;
                var call = Expression.Call(compare, leftText, rightText);
                var zero = Expression.Constant(0);
                return op switch
                {
                    FilterOp.Gt => Expression.GreaterThan(call, zero),
                    FilterOp.Gte => Expression.GreaterThanOrEqual(call, zero),
                    FilterOp.Lt => Expression.LessThan(call, zero),
                    FilterOp.Lte => Expression.LessThanOrEqual(call, zero),
                    FilterOp.Ne => Expression.NotEqual(leftText, rightText),
                    _ => Expression.Equal(leftText, rightText),
                };
            }

            return op switch
            {
                FilterOp.Gt => Expression.GreaterThan(leftConverted, rightConverted),
                FilterOp.Gte => Expression.GreaterThanOrEqual(leftConverted, rightConverted),
                FilterOp.Lt => Expression.LessThan(leftConverted, rightConverted),
                FilterOp.Lte => Expression.LessThanOrEqual(leftConverted, rightConverted),
                FilterOp.Ne => Expression.NotEqual(leftConverted, rightConverted),
                _ => Expression.Equal(leftConverted, rightConverted),
            };
        }

        /// <summary>
        /// 构建 <c>in</c> 系列比较。
        /// </summary>
        private static Expression BuildInComparison(Expression body, Type staticType, List<object> values)
        {
            if (values.Count == 0) return Expression.Constant(false, typeof(bool));

            if (IsCollectionType(body.Type))
            {
                // 集合字段的 in：任意元素属于给定集合。
                var elementType = GetElementType(body.Type);
                var list = Expression.Constant(CastValues(values, elementType));
                var containsMethod = typeof(Enumerable)
                    .GetMethods()
                    .First(x => x.Name == nameof(Enumerable.Contains) && x.GetParameters().Length == 2)
                    .MakeGenericMethod(elementType);
                var anyMethod = typeof(Enumerable)
                    .GetMethods()
                    .First(x => x.Name == nameof(Enumerable.Any) && x.GetParameters().Length == 2)
                    .MakeGenericMethod(elementType);
                var element = Expression.Parameter(elementType, "e");
                return Expression.Call(
                    anyMethod,
                    body,
                    Expression.Lambda(Expression.Call(containsMethod, list, element), element));
            }

            if (staticType == typeof(object))
            {
                var textValues = values.Select(x => x?.ToString() ?? string.Empty).ToList();
                var textList = Expression.Constant(textValues);
                var textBody = Expression.Call(body, typeof(object).GetMethod(nameof(ToString))!);
                var contains = typeof(List<string>).GetMethod(nameof(List<string>.Contains), [typeof(string)])!;
                return Expression.Call(textList, contains, textBody);
            }

            var typedList = BuildTypedList(values, body.Type);
            var typedListExpression = Expression.Constant(typedList, typedList.GetType());
            var typedContains = typedList.GetType().GetMethod(nameof(List<object>.Contains), [body.Type])
                ?? typedList.GetType().GetMethods().First(x => x.Name == "Contains");
            return Expression.Call(typedListExpression, typedContains, body);
        }

        /// <summary>
        /// 构建 <c>between</c> 比较。
        /// </summary>
        private static Expression BuildBetween(Expression body, Type staticType, List<object> values)
        {
            if (values.Count == 0) return Expression.Constant(false, typeof(bool));
            if (values.Count == 1) return BuildEquality(body, staticType, values);

            var target = body.Type == typeof(object) ? typeof(object) : body.Type;
            var lower = BuildValueComparison(body, Expression.Constant(ConvertValue(values[0], target), target), FilterOp.Gte);
            var upper = BuildValueComparison(body, Expression.Constant(ConvertValue(values[1], target), target), FilterOp.Lte);
            return Expression.AndAlso(lower, upper);
        }

        /// <summary>
        /// 构建 <c>allin</c> 比较：所有给定值都必须出现在集合字段中。
        /// </summary>
        private static Expression BuildAllIn(Expression body, Type staticType, List<object> values)
        {
            if (values.Count == 0) return Expression.Constant(false, typeof(bool));

            Expression? result = null;
            foreach (var value in values)
            {
                var contains = IsCollectionType(body.Type)
                    ? BuildContainsCall(body, value)
                    : BuildEquality(body, staticType, [value]);
                result = result is null ? contains : Expression.AndAlso(result, contains);
            }

            return result ?? Expression.Constant(true, typeof(bool));
        }

        /// <summary>
        /// 构建集合字段的 <c>Contains</c> 调用。
        /// </summary>
        private static Expression BuildContainsCall(Expression body, object value)
        {
            var elementType = GetElementType(body.Type);
            if (elementType == typeof(string))
            {
                // 必须是真正的 List<string>：List<object>.Contains 会被 EF Core 当成
                // object 参数处理，落到 SQL 上就是无类型参数，IN 比较会失配。
                var list = Expression.Constant(BuildTypedList([value], typeof(string)));
                return Expression.Call(
                    list,
                    typeof(List<string>).GetMethod(nameof(List<string>.Contains), [typeof(string)])!,
                    Convert(body, typeof(string)));
            }

            return Expression.Call(
                typeof(Enumerable),
                nameof(Enumerable.Contains),
                [elementType],
                body,
                Expression.Constant(ConvertValue(value, elementType), elementType));
        }

        /// <summary>
        /// 按元素类型构造强类型 <see cref="List{T}"/>，供 EF Core 翻译成 <c>IN (...)</c>。
        /// </summary>
        private static IList BuildTypedList(IEnumerable<object> values, Type elementType)
        {
            var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType))!;
            foreach (var value in values)
            {
                list.Add(ConvertValue(value, elementType));
            }

            return list;
        }

        /// <summary>
        /// PostgreSQL 下翻译为 <c>ILike</c> 便于使用 pg_trgm 索引。
        /// </summary>
        private static Expression BuildTextComparison(Expression body, Type staticType, List<object> values)
        {
            if (values.Count == 0) return Expression.Constant(true, typeof(bool));

            var keyword = values[0]?.ToString();
            if (string.IsNullOrWhiteSpace(keyword)) return Expression.Constant(true, typeof(bool));

            var pattern = EscapeLikePattern(keyword.ToLowerInvariant());
            var likeMethod = typeof(DbFunctionsExtensions).GetMethod(
                nameof(DbFunctionsExtensions.Like),
                [typeof(DbFunctions), typeof(string), typeof(string)])!;

            if (IsCollectionType(body.Type))
            {
                var elementType = GetElementType(body.Type);
                if (elementType != typeof(string)) return Expression.Constant(false, typeof(bool));

                var element = Expression.Parameter(elementType, "e");
                var loweredElement = Expression.Call(element, nameof(string.ToLower), Type.EmptyTypes);
                var likeCall = Expression.Call(
                    null,
                    likeMethod,
                    Expression.Constant(EF.Functions),
                    loweredElement,
                    Expression.Constant(pattern));
                return Expression.Call(
                    typeof(Enumerable),
                    nameof(Enumerable.Any),
                    [elementType],
                    body,
                    Expression.Lambda(likeCall, element));
            }

            var textBody = staticType == typeof(object)
                ? Expression.Call(body, typeof(object).GetMethod(nameof(ToString))!)
                : Convert(body, typeof(string));

            var loweredText = Expression.Call(textBody, nameof(string.ToLower), Type.EmptyTypes);
            return Expression.Call(
                null,
                likeMethod,
                Expression.Constant(EF.Functions),
                loweredText,
                Expression.Constant(pattern));
        }

        /// <summary>
        /// 文本搜索字段后缀规整（选项字段默认搜 label）。
        /// </summary>
        private static string FormatTextSearchField(string field, string? fieldType)
        {
            if (!IsOptionFieldType(fieldType)) return field;

            var segments = DynamicPathAccessor.SplitPath(field);
            if (segments.Count >= 2)
            {
                var child = segments[^1];
                if (child.EndsWith(".value", StringComparison.OrdinalIgnoreCase))
                {
                    child = $"{child[..^".value".Length]}.label";
                }
                else if (!child.EndsWith(".label", StringComparison.OrdinalIgnoreCase))
                {
                    child = $"{child}.label";
                }

                segments[^1] = child;
                return string.Join('>', segments);
            }

            if (field.EndsWith(".value", StringComparison.OrdinalIgnoreCase))
            {
                return $"{field[..^".value".Length]}.label";
            }

            return field.EndsWith(".label", StringComparison.OrdinalIgnoreCase) ? field : $"{field}.label";
        }

        private static bool IsOptionFieldType(string? fieldType) =>
            fieldType == FieldType.Select1
            || fieldType == FieldType.Select2
            || fieldType == FieldType.CheckBox
            || fieldType == FieldType.Radio;

        /// <summary>
        /// 归一化过滤值。标量包装为单元素列表，集合展开为多元素列表。
        /// </summary>
        private static List<object> NormalizeValues(object? value)
        {
            var normalized = DynamicValueNormalizer.Normalize(value);
            if (normalized is null) return [];

            if (normalized is IEnumerable enumerable and not string and not IDictionary)
            {
                return enumerable.Cast<object?>()
                    .Select(x => DynamicValueNormalizer.Normalize(x))
                    .Where(x => x is not null)
                    .Select(x => x!)
                    .ToList();
            }

            return [normalized];
        }

        private static void EnsureSafeValues(IEnumerable<object> values)
        {
            foreach (var value in values)
            {
                if (ContainsOperatorObject(value))
                {
                    throw new BadRequestException("过滤值不允许包含操作符对象");
                }
            }
        }

        private static bool ContainsOperatorObject(object? value)
        {
            if (value is IDictionary dictionary)
            {
                foreach (DictionaryEntry entry in dictionary)
                {
                    if (entry.Key is string key && key.StartsWith('$')) return true;
                    if (ContainsOperatorObject(entry.Value)) return true;
                }

                return false;
            }

            if (value is IEnumerable items && value is not string)
            {
                foreach (var item in items)
                {
                    if (ContainsOperatorObject(item)) return true;
                }
            }

            return false;
        }

        private static bool IsCollectionType(Type type)
        {
            var underlying = Nullable.GetUnderlyingType(type) ?? type;
            if (underlying == typeof(string)) return false;
            return DynamicPathAccessor.TryGetEnumerableElementType(underlying, out _);
        }

        private static Type GetElementType(Type type)
        {
            var underlying = Nullable.GetUnderlyingType(type) ?? type;
            return DynamicPathAccessor.TryGetEnumerableElementType(underlying, out var elementType)
                ? elementType
                : typeof(object);
        }

        private static List<TValue> CastValues<TValue>(IEnumerable<object> values)
            => values.Select(x => (TValue)ConvertValue(x, typeof(TValue))!).ToList();

        private static List<object> CastValues(IEnumerable<object> values, Type target)
            => values.Select(x => ConvertValue(x, target)).ToList();

        private static object? ConvertValue(object? value, Type target)
        {
            if (value is null) return null;

            var underlying = Nullable.GetUnderlyingType(target) ?? target;
            if (underlying == typeof(object)) return value;
            if (underlying.IsInstanceOfType(value)) return value;

            if (underlying.IsEnum)
            {
                return value is string text ? Enum.Parse(underlying, text, true) : Enum.ToObject(underlying, value);
            }

            if (underlying == typeof(Guid) && value is string guidText) return Guid.Parse(guidText);

            if (underlying == typeof(DateTime) && value is string dateText) return DateTime.Parse(dateText);

            if (underlying == typeof(bool) && value is string boolText) return bool.Parse(boolText);

            // 目标类型是 string 的场景主要是 jsonb 路径取值（一律按文本比较）。
            // 这里必须用不变文化的字面量形式，且布尔要小写——jsonb 里存的就是 true/false，
            // 用 Convert.ChangeType 会得到 "True"/"False" 而匹配不上。
            if (underlying == typeof(string))
            {
                return value switch
                {
                    bool flag => flag ? "true" : "false",
                    IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                    _ => value.ToString() ?? string.Empty,
                };
            }

            return System.Convert.ChangeType(value, underlying);
        }

        private static Expression Convert(Expression expression, Type target)
        {
            if (expression.Type == target) return expression;
            return DynamicPathAccessor.ConvertTo(expression, target);
        }

        private static Expression<Func<T, bool>> True<T>() => _ => true;

        /// <summary>
        /// 以逻辑与组合两个谓词表达式。
        /// </summary>
        /// <remarks>
        /// 不能直接用 <c>Expression.AndAlso(left.Body, right.Body)</c>，因为两个 lambda 的参数实例不同，
        /// 必须先把右式参数替换为左式参数，否则 EF Core 会因「多个参数」而无法翻译。
        /// </remarks>
        public static Expression<Func<T, bool>> AndAlso<T>(
            this Expression<Func<T, bool>> left,
            Expression<Func<T, bool>> right)
        {
            ArgumentNullException.ThrowIfNull(left);
            ArgumentNullException.ThrowIfNull(right);

            var parameter = left.Parameters[0];
            var visitor = new ParameterReplacer(right.Parameters[0], parameter);
            var body = Expression.AndAlso(left.Body, visitor.Visit(right.Body)!);
            return Expression.Lambda<Func<T, bool>>(body, parameter);
        }

        /// <summary>
        /// 以逻辑或组合两个谓词表达式。
        /// </summary>
        public static Expression<Func<T, bool>> OrElse<T>(
            this Expression<Func<T, bool>> left,
            Expression<Func<T, bool>> right)
        {
            ArgumentNullException.ThrowIfNull(left);
            ArgumentNullException.ThrowIfNull(right);

            var parameter = left.Parameters[0];
            var visitor = new ParameterReplacer(right.Parameters[0], parameter);
            var body = Expression.OrElse(left.Body, visitor.Visit(right.Body)!);
            return Expression.Lambda<Func<T, bool>>(body, parameter);
        }

        /// <summary>
        /// 把用户输入转义为可安全嵌入 <c>LIKE</c>/<c>ILIKE</c> 模式的关键字。
        /// </summary>
        /// <returns>已转义的 <c>%keyword%</c> 模式。</returns>
        /// <remarks>
        /// <c>ILIKE</c> 是**通配符**语义，两者的元字符集合不同：
        /// <list type="bullet">
        /// <item><description>正则元字符（<c>. * + ? ( ) [ ] { } ^ $ | \</c>）在 LIKE 中是普通字符，
        /// 直接保留即可，不能让 <c>Regex.Escape</c> 的反斜杠污染模式——PostgreSQL 的 LIKE
        /// 默认转义符就是 <c>\</c>，会把 <c>\.</c> 解读为「转义的 .」从而匹配不到。</description></item>
        /// <item><description>只有 <c>%</c> 和 <c>_</c> 需要转义成 <c>\%</c> / <c>\_</c>。</description></item>
        /// <item><description>已存在的反斜杠必须先加倍，否则会与后续添加的转义符混淆。</description></item>
        /// </list>
        /// 该行为与 <c>DynamicQueryExtensions.BuildTextComparison</c> 中 <c>FilterOp.Text</c>
        /// 的处理保持一致。
        /// </remarks>
        public static string EscapeLikePattern(string? keyword)
        {
            if (string.IsNullOrEmpty(keyword)) return "%";

            var escaped = keyword
                .Replace(@"\", @"\\")
                .Replace("%", @"\%")
                .Replace("_", @"\_");

            return $"%{escaped}%";
        }

        /// <summary>
        /// 把表达式树中的某个参数替换为另一个参数。
        /// </summary>
        private sealed class ParameterReplacer(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
        {
            /// <inheritdoc />
            protected override Expression VisitParameter(ParameterExpression node)
                => node == from ? to : base.VisitParameter(node);
        }
    }
}
