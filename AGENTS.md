# Agent instructions

These instructions apply to planning, implementation, debugging, and review.

## Required context

Before planning or editing:

1. Read `CONTEXT.md`.
2. Read relevant ADRs under `docs/adr`.
3. Read any approved specification under `docs/specs`.
4. Read the applicable ticket under `docs/tickets`.
5. Inspect nearby production code and tests.

## General rules

- Use the project’s established domain vocabulary.
- Prefer existing public seams over introducing new abstractions.
- Test observable behavior through public interfaces.
- Do not modify generated files directly.
- Do not introduce dependencies without reporting them.
- Do not weaken or delete tests merely to make a change pass.
- Do not silently expand scope.
- Do not change public contracts unless the approved specification requires it.
- Treat failing tests as evidence to investigate.
- Do not commit, push, publish, or create pull requests unless explicitly
  requested.

## Verification

Before declaring completion:

1. Run formatting or style verification.
2. Run static analysis.
3. Compile or typecheck.
4. Run the narrowest relevant tests during implementation.
5. Run the broader repository test command before completion.
6. Report any command that could not be run.

## Completion report

Return:

- changed behavior
- changed files
- tests and commands executed
- assumptions
- unresolved risks
- deferred work
