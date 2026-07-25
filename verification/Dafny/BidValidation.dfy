// Formal model of bid validation, mirroring Bid.Validate — whose production implementation is itself
// compiled from verified Dafny: src/Auctions.Domain.Verified/Validation.dfy. This model restates the
// rules with errors as sets (rather than the interop-oriented bv32) and proves additional properties.
//
// Time is modelled as an integer (DateTimeOffset ticks): the C# comparison operators on
// DateTimeOffset are a total order, which integers model faithfully for this purpose.
module BidValidation {

  type Time = int

  // UserId is an uninterpreted type with equality — the validation logic only ever compares ids.
  type UserId(==)

  // The subset of Wallymathieu.Auctions.DomainModels.Errors that bid validation can produce.
  // The C# [Flags] enum combined with `|` is modelled as a set of errors; Errors.None is `{}`.
  datatype Error =
    | SellerCannotPlaceBids
    | AuctionHasNotStarted
    | AuctionHasEnded

  // Mirrors Bid.Validate(bidder, seller, at, startsAt, expiry).
  function ValidateBid(bidder: UserId, seller: UserId, at: Time, startsAt: Time, expiry: Time): set<Error>
  {
    (if bidder == seller then {SellerCannotPlaceBids} else {}) +
    (if at < startsAt then {AuctionHasNotStarted} else {}) +
    (if at > expiry then {AuctionHasEnded} else {})
  }

  // The headline contract (also proved on the production source in Validation.dfy):
  // a bid is valid exactly when the bidder is not the seller and the bid falls inside the window.
  lemma ValidateBidCharacterisation(bidder: UserId, seller: UserId, at: Time, startsAt: Time, expiry: Time)
    ensures ValidateBid(bidder, seller, at, startsAt, expiry) == {}
        <==> bidder != seller && startsAt <= at && at <= expiry
  {
  }

  // The seller can never place a bid, no matter the timing.
  lemma SellerCanNeverBid(seller: UserId, at: Time, startsAt: Time, expiry: Time)
    ensures SellerCannotPlaceBids in ValidateBid(seller, seller, at, startsAt, expiry)
  {
  }

  // Each reported error is truthful: it appears exactly when its condition holds.
  lemma ErrorsAreTruthful(bidder: UserId, seller: UserId, at: Time, startsAt: Time, expiry: Time)
    ensures var errors := ValidateBid(bidder, seller, at, startsAt, expiry);
            && (SellerCannotPlaceBids in errors <==> bidder == seller)
            && (AuctionHasNotStarted in errors <==> at < startsAt)
            && (AuctionHasEnded in errors <==> at > expiry)
  {
  }

  // Validation is monotone in the window: enlarging the auction window never introduces errors.
  lemma WidenWindowPreservesValidity(bidder: UserId, seller: UserId, at: Time,
                                     startsAt: Time, expiry: Time, startsAt': Time, expiry': Time)
    requires startsAt' <= startsAt && expiry <= expiry'
    requires ValidateBid(bidder, seller, at, startsAt, expiry) == {}
    ensures ValidateBid(bidder, seller, at, startsAt', expiry') == {}
  {
  }
}
