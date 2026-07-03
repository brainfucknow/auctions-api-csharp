namespace Wallymathieu.Auctions.Tools.CSharpToDafny;

/// <summary>An enum discovered in a verified method signature, to be emitted into the Dafny prelude.</summary>
internal sealed record EnumModel(string Name, string FullName, bool IsFlags, IReadOnlyList<(string Member, long Value)> Members);
