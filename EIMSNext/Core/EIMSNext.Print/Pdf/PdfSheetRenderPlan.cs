namespace EIMSNext.Print.Pdf
{
    internal sealed class PdfSheetRenderPlan
    {
        public IReadOnlyList<int> VisibleRows { get; init; } = Array.Empty<int>();
        public IReadOnlyList<int> VisibleColumns { get; init; } = Array.Empty<int>();
        public IReadOnlyDictionary<int, int> RowMap { get; init; } = new Dictionary<int, int>();
        public IReadOnlyDictionary<int, int> ColumnMap { get; init; } = new Dictionary<int, int>();
        public IReadOnlyDictionary<int, double> RowHeightsCm { get; init; } = new Dictionary<int, double>();
        public IReadOnlyDictionary<int, double> ColumnWidthsCm { get; init; } = new Dictionary<int, double>();
        public IReadOnlyDictionary<(int Row, int Column), PdfMergeCellPlan> MergeCells { get; init; } = new Dictionary<(int Row, int Column), PdfMergeCellPlan>();
        public double ScaleFactor { get; init; } = 1.0;

        public bool TryGetVisibleRowIndex(int sourceRowIndex, out int visibleRowIndex) => RowMap.TryGetValue(sourceRowIndex, out visibleRowIndex);

        public bool TryGetVisibleColumnIndex(int sourceColumnIndex, out int visibleColumnIndex) => ColumnMap.TryGetValue(sourceColumnIndex, out visibleColumnIndex);

        public bool IsVisibleRow(int rowIndex) => RowMap.ContainsKey(rowIndex);

        public bool IsVisibleColumn(int columnIndex) => ColumnMap.ContainsKey(columnIndex);
    }

    internal sealed class PdfMergeCellPlan
    {
        public bool IsMasterCell { get; init; }
        public bool IsCoveredCell { get; init; }
        public int MergeRight { get; init; }
        public int MergeDown { get; init; }
    }
}
