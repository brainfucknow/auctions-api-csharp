// Formal model of the single sealed bid auction, mirroring
//   src/Auctions.Domain/DomainModels/SingleSealedBidAuction.cs
//
// The model is deliberately self-contained (no includes) so that each file can be verified on its own.
//
// Modelling notes:
//  * Time is an integer (DateTimeOffset ticks); amounts are unbounded integers (C# long).
//  * The C# [Flags] Errors enum combined with `|` is modelled as a set of errors; Errors.None is `{}`.
//  * The C# winner selection uses LINQ MaxBy / OrderByDescending, both of which are stable; the model
//    mirrors this by always picking the *first* bid holding the maximum amount.
module SingleSealedBid {

  type Time = int
  type UserId(==)

  datatype Error =
    | SellerCannotPlaceBids
    | AuctionHasNotStarted
    | AuctionHasEnded
    | AlreadyPlacedBid

  datatype Option<T> = None | Some(value: T)

  // Mirrors Wallymathieu.Auctions.DomainModels.SingleSealedBidOptions.
  datatype SealedBidOptions = Blind | Vickrey

  datatype Bid = Bid(user: UserId, amount: int, at: Time)

  datatype Auction = Auction(seller: UserId, startsAt: Time, expiry: Time, opts: SealedBidOptions)

  datatype State = AwaitingStart | AcceptingBids | DisclosingBids

  datatype AddBidResult = Accepted(bids: seq<Bid>) | Rejected(errors: set<Error>)

  // Mirrors SingleSealedBidAuction.GetState:
  //   (time > StartsAt, time < Expiry) switch { (true, true) => AcceptingBids, (true, false) => DisclosingBids, (false, _) => AwaitingStart }
  function GetState(a: Auction, now: Time): State
  {
    if now <= a.startsAt then AwaitingStart
    else if now < a.expiry then AcceptingBids
    else DisclosingBids
  }

  // Mirrors Bid.Validate (see BidValidation.dfy for its own characterisation proofs).
  function ValidateBid(bidder: UserId, seller: UserId, at: Time, startsAt: Time, expiry: Time): set<Error>
  {
    (if bidder == seller then {SellerCannotPlaceBids} else {}) +
    (if at < startsAt then {AuctionHasNotStarted} else {}) +
    (if at > expiry then {AuctionHasEnded} else {})
  }

  predicate HasBidFrom(bids: seq<Bid>, user: UserId)
  {
    exists i :: 0 <= i < |bids| && bids[i].user == user
  }

  // Mirrors SingleSealedBidAuction.TryAddBid.
  function TryAddBid(a: Auction, bids: seq<Bid>, now: Time, bid: Bid): AddBidResult
  {
    match GetState(a, now)
    case AcceptingBids =>
      var errors := ValidateBid(bid.user, a.seller, bid.at, a.startsAt, a.expiry);
      if HasBidFrom(bids, bid.user) then Rejected(errors + {AlreadyPlacedBid})
      else if errors != {} then Rejected(errors)
      else Accepted(bids + [bid])
    case DisclosingBids => Rejected({AuctionHasEnded})
    case AwaitingStart => Rejected({AuctionHasNotStarted})
  }

  function MaxAmount(bids: seq<Bid>): int
    requires |bids| > 0
    decreases |bids|
  {
    if |bids| == 1 then bids[0].amount
    else var rest := MaxAmount(bids[1..]);
         if bids[0].amount >= rest then bids[0].amount else rest
  }

  lemma MaxAmountIsMax(bids: seq<Bid>)
    requires |bids| > 0
    ensures forall i :: 0 <= i < |bids| ==> bids[i].amount <= MaxAmount(bids)
    ensures exists i :: 0 <= i < |bids| && bids[i].amount == MaxAmount(bids)
    decreases |bids|
  {
    if |bids| == 1 {
      assert bids[0].amount == MaxAmount(bids);
    } else {
      MaxAmountIsMax(bids[1..]);
      var rest := MaxAmount(bids[1..]);
      if bids[0].amount >= rest {
        assert bids[0].amount == MaxAmount(bids);
      } else {
        var i :| 0 <= i < |bids[1..]| && bids[1..][i].amount == rest;
        assert bids[1..][i] == bids[i + 1];
        assert bids[i + 1].amount == MaxAmount(bids);
      }
    }
  }

  // The index of the first bid holding the maximum amount. Mirrors the stable LINQ selection:
  // MaxBy for Blind, and the head of OrderByDescending for Vickrey.
  function MaxBidIndex(bids: seq<Bid>): nat
    requires |bids| > 0
    ensures MaxBidIndex(bids) < |bids|
    ensures bids[MaxBidIndex(bids)].amount == MaxAmount(bids)
    decreases |bids|
  {
    if bids[0].amount == MaxAmount(bids) then 0
    else assert |bids| > 1;
         1 + MaxBidIndex(bids[1..])
  }

  // All bids except the one at index i, keeping their order.
  function RemoveAt(bids: seq<Bid>, i: nat): seq<Bid>
    requires i < |bids|
  {
    bids[..i] + bids[i + 1..]
  }

  // Mirrors SingleSealedBidAuction.TryGetAmountAndWinner:
  //  * Blind: the highest bidder wins and pays their own bid (first-price sealed-bid auction).
  //  * Vickrey: the highest bidder wins and pays the second-highest bid — i.e. the highest
  //    bid among all *other* bids (second-price sealed-bid auction).
  function TryGetAmountAndWinner(a: Auction, bids: seq<Bid>, now: Time): Option<(int, UserId)>
  {
    if GetState(a, now) != DisclosingBids || |bids| == 0 then None
    else
      var i := MaxBidIndex(bids);
      match a.opts
      case Blind => Some((bids[i].amount, bids[i].user))
      case Vickrey =>
        if |bids| == 1 then Some((bids[0].amount, bids[0].user))
        else Some((MaxAmount(RemoveAt(bids, i)), bids[i].user))
  }

  // ------------------------------------------------------------------------------------------------
  // Verified properties
  // ------------------------------------------------------------------------------------------------

  // Safety: no bid is ever accepted outside the bidding window.
  lemma NoBidsOutsideWindow(a: Auction, bids: seq<Bid>, now: Time, bid: Bid)
    requires GetState(a, now) != AcceptingBids
    ensures TryAddBid(a, bids, now, bid).Rejected?
  {
  }

  predicate OneBidPerUser(bids: seq<Bid>)
  {
    forall i, j :: 0 <= i < j < |bids| ==> bids[i].user != bids[j].user
  }

  // Invariant: sealed-bid auctions accept at most one bid per user.
  lemma TryAddBidPreservesOneBidPerUser(a: Auction, bids: seq<Bid>, now: Time, bid: Bid)
    requires OneBidPerUser(bids)
    requires TryAddBid(a, bids, now, bid).Accepted?
    ensures OneBidPerUser(TryAddBid(a, bids, now, bid).bids)
  {
  }

  // Bids are only disclosed while accepting further bids or at the end — but there is never a
  // winner before the disclosure phase.
  lemma NoWinnerBeforeDisclosure(a: Auction, bids: seq<Bid>, now: Time)
    requires GetState(a, now) != DisclosingBids
    ensures TryGetAmountAndWinner(a, bids, now) == None
  {
  }

  // Blind (first-price): the winner placed the highest bid and pays exactly their own bid.
  lemma BlindWinnerPaysOwnHighestBid(a: Auction, bids: seq<Bid>, now: Time)
    requires a.opts == Blind
    requires TryGetAmountAndWinner(a, bids, now).Some?
    ensures var (price, winner) := TryGetAmountAndWinner(a, bids, now).value;
            var i := MaxBidIndex(bids);
            && winner == bids[i].user
            && price == bids[i].amount
            && (forall j :: 0 <= j < |bids| ==> bids[j].amount <= price)
  {
    MaxAmountIsMax(bids);
  }

  // Vickrey (second-price), two or more bids: the winner placed the highest bid, but pays the
  // highest bid among the *other* bids — no more than their own bid, and at least as much as
  // every other bid.
  lemma VickreyWinnerPaysSecondHighestBid(a: Auction, bids: seq<Bid>, now: Time)
    requires a.opts == Vickrey
    requires |bids| >= 2
    requires TryGetAmountAndWinner(a, bids, now).Some?
    ensures var (price, winner) := TryGetAmountAndWinner(a, bids, now).value;
            var i := MaxBidIndex(bids);
            var others := RemoveAt(bids, i);
            && winner == bids[i].user
            && bids[i].amount == MaxAmount(bids)
            && price <= bids[i].amount
            && (exists j :: 0 <= j < |others| && others[j].amount == price)
            && (forall j :: 0 <= j < |others| ==> others[j].amount <= price)
  {
    var i := MaxBidIndex(bids);
    var others := RemoveAt(bids, i);
    MaxAmountIsMax(bids);
    MaxAmountIsMax(others);
    assert forall j :: 0 <= j < |others| ==> others[j] in bids[..i] || others[j] in bids[i + 1..];
    assert forall j :: 0 <= j < |others| ==> others[j].amount <= MaxAmount(bids);
  }

  // A Vickrey winner never pays more than a Blind winner would have, on the same bids.
  lemma VickreyNeverExceedsBlind(a1: Auction, a2: Auction, bids: seq<Bid>, now: Time)
    requires a1.opts == Vickrey && a2.opts == Blind
    requires a1.seller == a2.seller && a1.startsAt == a2.startsAt && a1.expiry == a2.expiry
    requires TryGetAmountAndWinner(a1, bids, now).Some?
    ensures TryGetAmountAndWinner(a2, bids, now).Some?
    ensures TryGetAmountAndWinner(a1, bids, now).value.0 <= TryGetAmountAndWinner(a2, bids, now).value.0
  {
    if |bids| >= 2 {
      var i := MaxBidIndex(bids);
      MaxAmountIsMax(RemoveAt(bids, i));
      MaxAmountIsMax(bids);
    }
  }
}
