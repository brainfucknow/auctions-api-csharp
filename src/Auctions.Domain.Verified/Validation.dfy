// Dafny-first source of the auction validation rules (Phase 8 — reverse ownership).
//
// This file is PRODUCTION SOURCE, not a model: `dafny translate cs` compiles it to Generated/Validation.cs
// (committed; CI regenerates and fails on drift), and Auctions.Domain calls the compiled code. The Dafny
// verifier proves the specifications below — including, thanks to the 64-bit newtypes, the absence of
// arithmetic overflow — before any C# is emitted. Run ./generate.sh after editing.
//
// Interop notes:
//  * Int64 compiles to C# long; Errors (bv32) compiles to C# uint — Auctions.Domain casts to its
//    [Flags] Errors enum, whose member values these constants mirror (see Wallymathieu.Auctions
//    DomainModels/Errors.cs).
//  * Times are DateTimeOffset.UtcTicks (comparing DateTimeOffset values compares their UTC instants).
//  * ValidateBid is generic over the user id type: Dafny compiles `==` on a (==) type parameter to
//    .NET value equality, which matches the C# UserId record.
module AuctionValidation {

  newtype Int64 = x: int | -0x8000_0000_0000_0000 <= x < 0x8000_0000_0000_0000

  const INT64_MAX: Int64 := 0x7FFF_FFFF_FFFF_FFFF

  // Mirrors Wallymathieu.Auctions.DomainModels.Errors ([Flags] enum).
  type Errors = bv32
  const ErrNone: Errors := 0
  const ErrAuctionHasEnded: Errors := 4
  const ErrAuctionHasNotStarted: Errors := 8
  const ErrSellerCannotPlaceBids: Errors := 32
  const ErrMustPlaceBidOverHighestBid: Errors := 256
  const ErrMustRaiseWithAtLeast: Errors := 1024

  // A bid is valid exactly when the bidder is not the seller and the bid falls inside the auction
  // window; each error flag is reported exactly when its condition holds.
  function ValidateBid<U(==)>(bidder: U, seller: U, at: Int64, startsAt: Int64, expiry: Int64): (r: Errors)
    ensures r == ErrNone <==> bidder != seller && startsAt <= at <= expiry
    ensures r & ErrSellerCannotPlaceBids != 0 <==> bidder == seller
    ensures r & ErrAuctionHasNotStarted != 0 <==> at < startsAt
    ensures r & ErrAuctionHasEnded != 0 <==> at > expiry
    ensures r & !(ErrSellerCannotPlaceBids | ErrAuctionHasNotStarted | ErrAuctionHasEnded) == 0
  {
    (if bidder == seller then ErrSellerCannotPlaceBids else ErrNone) |
    (if at < startsAt then ErrAuctionHasNotStarted else ErrNone) |
    (if at > expiry then ErrAuctionHasEnded else ErrNone)
  }

  // English-auction raise policy: a new bid is accepted exactly when it is strictly above the highest
  // standing bid and raises it by at least minRaise. The function is total — no preconditions:
  //  * a non-positive minRaise means "no minimum raise" (the pre-migration C# behaved identically);
  //  * if highestBid + minRaise exceeds Int64 range, no representable bid can satisfy the raise, so the
  //    bid is rejected — the specification is stated over mathematical integers, which the verifier
  //    proves the bounded implementation satisfies (the pre-migration C# wrapped around instead).
  function ValidateRaise(amount: Int64, highestBid: Int64, minRaise: Int64): (r: Errors)
    ensures var raise := if minRaise < 0 then 0 else minRaise as int;
            r == ErrNone <==> amount as int > highestBid as int &&
                              amount as int >= highestBid as int + raise
    ensures r == ErrNone || r == ErrMustPlaceBidOverHighestBid || r == ErrMustRaiseWithAtLeast
  {
    var raise := if minRaise < 0 then 0 else minRaise;
    if amount <= highestBid then ErrMustPlaceBidOverHighestBid
    else if highestBid > INT64_MAX - raise then ErrMustRaiseWithAtLeast
    else if amount < highestBid + raise then ErrMustRaiseWithAtLeast
    else ErrNone
  }
}
