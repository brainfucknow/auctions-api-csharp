using System.Text.Json.Serialization;
using Wallymathieu.Auctions.Verification;

namespace Wallymathieu.Auctions.DomainModels;

public record Bid(UserId User, long Amount, DateTimeOffset At)
{
    public Errors Validate(Auction auction)
    {
        ArgumentNullException.ThrowIfNull(auction);
        return Validate(User, auction.User, At, auction.StartsAt, auction.Expiry);
    }

    /// <summary>
    ///     Pure core of bid validation: a bid is valid exactly when the bidder is not the seller and the bid
    ///     was placed within the auction window. Verified in <c>verification/Dafny/BidValidation.dfy</c>.
    /// </summary>
    [Verify]
    public static Errors Validate(UserId bidder, UserId seller, DateTimeOffset at, DateTimeOffset startsAt,
        DateTimeOffset expiry)
    {
        Contract.Ensures<Errors>(result =>
            (result == Errors.None) == (bidder != seller && startsAt <= at && at <= expiry));

        var errors = Errors.None;
        if (bidder == seller) errors |= Errors.SellerCannotPlaceBids;
        if (at < startsAt) errors |= Errors.AuctionHasNotStarted;
        if (at > expiry) errors |= Errors.AuctionHasEnded;
        return errors;
    }
}
/// <summary>
/// Main reason to have a separate class is to make it easier to map in Entity Framework Core. This gives us
/// another implicit dependency on Entity Framework Core.
/// <br />
/// Note that the <see cref="Bid"/> record is the same as the <see cref="BidEntity"/> class bit without the <see cref="Id"/>.
/// </summary>
public class BidEntity
{
#pragma warning disable CS8618 // Note that is used by Entity Framework Core.
    private BidEntity(){}
#pragma warning restore CS8618
    public BidEntity(long id, Bid bid)
    {
        Id = id;
        ArgumentNullException.ThrowIfNull(bid);
        User = bid.User;
        Amount = bid.Amount;
        At = bid.At;
    }
    #pragma warning disable IDE0051 // Note the presence of JsonConstructor, i.e. we intend for this to be used by System.Text.Json.
    [JsonConstructor]
    private BidEntity(long id, UserId user, long amount, DateTimeOffset at)
    #pragma warning restore IDE0051
    {
        Id = id;
        User = user;
        Amount = amount;
        At = at;
    }

    public long Id { get; init; }
    public UserId User{ get; init; }
    public long Amount{ get; init; }
    public DateTimeOffset At{ get; init; }

    public Bid ToBid()
    {
        return new(User, Amount, At);
    }
}