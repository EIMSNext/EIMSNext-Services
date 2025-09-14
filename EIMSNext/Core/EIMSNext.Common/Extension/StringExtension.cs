using Microsoft.International.Converters.PinYinConverter;

namespace EIMSNext.Common.Extension
{
    public static class StringExtension
    {
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
        /// 获取汉字字符串的拼音首字母
        /// </summary>
        /// <param name="str">字符串</param>
        /// <returns></returns>
        public static string GetPinYinFirst(this string str)
        {
            string result = string.Empty;
            str.ForEach(x => result += GetPinYinFirst(x));
            return result;
        }

        /// <summary>
        ///  获取单个汉字的首拼音
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        private static char GetPinYinFirst(char c)
        {
            return ChineseChar.IsValidChar(c) ? new ChineseChar(c).Pinyins[0][0] : c;
        }

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

        public static string FormatMoney(this decimal money)
        {
            return money.ToString(Constants.Defaut_MoneyFormat);
        }

        public static bool IsFlagsEnum(this Type enumType)
        {
            return enumType.IsEnum && enumType.IsDefined(typeof(FlagsAttribute), false);
        }
    }
}
