# Auction API written in C\#

There are currently these main implementations:

- [main](https://github.com/wallymathieu/auctions-api-csharp/tree/main) is the core simple implementation
- [application-layer](https://github.com/wallymathieu/auctions-api-csharp/tree/application-layer) is the implementation with an application layer
- [command-handlers-infrastructure](https://github.com/wallymathieu/auctions-api-csharp/tree/command-handlers-infrastructure) is the implementation but without hand written command handlers that binds to the entity methods
- [command-handlers-mediatr](https://github.com/wallymathieu/auctions-api-csharp/tree/command-handlers-mediatr) is an extension of the command-handlers-infrastructure but with MediatR pipeline behavior instead of decorators

## Getting started

To build the apps run:

```bash
dotnet watch run --project ./src/Auctions.AppHost
```

## Auth

The API assumes that you have auth middleware in front of the app.

Either the decoded JWT in the `x-jwt-payload` header or specify an encoded claims principal by using configuration value in `PrincipalHeader`, such as `x-ms-client-principal`.

## Formal verification

The business-critical auction rules are machine-checked with [Dafny](https://dafny.org). Bid validation
and the English-auction raise policy are **Dafny-first**: their production implementation is compiled
from verified Dafny source in [src/Auctions.Domain.Verified](src/Auctions.Domain.Verified) (overflow
freedom included in the proofs). The auction state machines (strictly ascending bids, sealed-bid winner
selection) are additionally proved as models. See [verification/README.md](verification/README.md).

## Add migration

```bash
dotnet ef migrations add NewMigration --project ./src/Auctions.Infrastructure/Auctions.Infrastructure.csproj --startup-project ./src/Auctions.WebApi/Auctions.WebApi.csproj
```

## Inspiration

The main inspiration for the architecture of the API is found in this book:

- [Clean Architecture](https://www.goodreads.com/en/book/show/18043011)

Note that there are many variants of "the clean architecture" described in the .net space with different interpretations of what it means to implement this architecture.
