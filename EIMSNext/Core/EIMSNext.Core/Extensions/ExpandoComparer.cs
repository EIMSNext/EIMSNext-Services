using System.Collections;
using System.Dynamic;

namespace EIMSNext.Core.Extensions
{
    public class ExpandoComparer
    {
        /// <summary>
        /// 比较两个ExpandoObject对象，返回所有变化的属性列表
        /// </summary>
        /// <param name="original">修改前的对象</param>
        /// <param name="modified">修改后的对象</param>
        /// <returns>IList<DataUpdateLog> 变更列表</returns>
        public static IList<ExpandoChangeLog> Compare(ExpandoObject original, ExpandoObject modified)
        {
            var oriDict = original as IDictionary<string, object> ?? new Dictionary<string, object>();
            var modDict = modified as IDictionary<string, object> ?? new Dictionary<string, object>();

            var changeLogs = new List<ExpandoChangeLog>();

            foreach (var kvp in oriDict)
            {
                string fId = kvp.Key;
                object oriValue = kvp.Value;

                // 属性被删除
                if (!modDict.ContainsKey(fId))
                {
                    changeLogs.Add(new ExpandoChangeLog
                    {
                        FieldId = fId,
                        OriValue = oriValue,
                        NewValue = null,
                        ChangeType = DataChangeType.Deleted
                    });
                }
                // 属性值被修改
                else
                {
                    object newValue = modDict[fId];
                    if (!AreValuesEqual(oriValue, newValue))
                    {
                        changeLogs.Add(new ExpandoChangeLog
                        {
                            FieldId = fId,
                            OriValue = oriValue,
                            NewValue = newValue,
                            ChangeType = DataChangeType.Modified
                        });
                    }
                }
            }

            // 2. 检查新增的属性
            foreach (var kvp in modDict)
            {
                string fId = kvp.Key;
                if (!oriDict.ContainsKey(fId))
                {
                    changeLogs.Add(new ExpandoChangeLog
                    {
                        FieldId = fId,
                        OriValue = null,
                        NewValue = kvp.Value,
                        ChangeType = DataChangeType.Added
                    });
                }
            }

            return changeLogs;
        }

        /// <summary>
        /// 深度对比两个值（支持数组、嵌套ExpandoObject、普通值类型）
        /// </summary>
        /// <param name="value1">第一个值</param>
        /// <param name="value2">第二个值</param>
        /// <returns>是否相等</returns>
        private static bool AreValuesEqual(object value1, object value2)
        {
            // 处理null的情况
            if (value1 == null && value2 == null) return true;
            if (value1 == null || value2 == null) return false;

            // 如果是ExpandoObject，递归对比内部属性
            if (value1 is ExpandoObject exp1 && value2 is ExpandoObject exp2)
            {
                var expChanges = Compare(exp1, exp2);
                return !expChanges.Any();
            }

            // 如果是数组/集合（实现IEnumerable），对比元素内容
            if (value1 is IEnumerable enumerable1 && value2 is IEnumerable enumerable2)
            {
                var list1 = enumerable1.Cast<object>().ToList();
                var list2 = enumerable2.Cast<object>().ToList();

                if (list1.Count != list2.Count) return false;

                for (int i = 0; i < list1.Count; i++)
                {
                    if (!AreValuesEqual(list1[i], list2[i]))
                    {
                        return false;
                    }
                }
                return true;
            }

            return Equals(value1, value2);
        }
    }

    public class ExpandoChangeLog
    {
        /// <summary>
        /// 变更的属性名
        /// </summary>
        public required string FieldId { get; set; }

        /// <summary>
        /// 修改前的原值
        /// </summary>
        public object? OriValue { get; set; }

        /// <summary>
        /// 修改后的新值
        /// </summary>
        public object? NewValue { get; set; }

        /// <summary>
        /// 变更类型（Added/Modified/Deleted）
        /// </summary>
        public DataChangeType ChangeType { get; set; }
    }
    public enum DataChangeType
    {
        Added,
        Modified,
        Deleted
    }
}
