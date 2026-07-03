using System.Diagnostics;

namespace Wallymathieu.Auctions.Verification;

/// <summary>
///     Lightweight design-by-contract vocabulary consumed by the <c>tools/CSharpToDafny</c> extraction tool.
///     <br />
///     Calls to these methods are compiled away unless the <c>CONTRACTS_FULL</c> symbol is defined; they exist
///     so that pre- and postconditions can be written next to the implementation in ordinary C# and then be
///     translated to Dafny <c>requires</c>/<c>ensures</c> clauses. The Dafny verifier — not the runtime — is
///     the authority on whether the implementation satisfies them.
/// </summary>
public static class Contract
{
    /// <summary>
    ///     States a precondition. Translated to a Dafny <c>requires</c> clause.
    ///     The condition must only mention the parameters of the enclosing method.
    /// </summary>
    [Conditional("CONTRACTS_FULL")]
    public static void Requires(bool condition)
    {
        if (!condition) throw new ContractViolationException("Precondition failed");
    }

    /// <summary>
    ///     States a postcondition over the return value. Translated to a Dafny <c>ensures</c> clause where the
    ///     lambda parameter becomes the Dafny out-parameter. The condition must only mention the lambda
    ///     parameter and the parameters of the enclosing method.
    /// </summary>
    [Conditional("CONTRACTS_FULL")]
#pragma warning disable IDE0060, CA1801 // The postcondition is consumed statically by the extraction tool.
    public static void Ensures<T>(Func<T, bool> postcondition)
#pragma warning restore IDE0060, CA1801
    {
        // Postconditions cannot be checked at this point in the method body (the result does not exist yet);
        // they are only meaningful to the verification pipeline.
    }
}
