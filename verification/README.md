# Formal verification

Formal verification is introduced into the auctions codebase **incrementally** using
[Dafny](https://dafny.org) (verified by Boogie + Z3). Two complementary mechanisms exist, and methods
graduate from the first to the second:

1. **The sidecar (on-ramp).** C# methods opt in with `[Verify]` + contracts; the Roslyn tool extracts
   Dafny skeletons; humans/LLMs complete specifications; handwritten models in `verification/Dafny`
   prove domain invariants. The C# stays the production implementation.
2. **Dafny-first (reverse ownership).** Once a candidate's specification is stable, the method
   *graduates*: its implementation moves to Dafny source in `src/Auctions.Domain.Verified`, is proved by
   the verifier (including absence of arithmetic overflow, via 64-bit newtypes), compiled to C# with
   `dafny translate cs`, and called by the domain through a thin interop shim. Dafny — not C# — becomes
   the source of truth. `Bid.Validate` and `TimedAscendingAuction.ValidateRaise` have graduated.

```text
   ┌── the sidecar (on-ramp) ────────────────┐   ┌── Dafny-first (graduated) ──────────────┐
   │  C# method + [Verify] + Contract        │   │  src/Auctions.Domain.Verified/*.dfy     │
   │            │                            │   │  (production source, machine-checked)   │
   │            ▼                            │   │            │                            │
   │  tools/CSharpToDafny (Roslyn)           │   │  dafny translate cs   (generate.sh)     │
   │            │                            │   │            │                            │
   │            ▼                            │   │            ▼                            │
   │  verification/Generated (skeletons)     │   │  Generated/Validation.cs (committed)    │
   │            │                            │   │            │                            │
   │            ▼                            │   │            ▼                            │
   │  verification/Dafny (models + proofs)   │   │  Auctions.Domain calls it via a shim    │
   └──────────────────┬──────────────────────┘   └──────────────────┬─────────────────────┘
                      └───────────► dafny verify + drift checks (CI: verification.yml)
```

## Layout

| Path | Contents |
|------|----------|
| `src/Auctions.Domain.Verified` | **Dafny-first production code**: `Validation.dfy` (source of truth) and the committed C# compiled from it. |
| `verification/Dafny` | Handwritten, fully verified models of the auction domain. |
| `verification/Generated` | Dafny skeletons generated from `[Verify]`-annotated C# methods. Do not edit; regenerate. (Currently empty: both extracted methods have graduated to Dafny-first.) |
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

# Verify + recompile the Dafny-first core after editing Validation.dfy:
./src/Auctions.Domain.Verified/generate.sh

# Regenerate skeletons from [Verify] methods (run from the repository root):
dotnet run --project tools/CSharpToDafny -- --source src/Auctions.Domain --output verification/Generated

# Verify the models:
for f in verification/Dafny/*.dfy verification/Generated/*.dfy; do dotnet tool run dafny -- verify "$f"; done
```

Each `.dfy` file is self-contained (or pulls in its dependencies via `include`), so files are verified
one at a time. CI fails if either generated artifact (the compiled verified core, or the skeletons)
drifts from what is committed.

## The Dafny-first core

`src/Auctions.Domain.Verified/Validation.dfy` is production source. Its functions are **total** (no
preconditions to discharge at call sites) and specified completely:

- `ValidateBid<U(==)>` — generic over the user id type; Dafny compiles `==` on it to .NET value
  equality, which matches the C# `UserId` record. Times cross the boundary as
  `DateTimeOffset.UtcTicks`.
- `ValidateRaise` — operates on an `Int64` newtype, so the verifier **proves the absence of overflow**
  instead of it being a documented assumption. Two deliberate semantics, both proved against a
  specification stated over mathematical integers: a non-positive `minRaise` means "no minimum raise"
  (identical to the pre-migration C#), and a raise bound exceeding `Int64` range rejects the bid
  (the pre-migration C# wrapped around and *accepted* — a bug fixed by construction here).

The `[Flags] Errors` enum crosses the boundary as the `uint` that Dafny's `bv32` compiles to; the
constants in `Validation.dfy` mirror the enum's member values, and the only hand-written interop is
`Auctions.Domain/Verification/VerifiedCore.cs` (two one-line conversions). Editing workflow: change
`Validation.dfy`, run `generate.sh` (translation verifies first — an unprovable spec aborts
generation), commit both files.

## Opting a method in (the sidecar on-ramp)

1. Keep (or refactor) the logic as a **pure, static** method over simple types — see
   `Bid.Validate(UserId, UserId, DateTimeOffset, DateTimeOffset, DateTimeOffset)` and
   `TimedAscendingAuction.ValidateRaise(long, long, long)` for the pattern.
2. Mark it `[Verify]` (`Wallymathieu.Auctions.Verification.VerifyAttribute`) and state its contract with
   `Contract.Requires(...)` / `Contract.Ensures(result => ...)`. The contract calls compile away unless
   `CONTRACTS_FULL` is defined; they exist for the extraction pipeline.
3. Run the extraction tool (above) and commit the regenerated files — CI fails on drift.
4. Complete the specification: give the generated contract a verified implementation and, for state
   machines and algorithms, a proper model with invariant proofs (see `Dafny/TimedAscending.dfy`). An
   LLM may *propose* specifications and proofs, but only what `dafny verify` accepts counts.
5. When the specification is stable, **graduate the method**: move the implementation into
   `src/Auctions.Domain.Verified/Validation.dfy` (or a sibling module), run `generate.sh`, delegate the
   C# method body to the compiled code via `VerifiedCore`, and drop the `[Verify]`/`Contract` markers —
   the Dafny source now carries the contract.

### What translates today

Parameters and returns: `int`, `long`, `bool`, `string`, `System.DateTimeOffset` (as integer `Time`),
enums (as `bv32` with named constants — `[Flags]` combination via `|`/`&` works), and domain
records/classes as opaque types with equality (e.g. `UserId`). Contract expressions: comparisons,
`&&`, `||`, `!`, `+`, `-`, `*`, `|`, `&`, literals, parameters, enum members, and numeric constant
fields such as `long.MaxValue` (emitted as their literal value — useful for no-overflow
preconditions, since Dafny integers are unbounded while C# `long` arithmetic wraps). Anything else is
emitted as a `// TODO(unsupported contract)` comment rather than silently mistranslated; instance
methods and unsupported types are skipped with a warning.

## What is proved today

`src/Auctions.Domain.Verified/Validation.dfy` — **the production implementation** of bid validation and
the raise policy:
- the complete input/output characterisation of both functions (stated over mathematical integers);
- truthfulness of every error flag;
- absence of arithmetic overflow (via the `Int64` newtype — these are verifier obligations, not
  assumptions).

`Dafny/BidValidation.dfy` — model of `Bid.Validate`:
- a bid is valid **exactly** when the bidder is not the seller and the bid is inside the auction window;
- each reported error flag is truthful; widening the window never invalidates a valid bid.

`Dafny/TimedAscending.dfy` — model of `TimedAscendingAuction` (English auction):
- no bid is accepted outside the `OnGoing` state;
- an accepted bid beats the standing high bid and respects the minimum raise
  (the `ValidateRaise` characterisation matches the one proved on the production source);
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

### Observation surfaced by modelling

`TimedAscendingAuction.GetState` decides `HasEnded` from the **initial** `Expiry`, while `EndsAt`
(extended by `TimeFrame` on every accepted bid) is only recorded — so the time-frame extension does not
actually extend the bidding window. The models mirror the code as-is; if the extension is *intended* to
extend bidding, that is a behavioural change to make in C# first, after which the model (and its proofs)
should be updated to match.

## Known limitations / next steps

- For the graduated functions the verified artifact **is** the production code — no sync-by-convention
  gap remains. The state-machine proofs in `verification/Dafny`, however, are still models of the
  EF-mapped entity classes (`TimedAscendingAuction`, `SingleSealedBidAuction`); their invariants hold of
  the model, and the entities mirror it by convention.
- The extraction tool translates contracts, not method bodies — bodies are quoted as comments for the
  human/LLM completing the specification.
- Candidate next targets for the on-ramp: `Amount` arithmetic (same-currency preconditions) and the
  auction state computation; the natural next graduation is a verified functional core for the auction
  state machines that the entity classes delegate to, extending Dafny-first beyond leaf functions.
