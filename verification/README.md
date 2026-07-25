# Formal verification

The business-critical auction rules are formally verified with [Dafny](https://dafny.org) (verified by
Boogie + Z3), **Dafny-first**: the verified Dafny in `src/Auctions.Domain.Verified` is the production
source of truth, compiled to C# and called by the domain through a thin interop shim. Alongside it,
handwritten models in `verification/Dafny` prove the invariants of the auction state machines. The
verifier — not tests, not review — is the authority on the verified properties.

```text
   src/Auctions.Domain.Verified/Validation.dfy        verification/Dafny/*.dfy
   (production source, machine-checked,               (models of the auction state
    overflow-free by proof)                            machines, invariant proofs)
                 │                                                │
   dafny translate cs   (generate.sh)                       dafny verify
                 │                                                │
                 ▼                                                │
   Generated/Validation.cs (committed;                            │
   CI recompiles and fails on drift)                              │
                 │                                                │
                 ▼                                                ▼
   Auctions.Domain calls it via VerifiedCore        CI: verification.yml gates both
```

The codebase got here incrementally. A Roslyn *sidecar* came first: C# methods opted in with a
`[Verify]` attribute and `Contract.Requires`/`Ensures` clauses, and a bespoke extraction tool
(`tools/CSharpToDafny`) generated Dafny skeletons whose specifications were then completed and proved.
Once both extracted methods graduated to Dafny-first ownership, the sidecar had no remaining consumers
and was removed — it lives in the git history should new C# candidates warrant reviving it.

## Layout

| Path | Contents |
|------|----------|
| `src/Auctions.Domain.Verified` | **Dafny-first production code**: `Validation.dfy` (source of truth) and the committed C# compiled from it. |
| `verification/Dafny` | Handwritten, fully verified models of the auction domain. |

## Why Dafny-first? (prior art)

There is no Microsoft (or other maintained) tool that translates C# to Dafny, with Roslyn or otherwise
— which is why the interim extraction tool was bespoke, and why the durable architecture goes the other
way:

- **Dafny's supported C# integration compiles Dafny → C#.** Dafny originated at
  [Microsoft Research](https://www.microsoft.com/en-us/research/publication/dafny-program-verifier/)
  and its toolchain targets C# (also Java, Go, Python, JavaScript), with `{:extern}` for calling
  hand-written code; see the
  [Dafny ↔ C# integration guide](https://dafny.org/dafny/DafnyRef/integration-cs/IntegrationCS).
  Dafny-first "reverse ownership" is therefore the supported, durable direction — the verified
  implementation *is* the production implementation, so nothing can drift.
- **The historical Microsoft tools for verifying C# are dormant.** *Spec#* (a C# superset verified via
  Boogie, ~2004) is long dead; *Code Contracts* (`System.Diagnostics.Contracts` + the Clousot static
  checker) was abandoned around 2015 with no Roslyn-era successor; *BCT* (Bytecode Translator, .NET IL
  → Boogie) is likewise dormant. Verifying C# in place is a research graveyard; compiling verified
  Dafny into the application is not.
- **C# → Dafny remains research, not product** — e.g. LLM-based synthesis of verified Dafny such as
  [Towards AI-Assisted Synthesis of Verified Dafny Methods](https://arxiv.org/pdf/2402.00247). LLMs
  may *propose* specifications and proofs here too, but only what `dafny verify` accepts counts.

## Running it locally

```bash
dotnet tool restore                 # installs the pinned Dafny CLI (see .config/dotnet-tools.json)
# Z3 4.12.1 must be on PATH; the z3-solver 4.12.1.0 wheel on PyPI ships the executable
# (see .github/workflows/verification.yml for the exact steps CI uses).

# Verify + recompile the Dafny-first core after editing Validation.dfy:
./src/Auctions.Domain.Verified/generate.sh

# Verify the models:
for f in verification/Dafny/*.dfy; do dotnet tool run dafny -- verify "$f"; done
```

Each `.dfy` file is self-contained (or pulls in its dependencies via `include`), so files are verified
one at a time. CI fails if the compiled verified core drifts from what is committed.

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

## Adding a verified function

1. Keep (or refactor) the logic as a **pure function over simple types** behind a small C# seam — see
   `Bid.Validate` and `TimedAscendingAuction.ValidateRaise` for the pattern.
2. Write the implementation and its specification in `src/Auctions.Domain.Verified/Validation.dfy` (or
   a sibling module). Prefer total functions and bounded newtypes so overflow is proved, and state the
   specification over mathematical integers. An LLM may *propose* the specification and proof, but only
   what the verifier accepts counts.
3. Run `generate.sh`, delegate the C# seam to the compiled code via `VerifiedCore`, and commit both the
   `.dfy` and the regenerated C# — CI fails on drift.
4. For state machines and algorithms, also add a model with invariant proofs under `verification/Dafny`
   (see `TimedAscending.dfy`): models capture properties of whole interaction sequences that a single
   function contract cannot.

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

- For the Dafny-first functions the verified artifact **is** the production code — no sync-by-convention
  gap remains. The state-machine proofs in `verification/Dafny`, however, are still models of the
  EF-mapped entity classes (`TimedAscendingAuction`, `SingleSealedBidAuction`); their invariants hold of
  the model, and the entities mirror it by convention.
- Candidate next targets: `Amount` arithmetic (same-currency preconditions) and the auction state
  computation; the natural next step is a verified functional core for the auction state machines that
  the entity classes delegate to, extending Dafny-first beyond leaf functions.
