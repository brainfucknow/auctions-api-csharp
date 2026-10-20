using System.Diagnostics.CodeAnalysis;

namespace Wallymathieu.Auctions.DomainModels;

/// <summary>
///     The responsibility of this class is to handle the domain model of "single sealed bid" auction model.
/// </summary>
/// <remarks>
///     Single sealed bid auction is a type of auction where the bidders are not aware of the other bids. The bids are
///     disclosed at the end of the auction.
///     You can read more about the different types of blind auctions on Wikipedia:
///     <br />
///     - <a href="https://en.wikipedia.org/wiki/First-price_sealed-bid_auction">First price sealed bid auction</a> or a
///     <br />
///     - <a href="https://en.wikipedia.org/wiki/Vickrey_auction">Vickrey auction</a>
/// </remarks>
public class SingleSealedBidAuction : Auction, IState
{
    public SingleSealedBidAuction()
    {
        AuctionType = AuctionType.SingleSealedBidAuction;
    }

    public SingleSealedBidOptions Options { get; init; }

    public override bool TryAddBid(DateTimeOffset time, Bid bid, [NotNullWhen(true)] out Errors errors)
    {
        ArgumentNullException.ThrowIfNull(bid);
        var state = GetState(time);
        switch (state)
        {
            case State.AcceptingBids:
                return TryAcceptBid(bid, out errors);
            case State.DisclosingBids:
                errors = Errors.AuctionHasEnded;
                return false;
            case State.AwaitingStart:
                errors = Errors.AuctionHasNotStarted;
                return false;
            default:
                throw new InvalidDataException(state.ToString());
        }
    }

    private bool TryAcceptBid(Bid bid, out Errors errors)
    {
        errors = bid.Validate(this);
        if (Bids.Any(existingBid => existingBid.User == bid.User))
            errors |= Errors.AlreadyPlacedBid;

        if (errors != Errors.None) return false;

        Bids.Add(new BidEntity(0, bid));
        return true;
    }


    public override IEnumerable<Bid> GetBids(DateTimeOffset time)
    {
        return GetState(time) switch
        {
            State.AcceptingBids or State.DisclosingBids => Bids.Select(b => b.ToBid()),
            _ => []
        };
    }

    public override (long Amount, UserId Winner)? TryGetAmountAndWinner(DateTimeOffset time)
    {
        return GetState(time) switch
        {
            State.DisclosingBids => TryGetDisclosedWinner(),
            _ => null
        };
    }

    private (long Amount, UserId Winner)? TryGetDisclosedWinner() => Options switch
    {
        SingleSealedBidOptions.Blind => TryGetBlindWinner(),
        SingleSealedBidOptions.Vickrey => TryGetVickreyWinner(),
        _ => null
    };

    private (long Amount, UserId Winner)? TryGetBlindWinner()
    {
        var winningBid = Bids.MaxBy(bid => bid.Amount);
        return winningBid is null ? null : (winningBid.Amount, winningBid.User);
    }

    private (long Amount, UserId Winner)? TryGetVickreyWinner()
    {
        if (Bids.Count == 0) return null;

        var bids = Bids.OrderByDescending(bid => bid.Amount).Take(2).ToArray();
        return bids.Length == 1
            ? (bids[0].Amount, bids[0].User)
            : (bids[1].Amount, bids[0].User);
    }

    public override bool HasEnded(DateTimeOffset time)
    {
        return GetState(time) switch
        {
            State.DisclosingBids => true,
            _ => false
        };
    }

    private State GetState(DateTimeOffset time)
    {
        return (time > StartsAt, time < Expiry) switch
        {
            (true, true) => State.AcceptingBids,
            (true, false) => State.DisclosingBids,
            (false, _) => State.AwaitingStart
        };
    }

    private enum State
    {
        AwaitingStart,
        AcceptingBids,
        DisclosingBids
    }
}