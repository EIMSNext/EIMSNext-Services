namespace EIMSNext.ApiService
{
    internal interface ISortItem
    {
        string Id { get; }
        int SortValue { get; set; }
    }

    internal static class SortHelper
    {
        public static T? FindSibling<T>(IEnumerable<T> siblings, string? id) where T : ISortItem
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return default;
            }

            return siblings.FirstOrDefault(x => x.Id == id);
        }

        public static int? CalculateSortValue(int? previous, int? next)
        {
            if (!previous.HasValue && !next.HasValue)
            {
                return 100;
            }

            if (!previous.HasValue)
            {
                return next!.Value > 1 ? next.Value / 2 : null;
            }

            if (!next.HasValue)
            {
                return previous.Value + 100;
            }

            var diff = next.Value - previous.Value;
            return diff > 1 ? previous.Value + diff / 2 : null;
        }

        public static List<T> NormalizeWithMoving<T>(List<T> siblings, T moving, string? previousId, string? nextId) where T : ISortItem
        {
            var normalized = siblings.ToList();
            var insertIndex = normalized.Count;
            if (!string.IsNullOrWhiteSpace(previousId))
            {
                var previousIndex = normalized.FindIndex(x => x.Id == previousId);
                if (previousIndex >= 0)
                {
                    insertIndex = previousIndex + 1;
                }
            }
            else if (!string.IsNullOrWhiteSpace(nextId))
            {
                var nextIndex = normalized.FindIndex(x => x.Id == nextId);
                if (nextIndex >= 0)
                {
                    insertIndex = nextIndex;
                }
            }

            normalized.Insert(insertIndex, moving);
            for (var i = 0; i < normalized.Count; i++)
            {
                normalized[i].SortValue = (i + 1) * 100;
            }

            return normalized;
        }
    }
}
