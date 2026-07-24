# Repository context

## Purpose

Auctions API is a .NET-based auction management system with API, domain,
infrastructure, migration, service-default, app-host, frontend, and automated
integration-test projects. It supports auction-oriented business capabilities
through HTTP APIs and supporting services.

## Domain glossary

### Auction

A listing or sale process managed by the system.

### Domain

Business rules and state transitions that should remain independent from
infrastructure concerns.

### Application

Use-case coordination and orchestration around the domain model.

### Infrastructure

Persistence, external integrations, and implementation details that support the
application and domain layers.

## Invariants

- Domain rules should remain isolated from infrastructure implementation details.
- Public HTTP contracts should remain backward compatible unless an approved
  specification explicitly changes them.
- Database migrations and generated contracts are change-sensitive and should be
  reviewed carefully.
- Tests should verify observable behavior through public interfaces.

## Architecture

- `src/Auctions.Domain`: business rules and state transitions.
- `src/Auctions.Infrastructure`: persistence and external integrations.
- `src/Auctions.WebApi`: external HTTP contracts and API composition.
- `src/Auctions.MigrationService`: database migration execution.
- `src/Auctions.ServiceDefaults`: shared service defaults.
- `src/Auctions.AppHost`: local orchestration host.
- `src/Auctions.Frontend`: user-facing frontend.
- `test/Tests`: unit, integration, architecture, and end-to-end tests.

## Dependency rules

- Domain must not depend on Infrastructure.
- Application behavior may depend on Domain abstractions.
- Infrastructure implements application or domain-facing interfaces.
- API must not contain business rules.

## Generated code

No generated directories are currently documented. If generated code is added,
record its location and regeneration command here.

## Important commands

```bash
# Restore or install dependencies
dotnet restore auctions-api.sln

# Build or typecheck
dotnet build auctions-api.sln --no-restore

# Unit tests
dotnet test test/Tests/Tests.csproj --no-build --filter "Category!=Integration"

# Integration tests
dotnet test test/Tests/Tests.csproj --no-build --filter "Category=Integration"

# Complete test suite
dotnet test auctions-api.sln --no-build

# Format verification
dotnet format auctions-api.sln --verify-no-changes --no-restore

# Static analysis
dotnet build auctions-api.sln --no-restore -warnaserror
```

## Change-sensitive areas

- authentication and authorization
- money or billing
- concurrency
- database migrations
- compatibility-sensitive APIs
- generated contracts
- external integrations
