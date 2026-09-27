using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Collections;
using System.Reflection;
using EIMSNext.Core.Abstractions;
using EIMSNext.Entities;
using EIMSNext.Json.Serialization;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace EIMSNext.Persistence.PostgreSql;

/// <summary>
/// 「复杂对象 / 对象集合 ↔ jsonb 文本」值转换器的工厂。
/// </summary>
/// <remarks>
/// </remarks>
public static class JsonbValueConverter
{
    private static readonly JsonSerializerOptions PersistenceOptions = CreatePersistenceOptions();

    /// <summary>生成 <typeparamref name="TValue"/> 与 jsonb 文本互转的值转换器。</summary>
    /// <typeparam name="TValue">被内嵌存储的 CLR 类型。</typeparam>
    /// <returns>存取均为 jsonb 文本的值转换器。</returns>
    /// <remarks>
    /// 约束写成 <c>class?</c> 而不是 <c>class</c>，是为了让可空属性（如
    /// <c>Wf_Definition.EventSetting?</c>）也能拿到类型精确匹配的转换器：
    /// <c>PropertyBuilder&lt;TProperty&gt;.HasConversion</c> 的签名是
    /// <c>ValueConverter&lt;TProperty?, TProvider&gt;</c>，若这里固定成非空
    /// <c>ValueConverter&lt;EventSetting, string&gt;</c>，编译器会报 CS8620 可空性不匹配。
    /// EF Core 对 NULL 值不调用转换器，两种写法运行时完全一致。
    /// </remarks>
    public static ValueConverter<TValue, string> Create<TValue>() where TValue : class?
        => new(
            value => JsonSerializer.Serialize(value, PersistenceOptions),
            value => JsonSerializer.Deserialize<TValue>(value, PersistenceOptions)!);

    /// <summary>
    /// 为使用 JSONB 转换器的集合属性创建结构比较器，确保集合原地修改能被 EF 检测到。
    /// </summary>
    public static ValueComparer CreateComparer(Type clrType)
    {
        ArgumentNullException.ThrowIfNull(clrType);
        var method = typeof(JsonbValueConverter)
            .GetMethod(nameof(CreateComparerCore), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(clrType);
        return (ValueComparer)method.Invoke(null, null)!;
    }

    private static ValueComparer<TValue> CreateComparerCore<TValue>() where TValue : class?
        => new(
            (left, right) => Serialize(left) == Serialize(right),
            value => StringComparer.Ordinal.GetHashCode(Serialize(value)),
            value => Deserialize<TValue>(Serialize(value))!);

    private static string Serialize<TValue>(TValue? value)
        => JsonSerializer.Serialize(value, PersistenceOptions);

    private static TValue? Deserialize<TValue>(string value)
        => JsonSerializer.Deserialize<TValue>(value, PersistenceOptions);

    private static JsonSerializerOptions CreatePersistenceOptions()
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(typeInfo =>
        {
            // FormDef.PublicRelatedFormIds 上的 [JsonIgnore] 只服务于 HTTP 表现层；
            // 它是持久化聚合的一部分，落 jsonb 时必须保留。
            if (typeInfo.Type == typeof(FormDef))
            {
                var property = typeInfo.Properties.FirstOrDefault(x => x.Name == nameof(FormDef.PublicRelatedFormIds));
                if (property is not null) typeInfo.Properties.Remove(property);
                property = typeInfo.CreateJsonPropertyInfo(
                    typeof(List<string>), nameof(FormDef.PublicRelatedFormIds));
                property.Get = value => ((FormDef)value).PublicRelatedFormIds;
                property.Set = (value, propertyValue) =>
                    ((FormDef)value).PublicRelatedFormIds = (List<string>?)propertyValue ?? [];
                typeInfo.Properties.Add(property);
                property.ShouldSerialize = static (_, _) => true;
            }

        });

        return new JsonSerializerOptions { TypeInfoResolver = resolver };
    }
}

/// <summary>
/// <see cref="Dictionary{String,Object}"/> 与 jsonb 文本的互转，供动态字段（FormData.Data 等）使用。
/// </summary>
/// <remarks>
/// <para>
/// 读回必须做「JsonElement → CLR 类型」的深度还原：EF Core 的
/// <c>JsonSerializer.Deserialize&lt;Dictionary&lt;string, object?&gt;&gt;</c> 默认会把内部值全部退化成
/// <see cref="System.Text.Json.JsonElement"/>，公式引擎（只接收 CLR 标量/字典/列表）的
/// 数值运算、布尔判断都会失效。还原规则（整数→long、小数→decimal、嵌套对象→
/// Dictionary、数组→List&lt;object?&gt;、bool/string 原样）由 EIMSNext.Json 层的
/// <see cref="DynamicValueReader"/> 统一提供，请求路径、导入路径共用同一实现。
/// </para>
/// <para>
/// 注意 DateTime 经默认序列化是 ISO 字符串，读回为 string（与 Json 层行为一致），
/// 由下游按字段语义自行解析。
/// </para>
/// <para>
/// 历史说明：这里曾是 <see cref="ExpandoObject"/>（91µs/行 vs 43µs/行，基准见
/// .workbuddy/memory/2026-09-20.md）。ExpandoObject 的 DLR 元对象支持在代码库中
/// 从未被 <c>dynamic</c> 访问用到（全部走 <c>IDictionary&lt;string, object?&gt;</c> 强转），
/// 属于纯开销，故统一改为 Dictionary。
/// </para>
/// </remarks>
public sealed class DynamicJsonbValueConverter() : ValueConverter<Dictionary<string, object?>, string>(
    value => JsonSerializer.Serialize((IDictionary<string, object?>)value, (JsonSerializerOptions?)null),
    value => DynamicJsonbReader.Parse(value) ?? new Dictionary<string, object?>());

/// <summary>
/// jsonb 文本 → 带类型 Dictionary 的深度还原器，见 <see cref="DynamicJsonbValueConverter"/> 的说明。
/// </summary>
/// <remarks>
/// 还原规则不再自己实现，统一委托给 EIMSNext.Json 层的 <see cref="DynamicValueReader"/>：
/// 请求路径（<see cref="DictionaryJsonConverter"/>）、导入路径都用它，
/// 保证同一个数值从任何入口进入 FormData.Data 都是同一个 CLR 类型。
/// </remarks>
public static class DynamicJsonbReader
{
    /// <summary>把 jsonb 文本解析为内部值带 CLR 类型的 Dictionary；顶层不是对象时返回 null。</summary>
    /// <returns>还原后的 Dictionary。</returns>
    public static Dictionary<string, object?>? Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        return root.ValueKind == JsonValueKind.Object ? DynamicValueReader.ToDictionary(root) : null;
    }
}

/// <summary>
/// <see cref="Operator"/> 与 jsonb 文本的互转，用于 CreateBy / UpdateBy 审计字段。
/// </summary>
public sealed class OperatorJsonConverter() : ValueConverter<Operator, string>(
    value => JsonSerializer.Serialize(value, (JsonSerializerOptions?)null),
    value => JsonSerializer.Deserialize<Operator>(value, (JsonSerializerOptions?)null) ?? Operator.Empty);
