namespace Wallymathieu.Auctions.Verification;

/// <summary>Thrown when a <see cref="Contract.Requires" /> fails and <c>CONTRACTS_FULL</c> is defined.</summary>
public class ContractViolationException : Exception
{
    public ContractViolationException(string message) : base(message)
    {
    }

    public ContractViolationException()
    {
    }

    public ContractViolationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
