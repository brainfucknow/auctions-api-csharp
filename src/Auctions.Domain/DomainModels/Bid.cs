namespace Wallymathieu.Auctions.DomainModels;

public record Bid(UserId User, long Amount, DateTimeOffset At)
{
    public Errors Validate(Auction auction)
    {
        ArgumentNullException.ThrowIfNull(auction);
        var errors = Errors.None;
        if (User == auction.User) errors |= Errors.SellerCannotPlaceBids;
        if (At < auction.StartsAt) errors |= Errors.AuctionHasNotStarted;
        if (At > auction.Expiry) errors |= Errors.AuctionHasEnded;
        return errors;
    }
}