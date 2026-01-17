using EIMSNext.Common;
using MongoDB.Bson;
using MongoDB.Driver;

namespace EIMSNext.Core.Query
{
    public static class DynamicFilterExtension
    {
        private static readonly string[] mainFields = ["corpId", "appId", "formId"];

        public static FilterDefinition<T> ToFilterDefinition<T>(this DynamicFilter filter)
        {
            if (filter == null || filter.IsEmpty)
                return Builders<T>.Filter.Empty;

            return ParseDynamicFilter<T>(filter);
        }

        private static FilterDefinition<T> ParseDynamicFilter<T>(DynamicFilter filter)
        {
            FilterDefinition<T> myFilter = Builders<T>.Filter.Empty;
            if (filter.IsEmpty)
                return myFilter;

            if (filter.IsGroup)
            {
                myFilter = ParseDynamicFilterGroup<T>(filter);
            }
            else
            {
                if (!string.IsNullOrEmpty(filter.Field) && !string.IsNullOrEmpty(filter.Op))
                {
                    // 不在此转换，应该在之外补上Data.
                    //var field = (string.IsNullOrEmpty(filter.Type)
                    //    || Consts.SystemFields.Contains(filter.Field, StringComparer.OrdinalIgnoreCase)
                    //    || mainFields.Contains(filter.Field, StringComparer.OrdinalIgnoreCase))
                    //    ? filter.Field : "Data." + filter.Field;
                    var field = FormatField(filter.Field, filter.Type);
                    var filterValues = new List<object>();
                    if (filter.Value is List<object>)
                    {
                        filterValues = (filter.Value as List<object>)!;
                    }
                    else
                    {
                        if (filter.Value != null)
                            filterValues.Add(filter.Value);
                    }

                    var arrField = "";
                    if (field.Contains('>'))
                    {
                        var fields = field.Split('>', StringSplitOptions.RemoveEmptyEntries);
                        arrField = fields[0];
                        field = fields[1];

                        myFilter = Builders<T>.Filter.ElemMatch<dynamic>(arrField, BuildFilter<dynamic>(field, filter.Op.ToLower(), filterValues));
                    }
                    else
                    {
                        myFilter = BuildFilter<T>(field, filter.Op.ToLower(), filterValues);
                    }
                }
            }

            return myFilter;
        }

        private static string FormatField(string field, string? fieldType)
        {
            var finalField = field;

            if (!string.IsNullOrEmpty(fieldType))
            {
                switch (fieldType)
                {
                    case FieldType.Select:
                    case FieldType.Select2:
                    case FieldType.CheckBox:
                    case FieldType.Radio:
                        if (!(
                           field.EndsWith(".value") ||
                           field.EndsWith(".label")))
                        {
                            finalField = $"{field}.value";
                        }
                        break;
                    case FieldType.Employee:
                    case FieldType.Employee2:
                    case FieldType.Department:
                    case FieldType.Department2:
                        if (!(field.EndsWith(".id") ||
                            field.EndsWith(".code") ||
                            field.EndsWith(".label")))
                        {
                            finalField = $"{field}.label";
                        }
                        break;
                }
            }

            return finalField;
        }
        private static FilterDefinition<T> BuildFilter<T>(string field, string op, List<object> filterValues)
        {
            var filter = Builders<T>.Filter.Empty;

            if (op == FilterOp.Empty)
            {
                filter = Builders<T>.Filter.Or(
                            Builders<T>.Filter.Exists(field, false),
                            Builders<T>.Filter.Eq(field, BsonNull.Value));
            }
            else
            {
                if (!Fields.IsSystemField(field))
                    filter = Builders<T>.Filter.Exists(field, true);

                FilterDefinition<T>? subFilter = null;
                switch (op)
                {
                    case FilterOp.NotEmpty:
                        subFilter = Builders<T>.Filter.Ne(field, BsonNull.Value);
                        break;
                    case FilterOp.AnyEq:
                        subFilter = Builders<T>.Filter.AnyEq(field, filterValues[0]);
                        break;
                    case FilterOp.AnyGt:
                        subFilter = Builders<T>.Filter.AnyGt(field, filterValues[0]);
                        break;
                    case FilterOp.AnyGte:
                        subFilter = Builders<T>.Filter.AnyGte(field, filterValues[0]);
                        break;
                    case FilterOp.AnyIn:
                        subFilter = Builders<T>.Filter.AnyIn(field, filterValues);
                        break;
                    case FilterOp.AnyLt:
                        subFilter = Builders<T>.Filter.AnyLt(field, filterValues[0]);
                        break;
                    case FilterOp.AnyLte:
                        subFilter = Builders<T>.Filter.AnyLte(field, filterValues[0]);
                        break;
                    case FilterOp.AnyNe:
                        subFilter = Builders<T>.Filter.AnyNe(field, filterValues[0]);
                        break;
                    case FilterOp.AnyNin:
                        subFilter = Builders<T>.Filter.AnyNin(field, filterValues);
                        break;
                    //case FilterOperation.AnyStringIn:
                    //    subFilter = Builders<dynamic>.Filter.AnyStringIn(field, filter.Value);
                    //    break;
                    //case FilterOperation.AnyStringNin:
                    //    subFilter = Builders<dynamic>.Filter.AnyStringNin(field, filter.Value);
                    //    break;
                    //case FilterOperation.ElemMatch:
                    //    subFilter = Builders<dynamic>.Filter.ElemMatch<T>(field, filter.Value[0]);
                    //    break;
                    case FilterOp.Exists:
                        break;
                    case FilterOp.Gt:
                        subFilter = Builders<T>.Filter.Gt(field, filterValues[0]);
                        break;
                    case FilterOp.In:
                        subFilter = Builders<T>.Filter.In(field, filterValues);
                        break;
                    case FilterOp.Lt:
                        subFilter = Builders<T>.Filter.Lt(field, filterValues[0]);
                        break;
                    case FilterOp.Lte:
                        subFilter = Builders<T>.Filter.Lte(field, filterValues[0]);
                        break;
                    case FilterOp.Ne:
                        subFilter = Builders<T>.Filter.Ne(field, filterValues[0]);
                        break;
                    case FilterOp.Nin:
                        subFilter = Builders<T>.Filter.Nin(field, filterValues);
                        break;
                    //case FilterOperation.StringIn:
                    //    subFilter = Builders<dynamic>.Filter.StringIn(field, filter.Value);
                    //    break;
                    //case FilterOperation.StringNin:
                    //    subFilter = Builders<dynamic>.Filter.StringNin(field, filter.Value);
                    //    break;
                    //case FilterOperation.Text:
                    //    subFilter = Builders<dynamic>.Filter.Text(field, filter.Value);
                    //    break;
                    default:
                        subFilter = Builders<T>.Filter.Eq(field, filterValues[0]);
                        break;
                }
                if (subFilter != null)
                    filter = Builders<T>.Filter.And(filter, subFilter);
            }

            return filter;
        }
        private static FilterDefinition<T> ParseDynamicFilterGroup<T>(DynamicFilter filter)
        {
            FilterDefinition<T> myFilter = Builders<T>.Filter.Empty;

            if (filter.IsGroup)
            {
                List<FilterDefinition<T>> subFilters = new List<FilterDefinition<T>>();
                foreach (var subFilter in filter.Items!)
                {
                    var sub = ParseDynamicFilter<T>(subFilter);
                    if (sub != Builders<T>.Filter.Empty)
                        subFilters.Add(sub);
                }

                if (subFilters.Any())
                {
                    switch (filter.Rel)
                    {
                        //case FilterRelation.Not:
                        //    myFilter = Builders<T>.Filter.Not(myFilter);
                        //    break;
                        case FilterRel.Or:
                            myFilter = Builders<T>.Filter.Or(subFilters);
                            break;
                        default:
                            myFilter = Builders<T>.Filter.And(subFilters);
                            break;
                    }
                }
            }

            return myFilter;
        }

        public static SortDefinition<T>? ToSortDefinition<T>(this DynamicSortList sortList)
        {
            if (sortList == null || sortList.Count == 0)
                return null;

            SortDefinition<T>? mySort = null;

            foreach (var sort in sortList)
            {
                if (!string.IsNullOrEmpty(sort.Field))
                {
                    if (sort.Dir == SortDir.Desc)
                    {
                        var st = Builders<T>.Sort.Descending(sort.Field);
                        mySort = mySort == null ? st : Builders<T>.Sort.Combine(mySort, st);
                    }
                    else
                    {
                        var st = Builders<T>.Sort.Ascending(sort.Field);
                        mySort = mySort == null ? st : Builders<T>.Sort.Combine(mySort, st);
                    }
                }
            }

            return mySort;
        }
        public static ProjectionDefinition<T>? ToProjectionDefinition<T>(this DynamicFieldList fieldList)
        {
            if (fieldList == null || fieldList.Count == 0)
                return null;

            ProjectionDefinition<T>? myProjection = null;

            foreach (var field in fieldList)
            {
                if (!string.IsNullOrEmpty(field.Field))
                {
                    if (field.Visible)
                    {
                        var proj = Builders<T>.Projection.Include(field.Field);
                        myProjection = myProjection == null ? proj : Builders<T>.Projection.Combine(myProjection, proj);
                    }
                    else
                    {
                        var proj = Builders<T>.Projection.Exclude(field.Field);
                        myProjection = myProjection == null ? proj : Builders<T>.Projection.Combine(myProjection, proj);
                    }
                }
            }

            return myProjection;
        }
    }
}
