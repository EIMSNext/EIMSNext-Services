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
                if (!string.IsNullOrEmpty(filter.Field) && !string.IsNullOrEmpty(filter.Op) && filter.Value != null)
                {
                    // 不在此转换，应该在之外补上Data.
                    //var field = (string.IsNullOrEmpty(filter.Type)
                    //    || Consts.SystemFields.Contains(filter.Field, StringComparer.OrdinalIgnoreCase)
                    //    || mainFields.Contains(filter.Field, StringComparer.OrdinalIgnoreCase))
                    //    ? filter.Field : "Data." + filter.Field;
                    var field = filter.Field;
                    var filterValues = new List<object>();
                    if (filter.Value is List<object>)
                    {
                        filterValues = (filter.Value as List<object>)!;
                    }
                    else
                    {
                        filterValues.Add(filter.Value);
                    }

                    var arrField = "";
                    if (field.Contains('>'))
                    {
                        var fields = field.Split('>', StringSplitOptions.RemoveEmptyEntries);
                        arrField = fields[0];
                        field = fields[1];

                        var subFilter=Builders<dynamic>.Filter.Empty;

                        switch (filter.Op.ToLower())
                        {
                            case FilterOp.AnyEq:
                                subFilter = Builders<dynamic>.Filter.AnyEq(field, filterValues[0]);
                                break;
                            case FilterOp.AnyGt:
                                subFilter = Builders<dynamic>.Filter.AnyGt(field, filterValues[0]);
                                break;
                            case FilterOp.AnyGte:
                                subFilter = Builders<dynamic>.Filter.AnyGte(field, filterValues[0]);
                                break;
                            case FilterOp.AnyIn:
                                subFilter = Builders<dynamic>.Filter.AnyIn(field, filterValues);
                                break;
                            case FilterOp.AnyLt:
                                subFilter = Builders<dynamic>.Filter.AnyLt(field, filterValues[0]);
                                break;
                            case FilterOp.AnyLte:
                                subFilter = Builders<dynamic>.Filter.AnyLte(field, filterValues[0]);
                                break;
                            case FilterOp.AnyNe:
                                subFilter = Builders<dynamic>.Filter.AnyNe(field, filterValues[0]);
                                break;
                            case FilterOp.AnyNin:
                                subFilter = Builders<dynamic>.Filter.AnyNin(field, filterValues);
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
                                subFilter = Builders<dynamic>.Filter.Exists(field);
                                break;
                            case FilterOp.Gt:
                                subFilter = Builders<dynamic>.Filter.Gt(field, filterValues[0]);
                                break;
                            case FilterOp.In:
                                subFilter = Builders<dynamic>.Filter.In(field, filterValues);
                                break;
                            case FilterOp.Lt:
                                subFilter = Builders<dynamic>.Filter.Lt(field, filterValues[0]);
                                break;
                            case FilterOp.Lte:
                                subFilter = Builders<dynamic>.Filter.Lte(field, filterValues[0]);
                                break;
                            case FilterOp.Ne:
                                subFilter = Builders<dynamic>.Filter.Ne(field, filterValues[0]);
                                break;
                            case FilterOp.Nin:
                                subFilter = Builders<dynamic>.Filter.Nin(field, filterValues);
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
                                subFilter = Builders<dynamic>.Filter.Eq(field, filterValues[0]);
                                break;
                        }

                        myFilter = Builders<T>.Filter.ElemMatch<dynamic>(arrField, subFilter);
                    }
                    else
                    {
                        switch (filter.Op.ToLower())
                        {
                            case FilterOp.AnyEq:
                                myFilter = Builders<T>.Filter.AnyEq(field, filterValues[0]);
                                break;
                            case FilterOp.AnyGt:
                                myFilter = Builders<T>.Filter.AnyGt(field, filterValues[0]);
                                break;
                            case FilterOp.AnyGte:
                                myFilter = Builders<T>.Filter.AnyGte(field, filterValues[0]);
                                break;
                            case FilterOp.AnyIn:
                                myFilter = Builders<T>.Filter.AnyIn(field, filterValues);
                                break;
                            case FilterOp.AnyLt:
                                myFilter = Builders<T>.Filter.AnyLt(field, filterValues[0]);
                                break;
                            case FilterOp.AnyLte:
                                myFilter = Builders<T>.Filter.AnyLte(field, filterValues[0]);
                                break;
                            case FilterOp.AnyNe:
                                myFilter = Builders<T>.Filter.AnyNe(field, filterValues[0]);
                                break;
                            case FilterOp.AnyNin:
                                myFilter = Builders<T>.Filter.AnyNin(field, filterValues);
                                break;
                            //case FilterOperation.AnyStringIn:
                            //    myFilter = Builders<T>.Filter.AnyStringIn(field, filter.Value);
                            //    break;
                            //case FilterOperation.AnyStringNin:
                            //    myFilter = Builders<T>.Filter.AnyStringNin(field, filter.Value);
                            //    break;
                            //case FilterOperation.ElemMatch:
                            //    myFilter = Builders<T>.Filter.ElemMatch<T>(field, filter.Value[0]);
                            //    break;
                            case FilterOp.Exists:
                                myFilter = Builders<T>.Filter.Exists(field);
                                break;
                            case FilterOp.Gt:
                                myFilter = Builders<T>.Filter.Gt(field, filterValues[0]);
                                break;
                            case FilterOp.In:
                                myFilter = Builders<T>.Filter.In(field, filterValues);
                                break;
                            case FilterOp.Lt:
                                myFilter = Builders<T>.Filter.Lt(field, filterValues[0]);
                                break;
                            case FilterOp.Lte:
                                myFilter = Builders<T>.Filter.Lte(field, filterValues[0]);
                                break;
                            case FilterOp.Ne:
                                myFilter = Builders<T>.Filter.Ne(field, filterValues[0]);
                                break;
                            case FilterOp.Nin:
                                myFilter = Builders<T>.Filter.Nin(field, filterValues);
                                break;
                            //case FilterOperation.StringIn:
                            //    myFilter = Builders<T>.Filter.StringIn(field, filter.Value);
                            //    break;
                            //case FilterOperation.StringNin:
                            //    myFilter = Builders<T>.Filter.StringNin(field, filter.Value);
                            //    break;
                            //case FilterOperation.Text:
                            //    myFilter = Builders<T>.Filter.Text(field, filter.Value);
                            //    break;
                            default:
                                myFilter = Builders<T>.Filter.Eq(field, filterValues[0]);
                                break;
                        }
                    }
                }
            }

            return myFilter;
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
