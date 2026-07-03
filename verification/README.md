# Formal verification sidecar

This directory introduces formal verification into the auctions codebase **incrementally**: the
production application stays in C#, while the business-critical algorithms are modelled and proved in
[Dafny](https://dafny.org) (verified by Boogie + Z3). Nothing here changes runtime behaviour — the
verifier, not the runtime, is the authority.

```text
                 C# domain (src/Auctions.Domain)
                         │
              [Verify] + Contract.Requires/Ensures
                         │
                         ▼
        tools/CSharpToDafny (Roslyn extraction tool)
                         │
                         ▼
     verification/Generated (Dafny skeletons, committed)
                         │
         human / LLM completes the specifications
                         │
                         ▼
      verification/Dafny (handwritten, machine-checked)
                         │
                         ▼
                dafny verify  (CI: verification.yml)
```

## Layout

| Path | Contents |
|------|----------|
| `verification/Dafny` | Handwritten, fully verified models of the auction domain. |
| `verification/Generated` | Dafny skeletons generated from `[Verify]`-annotated C# methods. Do not edit; regenerate. |
| `tools/CSharpToDafny` | Roslyn tool that extracts `[Verify]` methods and their contracts to Dafny. |

## Why a bespoke extraction tool? (prior art)

There is no Microsoft (or other maintained) tool that translates C# to Dafny, with Roslyn or
otherwise — which is why `tools/CSharpToDafny` exists. The surrounding landscape, and why we sit
where we do in it:

- **Dafny's supported C# integration runs in the opposite direction.** Dafny originated at
  [Microsoft Research](https://www.microsoft.com/en-us/research/publication/dafny-program-verifier/)
  and its toolchain compiles **Dafny → C#** (also Java, Go, Python, JavaScript), with `{:extern}` for
  calling hand-written C#; see the
  [Dafny ↔ C# integration guide](https://dafny.org/dafny/DafnyRef/integration-cs/IntegrationCS).
  That is the "reverse ownership" end-state sketched under *next steps* below: for the most critical
  components, the verified Dafny becomes the production implementation.
- **The historical Microsoft tools for verifying C# predate or bypass Roslyn, and are dormant.**
  *Spec#* (a C# superset verified via Boogie, custom compiler, ~2004) is long dead. *Code Contracts*
  (`System.Diagnostics.Contracts` + the Clousot static checker) worked by IL rewriting and abstract
  interpretation, was abandoned around 2015 and never got a Roslyn-era successor — our
  `Contract.Requires` / `Contract.Ensures` vocabulary deliberately mirrors it, because the shape was
  right even if the tooling died. *BCT* (Bytecode Translator, .NET IL → Boogie) is likewise dormant.
- **Current work in the C# → Dafny direction is research, not product** — e.g. LLM-based synthesis of
  verified Dafny such as
  [Towards AI-Assisted Synthesis of Verified Dafny Methods](https://arxiv.org/pdf/2402.00247). That
  maps to our Phase 6 role for LLMs: they may *propose* specifications and proofs, but only what
  `dafny verify` accepts counts.

The absence of a general tool is not an accident: full C# → Dafny transpilation is very hard (heap and
reference semantics, LINQ, exceptions, inheritance and virtual dispatch would all need faithful
modelling). Our tool deliberately dodges that by translating only what is tractable and valuable —
**signatures and contracts of pure, static methods** — and leaving bodies to humans/LLMs with the
verifier as the gate. That keeps the tool small enough to trust while still automating the
boilerplate: type mapping, enum/flags encoding, and drift detection in CI.

## Running it locally

```bash
dotnet tool restore                 # installs the pinned Dafny CLI (see .config/dotnet-tools.json)
# Z3 4.12.1 must be on PATH; the z3-solver 4.12.1.0 wheel on PyPI ships the executable
# (see .github/workflows/verification.yml for the exact steps CI uses).

# Regenerate skeletons from [Verify] methods (run from the repository root):
dotnet run --project tools/CSharpToDafny -- --source src/Auctions.Domain --output verification/Generated

# Verify everything:
for f in verification/Dafny/*.dfy verification/Generated/*.dfy; do dotnet tool run dafny -- verify "$f"; done
```

Each `.dfy` file is self-contained (or pulls in its dependencies via `include`), so files are verified
one at a time.

## Opting a method in

1. Keep (or refactor) the logic as a **pure, static** method over simple types — see
   `Bid.Validate(UserId, UserId, DateTimeOffset, DateTimeOffset, DateTimeOffset)` and
   `TimedAscendingAuction.ValidateRaise(long, long, long)` for the pattern.
2. Mark it `[Verify]` (`Wallymathieu.Auctions.Verification.VerifyAttribute`) and state its contract with
   `Contract.Requires(...)` / `Contract.Ensures(result => ...)`. The contract calls compile away unless
   `CONTRACTS_FULL` is defined; they exist for the extraction pipeline.
3. Run the extraction tool (above) and commit the regenerated files — CI fails on drift.
4. Complete the specification: give the generated contract a verified implementation (see
   `Dafny/GeneratedContracts.dfy`) and, for state machines and algorithms, a proper model with invariant
   proofs (see `Dafny/TimedAscending.dfy`). An LLM may *propose* specifications and proofs, but only what
   `dafny verify` accepts counts.

### What translates today

Parameters and returns: `int`, `long`, `bool`, `string`, `System.DateTimeOffset` (as integer `Time`),
enums (as `bv32` with named constants — `[Flags]` combination via `|`/`&` works), and domain
records/classes as opaque types with equality (e.g. `UserId`). Contract expressions: comparisons,
`&&`, `||`, `!`, `+`, `-`, `*`, `|`, `&`, literals, parameters and enum members. Anything else is
emitted as a `// TODO(unsupported contract)` comment rather than silently mistranslated; instance
methods and unsupported types are skipped with a warning.

## What is proved today

`Dafny/BidValidation.dfy` — model of `Bid.Validate`:
- a bid is valid **exactly** when the bidder is not the seller and the bid is inside the auction window;
- each reported error flag is truthful; widening the window never invalidates a valid bid.

`Dafny/TimedAscending.dfy` — model of `TimedAscendingAuction` (English auction):
- no bid is accepted outside the `OnGoing` state;
- an accepted bid beats the standing high bid and respects the minimum raise
  (the `ValidateRaise` characterisation matches the C# `Contract.Ensures`);
- accepted bids are **strictly ascending** — the standing price can only go up (invariant);
- the recorded `EndsAt` never moves backwards;
- a winner exists only after the end, holds the highest bid, and meets the reserve price.

`Dafny/SingleSealedBid.dfy` — model of `SingleSealedBidAuction`:
- no bids accepted outside the bidding window; at most **one bid per user** (invariant);
- no winner before the disclosure phase;
- Blind (first-price): the winner placed the highest bid and pays exactly their own bid;
- Vickrey (second-price): the winner placed the highest bid and pays the highest **other** bid —
  never more than their own bid, and at least as much as every other bid;
- on the same bids, a Vickrey winner never pays more than a Blind winner would.

`Dafny/GeneratedContracts.dfy` — discharges the `{:axiom}` contracts in `verification/Generated` by
proving implementations that mirror the original C# bodies (including the `bv32` flags representation).

### Observation surfaced by modelling

`TimedAscendingAuction.GetState` decides `HasEnded` from the **initial** `Expiry`, while `EndsAt`
(extended by `TimeFrame` on every accepted bid) is only recorded — so the time-frame extension does not
actually extend the bidding window. The models mirror the code as-is; if the extension is *intended* to
extend bidding, that is a behavioural change to make in C# first, after which the model (and its proofs)
should be updated to match.

## Known limitations / next steps

- The link between generated skeletons and their discharging proofs in `GeneratedContracts.dfy` is by
  convention (kept-in-sync text), not by a Dafny refinement relation.
- The extraction tool translates contracts, not method bodies — bodies are quoted as comments for the
  human/LLM completing the specification.
- Candidate next targets: `Amount` arithmetic (same-currency preconditions), auction state transition
  helpers, and eventually compiling verified Dafny back to C# for the most critical components
  (reverse ownership).
