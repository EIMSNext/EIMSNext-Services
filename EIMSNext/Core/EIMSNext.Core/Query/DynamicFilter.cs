using System.Text.Json.Serialization;

namespace EIMSNext.Core.Query
{
    /// <summary>
    /// 动态筛选条件。设置 Field 时表示叶子条件，设置 Items 时表示条件组。
    /// </summary>
    public class DynamicFilter
    {
        private static readonly DynamicFilter _empty = new DynamicFilter();

        /// <summary>
        /// 获取空筛选条件实例。
        /// </summary>
        public static DynamicFilter Empty => _empty;

        /// <summary>
        /// 初始化 <see cref="DynamicFilter"/> 类的新实例。
        /// </summary>
        public DynamicFilter()
        {
        }

        /// <summary>条件关系：and、or 或 not。</summary>
        public string Rel { get; set; } = FilterRel.And;
        /// <summary>嵌套条件组。</summary>
        public List<DynamicFilter>? Items { get; set; }

        #region Filter Field

        /// <summary>字段路径。</summary>
        public string? Field { get; set; }
        /// <summary>动态字段类型，影响选项、人员和部门字段的实际存储路径。</summary>
        public string? Type { get; set; }
        /// <summary>筛选运算符。</summary>
        public string? Op { get; set; }
        /// <summary>比较值或值数组。</summary>
        public object? Value { get; set; }
        /// <summary>是否将 Value 作为表达式。</summary>
        public bool ValueIsExp { get; set; }
        /// <summary>是否将 Value 作为字段路径。</summary>
        public bool ValueIsField {  get; set; }

        #endregion

        /// <summary>
        /// 获取一个值，指示筛选条件是否为空。
        /// </summary>
        [JsonIgnore]
        public bool IsEmpty => string.IsNullOrEmpty(Field) && !IsGroup;

        /// <summary>
        /// 获取一个值，指示筛选条件是否为条件组。
        /// </summary>
        [JsonIgnore]
        public bool IsGroup => Items?.Count > 0;
    }

    /// <summary>
    /// 筛选运算符常量定义。
    /// </summary>
    public static class FilterOp
    {
        /// <summary>数组任意元素等于。</summary>
        public const string AnyEq = "anyeq";
        /// <summary>数组任意元素大于。</summary>
        public const string AnyGt = "anygt";
        /// <summary>数组任意元素大于等于。</summary>
        public const string AnyGte = "anygte";
        /// <summary>数组任意元素属于。</summary>
        public const string AnyIn = "anyin";
        /// <summary>数组任意元素小于。</summary>
        public const string AnyLt = "anylt";
        /// <summary>数组任意元素小于等于。</summary>
        public const string AnyLte = "anylte";
        /// <summary>数组任意元素不等于。</summary>
        public const string AnyNe = "anyne";
        /// <summary>数组任意元素不属于。</summary>
        public const string AnyNin = "anynin";
        /// <summary>数组任意字符串属于。</summary>
        public const string AnyStringIn = "anystringin";
        /// <summary>数组任意字符串不属于。</summary>
        public const string AnyStringNin = "anystringnin";
        /// <summary>元素匹配。</summary>
        public const string ElemMatch = "elemmatch";
        /// <summary>等于。</summary>
        public const string Eq = "eq";
        /// <summary>存在。</summary>
        public const string Exists = "exists";
        /// <summary>大于。</summary>
        public const string Gt = "gt";
        /// <summary>大于等于。</summary>
        public const string Gte = "gte";
        /// <summary>属于。</summary>
        public const string In = "in";
        /// <summary>全部属于。</summary>
        public const string AllIn = "allin";
        /// <summary>小于。</summary>
        public const string Lt = "lt";
        /// <summary>小于等于。</summary>
        public const string Lte = "lte";
        /// <summary>介于。</summary>
        public const string Between = "between";
        /// <summary>不等于。</summary>
        public const string Ne = "ne";
        /// <summary>不属于。</summary>
        public const string Nin = "nin";
        /// <summary>字符串属于。</summary>
        public const string StringIn = "stringin";
        /// <summary>字符串不属于。</summary>
        public const string StringNin = "stringnin";
        /// <summary>文本搜索。</summary>
        public const string Text = "text";
        /// <summary>为空。</summary>
        public const string Empty = "empty";
        /// <summary>不为空。</summary>
        public const string NotEmpty = "notempty";
    }

    /// <summary>
    /// 筛选条件关系常量定义。
    /// </summary>
    public static class FilterRel
    {
        /// <summary>且。</summary>
        public const string And = "and";
        /// <summary>或。</summary>
        public const string Or = "or";
        /// <summary>非。</summary>
        public const string Not = "not";
    }

    /// <summary>
    /// 动态筛选条件组合扩展方法。
    /// </summary>
    public static class DynamicFilterCompositionExtensions
    {
        /// <summary>
        /// Removes expression and field-reference semantics from a client supplied filter tree.
        /// Public tokens must only be able to submit literal values.
        /// </summary>
        public static void ClearValueExpressions(this DynamicFilter? filter)
        {
            if (filter == null)
            {
                return;
            }

            filter.ValueIsExp = false;
            filter.ValueIsField = false;
            foreach (var item in filter.Items ?? [])
            {
                item.ClearValueExpressions();
            }
        }

        /// <summary>
        /// 将当前筛选条件与附加筛选条件按且关系组合。
        /// </summary>
        /// <param name="current">当前筛选条件。</param>
        /// <param name="additional">附加筛选条件。</param>
        /// <returns>组合后的筛选条件。</returns>
        public static DynamicFilter? And(this DynamicFilter? current, DynamicFilter? additional)
        {
            if (current == null || current.IsEmpty)
            {
                return additional;
            }

            if (additional == null || additional.IsEmpty)
            {
                return current;
            }

            return new DynamicFilter
            {
                Rel = FilterRel.And,
                Items = [current, additional],
            };
        }

        /// <summary>
        /// 将当前筛选条件与一个字段条件按且关系组合。
        /// </summary>
        /// <param name="current">当前筛选条件。</param>
        /// <param name="field">字段路径。</param>
        /// <param name="op">筛选运算符。</param>
        /// <param name="value">比较值。</param>
        /// <returns>组合后的筛选条件。</returns>
        public static DynamicFilter? And(this DynamicFilter? current, string field, string op, object? value)
        {
            return current.And(new DynamicFilter { Field = field, Op = op, Value = value });
        }
    }
}
