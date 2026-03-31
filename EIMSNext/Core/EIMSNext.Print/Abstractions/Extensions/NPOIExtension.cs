using NPOI.SS.UserModel;
using NPOI.SS.Util;
using NPOI.Util;
using NPOI.XWPF.UserModel;

namespace EIMSNext.Print.Extensions
{
    internal static class NPOIExtension
    {
        internal static void ClearBodyElements(this XWPFDocument docx)
        {
            var els = docx.BodyElements;
            for (int i = els.Count - 1; i > -1; i--)
            {
                docx.RemoveBodyElement(i);
            }
        }
        internal static void CopyTo(this XWPFTable source, XWPFTable target)
        {
            var sourceCTTbl = source.GetCTTbl();
            var targetCTTbl = target.GetCTTbl();

            target.TableCaption = source.TableCaption;
            target.TableDescription = source.TableDescription;
            target.StyleID = source.StyleID;

            targetCTTbl.tblPr = sourceCTTbl.tblPr?.Copy();
            targetCTTbl.tblGrid = sourceCTTbl.tblGrid?.Copy();

            //此处不复制行，行将会动态填充
            //for (int i = 0; i < source.Rows.Count; i++)
            //{
            //    var sourceRow = source.Rows[i];
            //    var targetRow = target.CreateRow();

            //    targetRow.RemoveCell(0);//删除创建时的默认空单元格
            //    CopyTo(sourceRow, targetRow);
            //}
        }
        internal static void CopyTo(this XWPFTableRow source, XWPFTableRow target)
        {
            var sourceCTR = source.GetCTRow();
            var targetCTR = target.GetCTRow();

            source.IsRepeatHeader = target.IsRepeatHeader;

            targetCTR.rsidR = sourceCTR.rsidR?.ToArray();
            targetCTR.rsidDel = sourceCTR.rsidDel?.ToArray();
            targetCTR.rsidRPr = sourceCTR.rsidRPr?.ToArray();
            targetCTR.rsidTr = sourceCTR.rsidTr?.ToArray();
            targetCTR.trPr = sourceCTR.trPr?.Copy();

            var cells = source.GetTableCells();
            for (int i = 0; i < cells.Count; i++)
            {
                var sourceCell = cells[i];
                var targetCell = target.CreateCell();

                targetCell.RemoveParagraph(0);//删除创建时的默认空段落
                sourceCell.CopyTo(targetCell);
            }

        }
        internal static void CopyTo(this XWPFTableCell source, XWPFTableCell target)
        {
            var sourceCTC = source.GetCTTc();
            var targetCTC = target.GetCTTc();

            targetCTC.tcPr = sourceCTC.tcPr?.Copy();

            for (int i = 0; i < source.Paragraphs.Count; i++)
            {
                var sourcePara = source.Paragraphs[i];
                var targetPara = target.AddParagraph();

                sourcePara.CopyTo(targetPara);
            }
        }
        internal static void CopyTo(this XWPFParagraph source, XWPFParagraph target)
        {
            var sourceCTP = source.GetCTP();
            var targetCTP = target.GetCTP();

            targetCTP.pPr = sourceCTP.pPr?.Copy();
            targetCTP.rsidR = sourceCTP.rsidR?.ToArray();
            targetCTP.rsidRPr = sourceCTP.rsidRPr?.ToArray();
            targetCTP.rsidRDefault = sourceCTP.rsidRDefault?.ToArray();
            targetCTP.rsidP = sourceCTP.rsidP?.ToArray();

            for (int i = 0; i < source.Runs.Count; i++)
            {
                var sourceRun = source.Runs[i];
                var targetRun = target.CreateRun();
                sourceRun.CopyTo(targetRun);
            }
        }
        internal static void CopyTo(this XWPFRun source, XWPFRun target)
        {
            var sourceCTR = source.GetCTR();
            var targetCTR = target.GetCTR();

            target.FontFamily = source.FontFamily;
            target.FontSize = source.FontSize;
            target.SetColor(source.GetColor());
            target.IsBold = source.IsBold;
            target.IsItalic = source.IsItalic;
            target.IsStrikeThrough = source.IsStrikeThrough;
            target.SetStyle(source.GetStyle());
            target.IsDoubleStrikeThrough = source.IsDoubleStrikeThrough;

            targetCTR.rPr = sourceCTR.rPr;
            targetCTR.rsidRPr = sourceCTR.rsidRPr;
            targetCTR.rsidR = sourceCTR.rsidR;
            targetCTR.AddNewT().Value = source.Text;

            foreach (var srcPic in source.GetEmbeddedPictures())
            {
                var pd = srcPic.GetPictureData();
                using (var ms = new MemoryStream(pd.Data))
                {
                    target.AddPicture(ms, pd.GetPictureType(), pd.FileName, (int)srcPic.Width, (int)srcPic.Height);
                }
            }
        }

        /// <summary>
        /// NPOI missing the ShiftedRows height, waiting offical fix.  just rewrite here
        /// </summary>
        /// <param name="sheet"></param>
        /// <param name="sourceRowIndex"></param>
        /// <param name="targetRowIndex"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static IRow CopyRow(ISheet sheet, int sourceRowIndex, int targetRowIndex)
        {
            if (sourceRowIndex == targetRowIndex)
                throw new ArgumentException("sourceIndex and targetIndex cannot be same");
            // Get the source / new row
            IRow newRow = sheet.GetRow(targetRowIndex);
            IRow sourceRow = sheet.GetRow(sourceRowIndex);

            // If the row exist in destination, push down all rows by 1 else create a new row
            if (newRow != null)
            {
                sheet.ShiftRows(targetRowIndex, sheet.LastRowNum, 1, true, false);
            }
            newRow = sheet.CreateRow(targetRowIndex);
            newRow.Height = sourceRow.Height;   //copy row height

            // Loop through source columns to add to new row
            for (int i = sourceRow.FirstCellNum; i < sourceRow.LastCellNum; i++)
            {
                // Grab a copy of the old/new cell
                NPOI.SS.UserModel.ICell oldCell = sourceRow.GetCell(i);

                // If the old cell is null jump to next cell
                if (oldCell == null)
                {
                    continue;
                }
                NPOI.SS.UserModel.ICell newCell = newRow.CreateCell(i);

                if (oldCell.CellStyle != null)
                {
                    // apply style from old cell to new cell 
                    newCell.CellStyle = oldCell.CellStyle;
                }

                // If there is a cell comment, copy
                //if (oldCell.CellComment != null)
                //{
                //    sheet.CopyComment(oldCell, newCell);
                //}

                // If there is a cell hyperlink, copy
                if (oldCell.Hyperlink != null)
                {
                    newCell.Hyperlink = oldCell.Hyperlink;
                }

                // Set the cell data type
                newCell.SetCellType(oldCell.CellType);

                // Set the cell data value
                switch (oldCell.CellType)
                {
                    case CellType.Blank:
                        newCell.SetCellValue(oldCell.StringCellValue);
                        break;
                    case CellType.Boolean:
                        newCell.SetCellValue(oldCell.BooleanCellValue);
                        break;
                    case CellType.Error:
                        newCell.SetCellErrorValue(oldCell.ErrorCellValue);
                        break;
                    case CellType.Formula:
                        newCell.SetCellType(CellType.Formula);
                        newCell.SetCellFormula(oldCell.CellFormula);
                        break;
                    case CellType.Numeric:
                        newCell.SetCellValue(oldCell.NumericCellValue);
                        break;
                    case CellType.String:
                        newCell.SetCellValue(oldCell.RichStringCellValue);
                        break;
                }
            }

            // If there are are any merged regions in the source row, copy to new row
            for (int i = 0; i < sheet.NumMergedRegions; i++)
            {
                CellRangeAddress cellRangeAddress = sheet.GetMergedRegion(i);
                if (cellRangeAddress != null && cellRangeAddress.FirstRow == sourceRow.RowNum)
                {
                    CellRangeAddress newCellRangeAddress = new CellRangeAddress(newRow.RowNum,
                            newRow.RowNum +
                                    (cellRangeAddress.LastRow - cellRangeAddress.FirstRow
                                            ),
                            cellRangeAddress.FirstColumn,
                            cellRangeAddress.LastColumn);
                    sheet.AddMergedRegion(newCellRangeAddress);
                }
            }
            return newRow;
        }
    }
}
