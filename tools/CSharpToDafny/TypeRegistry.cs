namespace Wallymathieu.Auctions.Tools.CSharpToDafny;

/// <summary>Shared type information collected while extracting, emitted once into <c>Types.dfy</c>.</summary>
internal sealed class TypeRegistry
{
    public bool UsesTime { get; set; }
    public SortedDictionary<string, string> OpaqueTypes { get; } = new(StringComparer.Ordinal);
    public SortedDictionary<string, EnumModel> Enums { get; } = new(StringComparer.Ordinal);
}
