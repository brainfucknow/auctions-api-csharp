namespace Wallymathieu.Auctions.Verification;

/// <summary>
///     Marks a method as a candidate for formal verification.
///     <br />
///     Methods carrying this attribute are picked up by the <c>tools/CSharpToDafny</c> Roslyn tool, which
///     extracts their signatures and <see cref="Contract" /> clauses and generates Dafny skeletons under
///     <c>verification/Generated</c>. The attribute has no runtime behaviour.
/// </summary>
/// <remarks>
///     Only mark methods that are pure and deterministic (no I/O, no shared mutable state). Static methods
///     over primitive or simple domain types translate best. See <c>verification/README.md</c>.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class VerifyAttribute : Attribute
{
}
