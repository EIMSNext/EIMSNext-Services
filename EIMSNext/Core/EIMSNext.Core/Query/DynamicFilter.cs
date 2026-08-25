using System.Text.Json.Serialization;

namespace EIMSNext.Core.Query
{
    /// <summary>
    /// 动态筛选条件。设置 Field 时表示叶子条件，设置 Items 时表示条件组。
    /// </summary>
    public class DynamicFilter
    {
        private static readonly DynamicFilter _empty = new DynamicFilter();
        public static DynamicFilter Empty => _empty;

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

        [JsonIgnore]
        public bool IsEmpty => string.IsNullOrEmpty(Field) && !IsGroup;

        [JsonIgnore]
        public bool IsGroup => Items?.Count > 0;
    }

    public static class FilterOp
    {
        public const string AnyEq = "anyeq";
        public const string AnyGt = "anygt";
        public const string AnyGte = "anygte";
        public const string AnyIn = "anyin";
        public const string AnyLt = "anylt";
        public const string AnyLte = "anylte";
        public const string AnyNe = "anyne";
        public const string AnyNin = "anynin";
        public const string AnyStringIn = "anystringin";
        public const string AnyStringNin = "anystringnin";
        public const string ElemMatch = "elemmatch";
        public const string Eq = "eq";
        public const string Exists = "exists";
        public const string Gt = "gt";
        public const string Gte = "gte";
        public const string In = "in";
        public const string AllIn = "allin";
        public const string Lt = "lt";
        public const string Lte = "lte";
        public const string Between = "between";
        public const string Ne = "ne";
        public const string Nin = "nin";
        public const string StringIn = "stringin";
        public const string StringNin = "stringnin";
        public const string Text = "text";
        public const string Empty = "empty";
        public const string NotEmpty = "notempty";
    }
    public static class FilterRel
    {
        public const string And = "and";
        public const string Or = "or";
        public const string Not = "not";
    }

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

        public static DynamicFilter? And(this DynamicFilter? current, string field, string op, object? value)
        {
            return current.And(new DynamicFilter { Field = field, Op = op, Value = value });
        }
    }
}
