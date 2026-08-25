using System.Text.Json;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

var sourceRoot = GetArgument("--source") ?? throw new ArgumentException("--source is required");
var output = GetArgument("--output") ?? throw new ArgumentException("--output is required");
var modelNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    "AppRequest", "FormDefRequest", "EmployeeRequest", "DepartmentRequest", "RoleRequest", "RoleGroupRequest", "DynamicFindOptions", "DynamicFilter", "DynamicField", "DynamicSort", "DataScope", "SortItem", "DashboardAggregateRequest", "FormData", "FormDataFilterOptionsRequest", "AppDef", "FormDef", "Employee", "Department", "Role", "RoleGroup", "Wf_Task", "Wf_TaskLog", "WfTaskViewModel", "BriefField"
};
var ignored = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    "UpdateBy", "UpdateTime", "DeleteFlag", "CorpId", "IsSystem", "IsAnonymous", "IsDummy", "Invite", "PublicRelatedFormIds",
    "OldData", "NewData", "DataFilter", "UpdateExp", "ClientSecrets", "ApiKey", "RequireClientSecret", "AccessTokenLifetime", "IdentityTokenLifetime", "AllowedGrantTypes", "AllowedScopes"
};
var trees = Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
    .Where(path => !path.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                && !path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                && !path.Contains(Path.DirectorySeparatorChar + "Tests" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
    .Select(path => (Path: path, Tree: CSharpSyntaxTree.ParseText(File.ReadAllText(path), path: path)))
    .ToList();

foreach (var (_, tree) in trees)
{
    foreach (var invocation in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax { Name.Identifier.Text: "Ignore" } access) continue;
        if (invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression is SimpleLambdaExpressionSyntax lambda
            && lambda.Body is MemberAccessExpressionSyntax member)
            ignored.Add(member.Name.Identifier.Text);
    }
}

var declarations = trees.SelectMany(item => item.Tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>())
    .GroupBy(item => item.Identifier.Text, StringComparer.OrdinalIgnoreCase)
    .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
var models = new Dictionary<string, Model>(StringComparer.OrdinalIgnoreCase);
var missing = new List<string>();
foreach (var modelName in modelNames)
{
    if (!declarations.TryGetValue(modelName, out var declaration)) continue;
    var fields = CollectFields(declaration, declarations, new HashSet<string>(StringComparer.OrdinalIgnoreCase))
            .Where(property => property.Modifiers.Any(SyntaxKind.PublicKeyword))
            .Where(property => !property.Modifiers.Any(SyntaxKind.StaticKeyword))
            .Where(property => !IsIgnored(property, ignored))
            .Select(property =>
            {
                var description = ReadSummary(property.GetLeadingTrivia().ToFullString());
                if (string.IsNullOrWhiteSpace(description)) missing.Add($"{declaration.Identifier.Text}.{property.Identifier.Text}");
                return new Field(JsonName(property), property.Type.ToString(), description ?? "待补充字段说明", IsRequired(property));
            }).ToList();
    models[declaration.Identifier.Text] = new Model(fields);
}
if (missing.Count > 0)
{
    Console.Error.WriteLine("Missing XML summaries:");
    foreach (var item in missing) Console.Error.WriteLine($"  {item}");
    return 2;
}

Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
var json = JsonSerializer.Serialize(models, new JsonSerializerOptions
{
    WriteIndented = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
});
File.WriteAllText(output, "window.EIMS_API_MODELS = " + json + ";\n");
Console.WriteLine($"Generated {models.Count} public API models to {output}");
return 0;

static bool IsIgnored(PropertyDeclarationSyntax property, ISet<string> ignored)
    => ignored.Contains(property.Identifier.Text) || property.AttributeLists.SelectMany(x => x.Attributes)
        .Any(attribute => Regex.IsMatch(attribute.Name.ToString(), "^(JsonIgnore|IgnoreDataMember)(Attribute)?$", RegexOptions.IgnoreCase));

static bool IsRequired(PropertyDeclarationSyntax property)
    => property.Modifiers.Any(SyntaxKind.RequiredKeyword) || property.AttributeLists.SelectMany(x => x.Attributes).Any(attribute =>
        attribute.Name.ToString().Equals("Required", StringComparison.OrdinalIgnoreCase) ||
        attribute.Name.ToString().EndsWith("RequiredAttribute", StringComparison.OrdinalIgnoreCase));

static IEnumerable<PropertyDeclarationSyntax> CollectFields(ClassDeclarationSyntax declaration, IReadOnlyDictionary<string, ClassDeclarationSyntax> declarations, ISet<string> visited)
{
    if (!visited.Add(declaration.Identifier.Text)) yield break;
    if (declaration.BaseList?.Types.FirstOrDefault()?.Type is IdentifierNameSyntax baseName
        && declarations.TryGetValue(baseName.Identifier.Text, out var baseDeclaration))
    {
        foreach (var property in CollectFields(baseDeclaration, declarations, visited)) yield return property;
    }
    foreach (var property in declaration.Members.OfType<PropertyDeclarationSyntax>()) yield return property;
}

static string JsonName(PropertyDeclarationSyntax property)
{
    var attribute = property.AttributeLists.SelectMany(x => x.Attributes).FirstOrDefault(x => x.Name.ToString().StartsWith("JsonPropertyName", StringComparison.Ordinal));
    if (attribute?.ArgumentList?.Arguments.FirstOrDefault()?.Expression is LiteralExpressionSyntax literal) return literal.Token.ValueText;
    var name = property.Identifier.Text;
    return name.Length == 0 ? name : char.ToLowerInvariant(name[0]) + name[1..];
}

static string? ReadSummary(string trivia)
{
    var normalized = Regex.Replace(trivia, @"^\s*/// ?", string.Empty, RegexOptions.Multiline);
    var summary = Regex.Match(normalized, @"<summary>\s*(.*?)\s*</summary>", RegexOptions.Singleline);
    if (summary.Success) return Regex.Replace(summary.Groups[1].Value, @"<.*?>", string.Empty).Trim();
    var matches = Regex.Matches(normalized, @"^\s*(?!<)(.*)$", RegexOptions.Multiline);
    var lines = matches.Select(match => match.Groups[1].Value.Trim()).Where(line => line.Length > 0 && !line.StartsWith("<", StringComparison.Ordinal)).ToList();
    return lines.Count == 0 ? null : string.Join(" ", lines);
}

static string? GetArgument(string name)
{
    var index = Array.IndexOf(Environment.GetCommandLineArgs(), name);
    return index >= 0 && index + 1 < Environment.GetCommandLineArgs().Length ? Environment.GetCommandLineArgs()[index + 1] : null;
}

record Model(List<Field> Fields);
record Field(string Name, string Type, string Description, bool Required);
