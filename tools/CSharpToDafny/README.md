# CSharpToDafny

Roslyn-based extraction tool: discovers `[Verify]`-annotated C# methods and generates Dafny skeletons
(signatures plus `Contract.Requires`/`Contract.Ensures` translated to `requires`/`ensures`) into
`verification/Generated`.

```bash
dotnet run --project tools/CSharpToDafny -- --source src/Auctions.Domain --output verification/Generated
```

Run it from the repository root so the source paths recorded in the generated files are stable; CI
regenerates and fails on drift.

This tool is bespoke because no Microsoft or otherwise maintained C# → Dafny translator exists (Dafny's
own toolchain compiles the *other* way, Dafny → C#). It deliberately translates only the tractable
slice — signatures and contracts of pure, static methods over simple types — and leaves implementations
to humans/LLMs with `dafny verify` as the gate. See `verification/README.md` ("Why a bespoke extraction
tool?") for the full rationale, the supported translation subset, and the workflow.
