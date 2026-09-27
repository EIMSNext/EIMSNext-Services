namespace EIMSNext.Service.Contracts;

public interface IEmployeeAccessSubjectResolver
{
    EmployeeAccessSubjects ResolveCurrent();
}

public sealed record EmployeeAccessSubjects(
    string EmployeeId,
    IReadOnlySet<string> DepartmentIds,
    IReadOnlySet<string> AncestorDepartmentIds,
    IReadOnlySet<string> EmployeeGroupIds)
{
    public static EmployeeAccessSubjects Empty { get; } = new(
        string.Empty,
        new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        new HashSet<string>(StringComparer.OrdinalIgnoreCase));
}
