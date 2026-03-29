using System.Text;

namespace EIMSNext.Print.Pdf
{
    public class PdfPrintMeta
    {
        /// <summary>
        /// 字段ID, 
        /// </summary>
        public string? Id { get; set; }

        /// <summary>
        /// 数据类型, 可能为field, barcode, qrcode
        /// </summary>
        public string? DataType { get; set; }

        public bool IsTable()
        {
            return Id != null && Id.Contains('>');
        }

        public string GetTablePath()
        {
            if (IsTable())
            {
                return $"$.{Id!.Split('>')[0]}";
            }

            return string.Empty;
        }

        public string GetValuePath(int[] indexes)
        {
            var path = "";
            if (!string.IsNullOrEmpty(Id))
            {
                if (indexes.Length > 0)
                {
                    var fields = Id.Split('>');
                    var builder = new StringBuilder("$.");
                    for (int i = 0; i < fields.Length; i++)
                    {
                        builder.Append(fields[i]);
                        if (i < indexes.Length)
                        {
                            builder.Append($"[{indexes[i]}]");
                        }

                        if (i < fields.Length - 1)
                            builder.Append('.');
                    }
                    path = builder.ToString();
                }
                else
                    path = $"$.{Id}";
            }

            return path;
        }
    }
}
