using System.Text.Json;

using Microsoft.International.Converters.PinYinConverter;

namespace EIMSNext.Common.Extensions
{
    /// <summary>
    /// 提供 <see cref="string"/> 类型的扩展方法。
    /// </summary>
    public static class StringExtension
    {
        /// <summary>
        /// 遍历集合并对每个元素应用指定转换函数。
        /// </summary>
        /// <typeparam name="TResult">转换后的元素类型。</typeparam>
        /// <param name="source">源集合。</param>
        /// <param name="predicate">每个元素的转换函数。</param>
        /// <returns>转换后的元素集合。</returns>
        public static IEnumerable<TResult> Cast<TResult>(this System.Collections.IEnumerable? source, Func<object, TResult> predicate)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }

            if (predicate == null)
            {
                throw new ArgumentNullException("predicate");
            }

            foreach (var item in source)
            {
                yield return predicate(item);
            }
        }

        /// <summary>
        /// 获取汉字字符串的拼音首字母。
        /// </summary>
        /// <param name="str">字符串。</param>
        /// <returns>由汉字拼音首字母组成的字符串。</returns>
        public static string GetPinYinFirst(this string str)
        {
            string result = string.Empty;
            str.ForEach(x => result += GetPinYinFirst(x));
            return result;
        }

        private static char GetPinYinFirst(char c)
        {
            return ChineseChar.IsValidChar(c) ? new ChineseChar(c).Pinyins[0][0] : c;
        }

        /// <summary>
        /// 对银行卡号进行脱敏处理，保留前四位和后四位。
        /// </summary>
        /// <param name="bankNo">原始银行卡号。</param>
        /// <returns>脱敏后的卡号字符串。</returns>
        public static string FormatBankNo(this string bankNo)
        {
            string start = "";
            string end = "";
            var length = bankNo.Length;
            if (length <= 4)
            {
                start = bankNo;
                end = bankNo;
            }
            else
            {
                start = bankNo.Substring(0, 4);
                end = bankNo.Substring(length - 4);
            }

            return $"{start}***********{end}";
        }

        /// <summary>
        /// 将金额格式化为默认金额格式字符串。
        /// </summary>
        /// <param name="money">金额数值。</param>
        /// <returns>格式化后的金额字符串。</returns>
        public static string FormatMoney(this decimal money)
        {
            return money.ToString(Constants.Defaut_MoneyFormat);
        }

        /// <summary>
        /// 判断枚举类型是否为标志（Flags）枚举。
        /// </summary>
        /// <param name="enumType">需要检查的类型。</param>
        /// <returns>是标志枚举时返回 true，否则返回 false。</returns>
        public static bool IsFlagsEnum(this Type enumType)
        {
            return enumType.IsEnum && enumType.IsDefined(typeof(FlagsAttribute), false);
        }
    }
}