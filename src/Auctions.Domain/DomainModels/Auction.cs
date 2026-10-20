using System.Text.Json.Serialization;
using Wallymathieu.Auctions.Commands;
using Wallymathieu.Auctions.DomainModels.Bids;
using Wallymathieu.Auctions.Services;

namespace Wallymathieu.Auctions.DomainModels;

/// <summary>
/// Base class for all auctions. Note that we are using an abstract class to allow for polymorphism.
/// <br />
/// Abstract classes is a low level way to share code and is not recommended for most cases. This is an example of
/// white box reuse. This means that all derived classes are aware of the implementation details of the base class.
/// Generally you should avoid abstract classes if possible and <a href="https://en.wikipedia.org/wiki/Composition_over_inheritance">prefer composition</a>.
/// </summary>
[JsonPolymorphic(UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToBaseType,
     TypeDiscriminatorPropertyName = "$type"),
 JsonDerivedType(typeof(SingleSealedBidAuction), typeDiscriminator: nameof(SingleSealedBidAuction)),
 JsonDerivedType(typeof(TimedAscendingAuction), typeDiscriminator: nameof(TimedAscendingAuction))]
public abstract class Auction : IState
{
#pragma warning disable CS8618 // Note that is used by Entity Framework Core.
    protected Auction()
#pragma warning restore CS8618
    {
    }
    /// <summary>
    /// Raw list of bids. This is the storage of the bids, should not be used directly.
    /// </summary>
#pragma warning disable CA1721 // These are expected to be used internally by auction implementations.
    protected ICollection<BidEntity> Bids { get; init; } = [];
#pragma warning restore CA1721

    public AuctionId AuctionId { get; set; }

    public DateTimeOffset StartsAt { get; init; }
    public string Title { get; init; }

    ///<summary> initial expiry </summary>
    public DateTimeOffset Expiry { get; init; }

    public UserId User { get; init; }
    public CurrencyCode Currency { get; init; }
    public AuctionType AuctionType { get; init; }

    public bool OpenBidders { get; init; }
#pragma warning disable CA1819 // EF Core row-version properties must be byte arrays.
    public byte[] Version { get; set; } = [];
#pragma warning restore CA1819

    /// <summary>
    /// Create either a SingleSealedBidAuction or a TimedAscendingAuction based on the command.
    /// </summary>
    public static Auction Create(CreateAuctionCommand cmd, IUserContext userContext)
    {
        ArgumentNullException.ThrowIfNull(cmd);
        ArgumentNullException.ThrowIfNull(userContext);
        if (userContext.UserId == null)
            throw new InvalidOperationException("User must be logged in to create an auction.");
        return cmd.SingleSealedBidOptions != null
            ? CreateSingleSealedBidAuction(cmd, userContext)
            : CreateTimedAscendingAuction(cmd, userContext);

        static SingleSealedBidAuction CreateSingleSealedBidAuction(CreateAuctionCommand cmd, IUserContext userContext)
        {
            return new SingleSealedBidAuction
            {
                Currency = cmd.Currency,
                Expiry = cmd.EndsAt,
                StartsAt = cmd.StartsAt,
                Title = cmd.Title,
                User = userContext.UserId!,
                Options = cmd.SingleSealedBidOptions!.Value,
                OpenBidders = cmd.Open,
            };
        }

        static TimedAscendingAuction CreateTimedAscendingAuction(CreateAuctionCommand cmd, IUserContext userContext)
        {
            return new TimedAscendingAuction
            {
                Currency = cmd.Currency,
                Expiry = cmd.EndsAt,
                StartsAt = cmd.StartsAt,
                Title = cmd.Title,
                User = userContext.UserId!,
                Options = cmd.TimedAscendingOptions!,
                OpenBidders = cmd.Open,
            };
        }
    }

    public Result<Bid, Errors> TryAddBid(CreateBidCommand model, IUserContext userContext, ISystemClock systemClock)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(userContext);
        ArgumentNullException.ThrowIfNull(systemClock);
        if (userContext.UserId == null)
            throw new InvalidOperationException("User must be logged in to place a bid.");
        var bid = new Bid(userContext.UserId, model.Amount, systemClock.Now);
        if (TryAddBid(systemClock.Now, bid, out var error))
        {
            return Result.Ok<Bid, Errors>(bid);
        }
        else
        {
            return Result.Error<Bid, Errors>(error);
        }
    }

    public abstract bool TryAddBid(DateTimeOffset time, Bid bid, out Errors errors);
    public abstract IEnumerable<Bid> GetBids(DateTimeOffset time);
    public abstract (long Amount, UserId Winner)? TryGetAmountAndWinner(DateTimeOffset time);
    public abstract bool HasEnded(DateTimeOffset time);

    public IBidUserMapper BidUserMapper()
    {
        IBidUserMapper bidUserMapper = OpenBidders
            ? new BidUserMapper()
            : new NumberedBidUserMapper(Bids);
        return bidUserMapper;
    }
}