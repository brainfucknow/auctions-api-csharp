using Wallymathieu.Auctions.DomainModels;

namespace Wallymathieu.Auctions.Verification;

/// <summary>
///     Boundary between the domain and the Dafny-compiled verified core (<c>Auctions.Domain.Verified</c>,
///     compiled from <c>src/Auctions.Domain.Verified/Validation.dfy</c>). The Dafny source — machine-checked
///     by the verifier, including absence of arithmetic overflow — is the source of truth; this shim only
///     converts representations at the boundary:
///     times cross as <see cref="DateTimeOffset.UtcTicks" /> (comparing <see cref="DateTimeOffset" /> values
///     compares their UTC instants), and error flags cross as the raw <c>uint</c> that Dafny's <c>bv32</c>
///     compiles to, whose constants mirror the <see cref="Errors" /> member values.
/// </summary>
internal static class VerifiedCore
{
    public static Errors ValidateBid(UserId bidder, UserId seller, DateTimeOffset at, DateTimeOffset startsAt,
        DateTimeOffset expiry)
    {
        return (Errors)AuctionValidation.__default.ValidateBid(bidder, seller,
            at.UtcTicks, startsAt.UtcTicks, expiry.UtcTicks);
    }

    public static Errors ValidateRaise(long amount, long highestBid, long minRaise)
    {
        return (Errors)AuctionValidation.__default.ValidateRaise(amount, highestBid, minRaise);
    }
}
