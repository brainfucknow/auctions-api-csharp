using Wallymathieu.Auctions.DomainModels;

namespace Wallymathieu.Auctions.Tests;

public class VickreyAuctionStateSpec
{
    private static SingleSealedBidAuction GetState() => WithBids(VickreyAuction);

    [Fact]
    public void bid_after_auction_has_ended()
    {
        Assert.Equal(Errors.AuctionHasEnded,
            !GetState().TryAddBid(EndsAt, BidOf100, out var err) ? err : default);
    }

    [Fact]
    public void bid_before_auction_has_started()
    {
        var auction = VickreyAuction;

        Assert.False(auction.TryAddBid(StartsAt, BidOf100 with { At = StartsAt }, out var error));
        Assert.Equal(Errors.AuctionHasNotStarted, error);
    }

    [Fact]
    public void valid_bid_is_available_once_the_auction_starts()
    {
        var auction = VickreyAuction;
        var at = StartsAt.AddMinutes(1);
        var bid = BidOf100 with { At = at };

        Assert.True(auction.TryAddBid(at, bid, out var error));
        Assert.Equal(Errors.None, error);
        Assert.Empty(auction.GetBids(StartsAt));
        Assert.Equal([bid], auction.GetBids(at));
        Assert.Equal([bid], auction.GetBids(EndsAt));
    }

    [Fact]
    public void vickrey_auction_winner_and_price()
    {
        var maybeAmountAndWinner = GetState().TryGetAmountAndWinner(EndsAt);
        Assert.Equal((Bid1.Amount, Bid2.User), maybeAmountAndWinner);
    }

    [Fact]
    public void vickrey_auction_with_one_bid_charges_that_bid()
    {
        var auction = VickreyAuction;
        Assert.True(auction.TryAddBid(Bid1.At, Bid1, out _));

        Assert.Equal((Bid1.Amount, Bid1.User), auction.TryGetAmountAndWinner(EndsAt));
    }

    [Fact]
    public void blind_auction_charges_the_winning_bid()
    {
        var auction = WithOptions(SingleSealedBidOptions.Blind);
        WithBids(auction);

        Assert.Equal((Bid2.Amount, Bid2.User), auction.TryGetAmountAndWinner(EndsAt));
    }

    [Fact]
    public void winner_is_not_available_before_disclosure_or_without_bids()
    {
        var auction = VickreyAuction;

        Assert.Null(auction.TryGetAmountAndWinner(StartsAt));
        Assert.Null(auction.TryGetAmountAndWinner(StartsAt.AddMinutes(1)));
        Assert.Null(auction.TryGetAmountAndWinner(EndsAt));
    }

    [Fact]
    public void auction_ends_at_its_expiry()
    {
        var auction = VickreyAuction;

        Assert.False(auction.HasEnded(EndsAt.AddTicks(-1)));
        Assert.True(auction.HasEnded(EndsAt));
    }

    [Fact]
    public void vickrey_auction_Cant_place_bid_two_bids()
    {
        var auction = VickreyAuction;
        var at = StartsAt.AddHours(1);
        Assert.True(auction.TryAddBid(at, BidOf100 with { At = at }, out _));
        at = StartsAt.AddHours(2);
        Assert.False(auction.TryAddBid(at, BidOf200 with { At = at }, out var err));
        Assert.Equal(Errors.AlreadyPlacedBid, err);
    }

    private static SingleSealedBidAuction WithOptions(SingleSealedBidOptions options) =>
        new()
        {
            AuctionId = TestData.AuctionId,
            Title = Title,
            StartsAt = StartsAt,
            Expiry = EndsAt,
            User = Seller,
            Currency = CurrencyCode.SEK,
            Options = options
        };
}