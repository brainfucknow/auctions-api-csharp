namespace Wallymathieu.Auctions.Tools.CSharpToDafny;

/// <summary>Everything extracted from one <c>[Verify]</c>-annotated C# method.</summary>
internal sealed record VerifiedMethod(
    string ContainingNamespace,
    string ContainingType,
    string Name,
    string SourcePath,
    int SourceLine,
    IReadOnlyList<DafnyParameter> Parameters,
    string ReturnDafnyType,
    string ResultName,
    IReadOnlyList<string> RequiresClauses,
    IReadOnlyList<string> EnsuresClauses,
    IReadOnlyList<string> UntranslatedClauses,
    string OriginalBody);
