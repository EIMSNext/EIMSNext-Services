using System.Text.Json;
using System.Text.Json.Nodes;
using EIMSNext.Print.Common;

namespace EIMSNext.Print.Test
{
    [TestClass]
    public sealed class PdfPrintTest
    {
        [TestMethod]
        public void PrintTest()
        {
            var template = new PrintTemplate();
            var option = new PrintOption();

            template.Content = "{\"id\":\"Sheet1\",\"sheetOrder\":[\"RfAziJwRfuVR8hXseP1Dm\"],\"name\":\"Sheet1\",\"appVersion\":\"0.8.3\",\"locale\":\"zhCN\",\"styles\":{\"SQ2g_2\":{\"bd\":{\"r\":{\"s\":1,\"cl\":{\"rgb\":\"#000000\"}},\"t\":{\"s\":1,\"cl\":{\"rgb\":\"#000000\"}},\"b\":{\"s\":1,\"cl\":{\"rgb\":\"#000000\"}},\"l\":{\"s\":1,\"cl\":{\"rgb\":\"#000000\"}}}},\"R6EBq0\":{\"bl\":1},\"Ju8N0l\":{\"ul\":{\"s\":1}},\"9UydFM\":{\"bl\":1,\"it\":1},\"4e2bN0\":{\"ul\":{\"s\":1,\"cl\":{\"rgb\":\"#f05252\"}},\"cl\":{\"rgb\":\"#f05252\"}},\"zMRzNC\":{\"ul\":{\"s\":1,\"cl\":{\"rgb\":\"#f05252\"}},\"cl\":{\"rgb\":\"#f05252\"},\"ht\":3},\"Iq3Z1m\":{\"bd\":{\"r\":{\"s\":1,\"cl\":{\"rgb\":\"#000000\"}},\"t\":{\"s\":1,\"cl\":{\"rgb\":\"#000000\"}},\"b\":{\"s\":1,\"cl\":{\"rgb\":\"#000000\"}},\"l\":{\"s\":1,\"cl\":{\"rgb\":\"#000000\"}}},\"ht\":3},\"Xebjym\":{\"fs\":16},\"WYAUdI\":{\"fs\":16,\"vt\":2}},\"sheets\":{\"RfAziJwRfuVR8hXseP1Dm\":{\"id\":\"RfAziJwRfuVR8hXseP1Dm\",\"name\":\"Sheet1\",\"tabColor\":\"\",\"hidden\":0,\"rowCount\":1001,\"columnCount\":20,\"zoomRatio\":1,\"freeze\":{\"xSplit\":0,\"ySplit\":0,\"startRow\":-1,\"startColumn\":-1},\"scrollTop\":0,\"scrollLeft\":0,\"defaultColumnWidth\":88,\"defaultRowHeight\":24,\"mergeData\":[],\"cellData\":{\"0\":{\"1\":{\"v\":\"测试\",\"t\":1,\"s\":\"WYAUdI\"}},\"1\":{\"1\":{\"v\":\"${单行文本}\",\"t\":1,\"custom\":{\"dataType\":\"field\",\"id\":\"jpnyf0qrrjtrrtuu\"},\"s\":\"9UydFM\"},\"3\":{\"v\":\"${数字}\",\"t\":1,\"custom\":{\"dataType\":\"field\",\"id\":\"jn7ev91nbiigjazy\"},\"s\":\"zMRzNC\"}},\"2\":{},\"3\":{\"1\":{\"v\":\"子表单\",\"t\":1}},\"4\":{\"1\":{\"v\":\"${单行文本}\",\"t\":1,\"custom\":{\"dataType\":\"field\",\"id\":\"jwldykoactu0avqu>j9aolm8kagxmafqr\"},\"s\":\"SQ2g_2\"},\"2\":{\"s\":\"SQ2g_2\"},\"3\":{\"v\":\"${数字}\",\"t\":1,\"custom\":{\"dataType\":\"field\",\"id\":\"jwldykoactu0avqu>jawcz0v912ij7o0k\"},\"s\":\"Iq3Z1m\"}}},\"rowData\":{\"0\":{\"ah\":26,\"h\":49,\"ia\":0},\"2\":{\"h\":24,\"hd\":0}},\"columnData\":{},\"showGridlines\":1,\"rowHeader\":{\"width\":46,\"hidden\":0},\"columnHeader\":{\"height\":20,\"hidden\":0},\"rightToLeft\":0}},\"resources\":[{\"name\":\"SHEET_RANGE_PROTECTION_PLUGIN\",\"data\":\"\"},{\"name\":\"SHEET_AuthzIoMockService_PLUGIN\",\"data\":\"{}\"},{\"name\":\"SHEET_WORKSHEET_PROTECTION_PLUGIN\",\"data\":\"{}\"},{\"name\":\"SHEET_WORKSHEET_PROTECTION_POINT_PLUGIN\",\"data\":\"{}\"},{\"name\":\"SHEET_DEFINED_NAME_PLUGIN\",\"data\":\"{}\"},{\"name\":\"SHEET_RANGE_THEME_MODEL_PLUGIN\",\"data\":\"{}\"}]}";

            var data = "{\"createTime\":\"2026-03-15\", \"jpnyf0qrrjtrrtuu\":\"111\",\"jn7ev91nbiigjazy\":222,\"jwldykoactu0avqu\":[{\"j9aolm8kagxmafqr\":\"333\",\"jawcz0v912ij7o0k\":444},{\"j9aolm8kagxmafqr\":\"555\",\"jawcz0v912ij7o0k\":666}]}";
            var datas = new List<object>();

            for (var i = 0; i < 1; i++)
            {
                datas.Add(JsonNode.Parse(data)!);
            }

            Pdf.PdfGenerator pdfGenerator = new Pdf.PdfGenerator();
            var bytes = pdfGenerator.Print(template, option, datas);

            var fileName = $"D:/Temp/{Guid.NewGuid()}.pdf";
            File.WriteAllBytes(fileName, bytes);
        }
    }
}
