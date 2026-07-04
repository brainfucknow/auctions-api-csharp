// Formal model of the timed ascending (English) auction, mirroring
//   src/Auctions.Domain/DomainModels/TimedAscendingAuction.cs
//
// The model is deliberately self-contained (no includes) so that each file can be verified on its own.
//
// Modelling notes:
//  * Time is an integer (DateTimeOffset ticks).
//  * The C# [Flags] Errors enum combined with `|` is modelled as a set of errors; Errors.None is `{}`.
//  * Amounts are unbounded integers. The production raise policy is itself compiled from verified Dafny
//    (src/Auctions.Domain.Verified/Validation.dfy) over 64-bit newtypes; it additionally rejects raises
//    whose bound exceeds the Int64 range — a case that cannot arise in this unbounded model. Elsewhere
//    the two agree, including treating a negative minRaise as "no minimum raise".
//  * Observation from modelling: the C# GetState uses the *initial* `Expiry` to decide whether the auction
//    has ended, while `EndsAt` (which is extended by `TimeFrame` on every accepted bid) is only recorded.
//    The model mirrors that behaviour faithfully; see EndsAtDoesNotAffectState below.
module TimedAscending {

  type Time = int
  type UserId(==)

  datatype Error =
    | SellerCannotPlaceBids
    | AuctionHasNotStarted
    | AuctionHasEnded
    | MustPlaceBidOverHighestBid
    | MustRaiseWithAtLeast

  datatype Option<T> = None | Some(value: T)

  datatype TimedAscendingOptions = TimedAscendingOptions(reservePrice: int, minRaise: int, timeFrame: int)

  datatype Bid = Bid(user: UserId, amount: int, at: Time)

  // The immutable part of TimedAscendingAuction.
  datatype Auction = Auction(seller: UserId, startsAt: Time, expiry: Time, opts: TimedAscendingOptions)

  // The mutable part of TimedAscendingAuction: the accepted bids and the recorded end time.
  datatype AuctionState = AuctionState(bids: seq<Bid>, endsAt: Option<Time>)

  datatype State = AwaitingStart | OnGoing | HasEnded

  datatype AddBidResult = Accepted(next: AuctionState) | Rejected(errors: set<Error>)

  // Mirrors TimedAscendingAuction.GetState:
  //   (time > StartsAt, time < Expiry) switch { (true, true) => OnGoing, (true, false) => HasEnded, (false, _) => AwaitingStart }
  function GetState(a: Auction, now: Time): State
  {
    if now <= a.startsAt then AwaitingStart
    else if now < a.expiry then OnGoing
    else HasEnded
  }

  // Mirrors Bid.Validate (see BidValidation.dfy for its own characterisation proofs).
  function ValidateBid(bidder: UserId, seller: UserId, at: Time, startsAt: Time, expiry: Time): set<Error>
  {
    (if bidder == seller then {SellerCannotPlaceBids} else {}) +
    (if at < startsAt then {AuctionHasNotStarted} else {}) +
    (if at > expiry then {AuctionHasEnded} else {})
  }

  // Mirrors AuctionValidation.ValidateRaise (src/Auctions.Domain.Verified/Validation.dfy).
  function ValidateRaise(amount: int, highestBid: int, minRaise: int): set<Error>
  {
    if amount <= highestBid then {MustPlaceBidOverHighestBid}
    else if amount < highestBid + minRaise then {MustRaiseWithAtLeast}
    else {}
  }

  // The characterisation the production source proves over Int64 (here over unbounded integers).
  lemma ValidateRaiseCharacterisation(amount: int, highestBid: int, minRaise: int)
    ensures ValidateRaise(amount, highestBid, minRaise) == {}
        <==> amount > highestBid && amount >= highestBid + minRaise
  {
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

  // Mirrors Bids.MaxBy(b => b.Amount): the first bid holding the maximum amount.
  function MaxBid(bids: seq<Bid>): Bid
    requires |bids| > 0
    ensures MaxBid(bids) in bids
    ensures MaxBid(bids).amount == MaxAmount(bids)
    decreases |bids|
  {
    if |bids| == 1 then bids[0]
    else assert bids == [bids[0]] + bids[1..];
         var rest := MaxBid(bids[1..]);
         if bids[0].amount >= rest.amount then bids[0] else rest
  }

  // Mirrors the EndsAt update on an accepted bid:
  //   EndsAt = new[] { EndsAt, Expiry, time + Options.TimeFrame }.Where(v => v != null).Max();
  function Max2(x: int, y: int): int
  {
    if x >= y then x else y
  }

  function NextEndsAt(a: Auction, endsAt: Option<Time>, now: Time): Time
  {
    var candidate := Max2(a.expiry, now + a.opts.timeFrame);
    match endsAt
    case None => candidate
    case Some(e) => Max2(e, candidate)
  }

  // Mirrors TimedAscendingAuction.TryAddBid.
  function TryAddBid(a: Auction, s: AuctionState, now: Time, bid: Bid): AddBidResult
  {
    match GetState(a, now)
    case OnGoing =>
      var errors := ValidateBid(bid.user, a.seller, bid.at, a.startsAt, a.expiry);
      var raiseErrors := if |s.bids| > 0
                         then ValidateRaise(bid.amount, MaxAmount(s.bids), a.opts.minRaise)
                         else {};
      if raiseErrors != {} then Rejected(errors + raiseErrors)
      else if errors != {} then Rejected(errors)
      else Accepted(AuctionState(s.bids + [bid], Some(NextEndsAt(a, s.endsAt, now))))
    case HasEnded => Rejected({AuctionHasEnded})
    case AwaitingStart => Rejected({AuctionHasNotStarted})
  }

  // Mirrors TimedAscendingAuction.TryGetAmountAndWinner.
  function TryGetAmountAndWinner(a: Auction, s: AuctionState, now: Time): Option<(int, UserId)>
  {
    if GetState(a, now) == HasEnded && |s.bids| > 0 && MaxBid(s.bids).amount >= a.opts.reservePrice
    then Some((MaxBid(s.bids).amount, MaxBid(s.bids).user))
    else None
  }

  // ------------------------------------------------------------------------------------------------
  // Verified properties
  // ------------------------------------------------------------------------------------------------

  // Safety: no bid is ever accepted outside the bidding window.
  lemma NoBidsOutsideWindow(a: Auction, s: AuctionState, now: Time, bid: Bid)
    requires GetState(a, now) != OnGoing
    ensures TryAddBid(a, s, now, bid).Rejected?
  {
  }

  // An accepted bid was placed by someone other than the seller, within the auction window,
  // and — when there is a standing bid — strictly above it and by at least the minimum raise.
  lemma AcceptedBidIsValid(a: Auction, s: AuctionState, now: Time, bid: Bid)
    requires TryAddBid(a, s, now, bid).Accepted?
    ensures bid.user != a.seller
    ensures a.startsAt <= bid.at <= a.expiry
    ensures |s.bids| > 0 ==>
              bid.amount > MaxAmount(s.bids) &&
              bid.amount >= MaxAmount(s.bids) + a.opts.minRaise
  {
  }

  predicate StrictlyAscending(bids: seq<Bid>)
  {
    forall i, j :: 0 <= i < j < |bids| ==> bids[i].amount < bids[j].amount
  }

  // Invariant: starting from a state whose bids are strictly ascending (e.g. the empty auction),
  // every accepted bid keeps them strictly ascending. Together with AcceptedBidIsValid this shows the
  // standing price of an English auction can only go up.
  lemma TryAddBidPreservesAscending(a: Auction, s: AuctionState, now: Time, bid: Bid)
    requires StrictlyAscending(s.bids)
    requires TryAddBid(a, s, now, bid).Accepted?
    ensures StrictlyAscending(TryAddBid(a, s, now, bid).next.bids)
  {
    if |s.bids| > 0 {
      MaxAmountIsMax(s.bids);
    }
  }

  // The recorded end time never moves backwards when bids are accepted.
  lemma EndsAtIsMonotone(a: Auction, s: AuctionState, now: Time, bid: Bid)
    requires TryAddBid(a, s, now, bid).Accepted?
    ensures var next := TryAddBid(a, s, now, bid).next;
            next.endsAt.Some? && (s.endsAt.Some? ==> s.endsAt.value <= next.endsAt.value)
  {
  }

  // There is no winner while the auction is still open.
  lemma NoWinnerBeforeEnd(a: Auction, s: AuctionState, now: Time)
    requires GetState(a, now) != HasEnded
    ensures TryGetAmountAndWinner(a, s, now) == None
  {
  }

  // If there is a winner: the auction has ended, the winning amount is the highest bid, it meets the
  // reserve price, and it belongs to an actual bid by the winner.
  lemma WinnerHasHighestBid(a: Auction, s: AuctionState, now: Time)
    requires TryGetAmountAndWinner(a, s, now).Some?
    ensures GetState(a, now) == HasEnded
    ensures var (amount, winner) := TryGetAmountAndWinner(a, s, now).value;
            && amount >= a.opts.reservePrice
            && Bid(winner, amount, MaxBid(s.bids).at) in s.bids
            && (forall i :: 0 <= i < |s.bids| ==> s.bids[i].amount <= amount)
  {
    MaxAmountIsMax(s.bids);
  }
}
