using System.Text.Json.Serialization;

namespace EIMSNext.Core.Query
{
    public class DynamicFilter
    {
        private static readonly DynamicFilter _empty = new DynamicFilter();
        public static DynamicFilter Empty => _empty;

        public DynamicFilter()
        {
        }

        public string Rel { get; set; } = FilterRel.And;
        public List<DynamicFilter>? Items { get; set; }

        #region Filter Field

        public string? Field { get; set; }
        public string? Type { get; set; }
        public string? Op { get; set; }
        public object? Value { get; set; }
        public bool ValueIsExp { get; set; }
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
        public const string Lt = "lt";
        public const string Lte = "lte";
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
}
