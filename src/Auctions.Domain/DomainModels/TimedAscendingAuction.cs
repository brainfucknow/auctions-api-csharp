using Wallymathieu.Auctions.Verification;

namespace Wallymathieu.Auctions.DomainModels;

/// <summary>
///     The responsibility of this class is to handle the domain model of "timed ascending" auction model.
/// </summary>
/// <remarks>
///     You can read more about this style of auction model on Wikipedia on the page about <a href="https://en.wikipedia.org/wiki/English_auction">English
///     auction</a>.
/// </remarks>
public class TimedAscendingAuction : Auction, IState
{
    public TimedAscendingAuction()
    {
        AuctionType = AuctionType.TimedAscendingAuction;
    }

    public TimedAscendingOptions Options { get; init; } = new();

    public DateTimeOffset? EndsAt { get; set; }

    public override bool TryAddBid(DateTimeOffset time, Bid bid, out Errors errors)
    {
        ArgumentNullException.ThrowIfNull(bid);
        var state = GetState(time);
        switch (state)
        {
            case State.OnGoing:
            {
                errors = bid.Validate(this);

                if (Bids.Count != 0)
                {
                    var maxBid = Bids.Max(b => b.Amount);
                    var raiseErrors = ValidateRaise(bid.Amount, maxBid, Options.MinRaise);
                    if (raiseErrors != Errors.None)
                    {
                        errors |= raiseErrors;
                        return false;
                    }
                }

                if (errors != Errors.None) return false;

                EndsAt = new[] { EndsAt, Expiry, time + Options.TimeFrame }.Where(v => v != null).Max();
                Bids.Add(new BidEntity(0, bid));
                return true;
            }
            case State.HasEnded:
            {
                errors = Errors.AuctionHasEnded;
                return false;
            }
            case State.AwaitingStart:
            {
                errors = Errors.AuctionHasNotStarted;
                return false;
            }
            default:
                throw new InvalidDataException(state.ToString());
        }
    }

    public override IEnumerable<Bid> GetBids(DateTimeOffset time)
    {
        switch (GetState(time))
        {
            case State.OnGoing:
            case State.HasEnded: return Bids.Select(b => b.ToBid());
        }

        return Array.Empty<Bid>();
    }

    public override (long Amount, UserId Winner)? TryGetAmountAndWinner(DateTimeOffset time)
    {
        switch (GetState(time))
        {
            case State.HasEnded:
            {
                var winningBid = Bids.MaxBy(b => b.Amount);
                return winningBid?.Amount >= Options.ReservePrice
                    ? (winningBid.Amount, winningBid.User)
                    : null;
            }
            case State.AwaitingStart:
            case State.OnGoing:
            default: return null;
        }
    }

    public override bool HasEnded(DateTimeOffset time)
    {
        return GetState(time) switch
        {
            State.HasEnded => true,
            _ => false
        };
    }

    /// <summary>
    ///     Pure core of the English-auction raise policy: a new bid is accepted exactly when it is strictly
    ///     above the highest standing bid and raises it by at least <paramref name="minRaise" />.
    ///     Verified in <c>verification/Dafny/TimedAscending.dfy</c>.
    /// </summary>
    /// <remarks>
    ///     The no-overflow precondition scopes the verified contract to inputs where the C# `long` addition
    ///     agrees with Dafny's unbounded integers; without it, `highestBid + minRaise` wrapping around
    ///     <see cref="long.MaxValue" /> would let an absurdly high bid be rejected as too low — or worse,
    ///     let the verifier certify a property the wrapped arithmetic does not satisfy.
    /// </remarks>
    [Verify]
    internal static Errors ValidateRaise(long amount, long highestBid, long minRaise)
    {
        Contract.Requires(minRaise >= 0);
        Contract.Requires(highestBid <= long.MaxValue - minRaise);
        Contract.Ensures<Errors>(result =>
            (result == Errors.None) == (amount > highestBid && amount >= highestBid + minRaise));

        if (amount <= highestBid) return Errors.MustPlaceBidOverHighestBid;
        if (amount < highestBid + minRaise) return Errors.MustRaiseWithAtLeast;
        return Errors.None;
    }

    private State GetState(DateTimeOffset time)
    {
        return (time > StartsAt, time < Expiry) switch
        {
            (true, true) => State.OnGoing,
            (true, false) => State.HasEnded,
            (false, _) => State.AwaitingStart
        };
    }

    private enum State
    {
        AwaitingStart,
        OnGoing,
        HasEnded
    }
}