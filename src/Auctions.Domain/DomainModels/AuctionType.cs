namespace Wallymathieu.Auctions.DomainModels;

/// <summary>
/// Type of auction. Discriminator used by Entity Framework Core.
/// </summary>
public enum AuctionType
{
    /// <summary>
    /// Single sealed bid auction.
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
    SingleSealedBidAuction,

    /// <summary>
    /// Timed ascending auction. Also known as an English auction.
    /// </summary>
    /// <remarks>
    ///     You can read more about this style of auction model on Wikipedia on the page about <a href="https://en.wikipedia.org/wiki/English_auction">English
    ///     auction</a>.
    /// </remarks>
    TimedAscendingAuction,
}