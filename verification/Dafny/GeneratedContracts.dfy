// Verified implementations of the contracts extracted by tools/CSharpToDafny into verification/Generated.
//
// The generated skeletons carry {:axiom} — their contracts are assumed, not proved. This file discharges
// them: each method below repeats a generated contract verbatim and provides an implementation mirroring
// the C# body, which Dafny then proves against the contract. This demonstrates that the specifications
// written in the C# Contract clauses are realizable, and that the C# implementations (transliterated to
// Dafny, including the [Flags] bit-vector representation of Errors) satisfy them.
//
// Keep the signatures and clauses in sync with verification/Generated when contracts change; the
// implementations mirror the original C# bodies quoted in the generated files.
include "../Generated/Types.dfy"

module GeneratedContracts {
  import opened GeneratedTypes

  // Discharges Generated_Wallymathieu_Auctions_DomainModels_Bid.Validate
  // (mirrors the body of Bid.Validate in src/Auctions.Domain/DomainModels/Bid.cs).
  method Validate(bidder: UserId, seller: UserId, at: Time, startsAt: Time, expiry: Time)
    returns (result: Errors)
    ensures (result == Errors_None) == (bidder != seller && startsAt <= at && at <= expiry)
  {
    var errors := Errors_None;
    if bidder == seller { errors := errors | Errors_SellerCannotPlaceBids; }
    if at < startsAt { errors := errors | Errors_AuctionHasNotStarted; }
    if at > expiry { errors := errors | Errors_AuctionHasEnded; }
    result := errors;
  }

  // Discharges Generated_Wallymathieu_Auctions_DomainModels_TimedAscendingAuction.ValidateRaise
  // (mirrors the body of TimedAscendingAuction.ValidateRaise in src/Auctions.Domain/DomainModels/TimedAscendingAuction.cs).
  method ValidateRaise(amount: int, highestBid: int, minRaise: int)
    returns (result: Errors)
    requires minRaise >= 0
    ensures (result == Errors_None) == (amount > highestBid && amount >= highestBid + minRaise)
  {
    if amount <= highestBid { result := Errors_MustPlaceBidOverHighestBid; }
    else if amount < highestBid + minRaise { result := Errors_MustRaiseWithAtLeast; }
    else { result := Errors_None; }
  }
}
