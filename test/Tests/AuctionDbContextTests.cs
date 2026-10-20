using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Wallymathieu.Auctions.Commands;
using Wallymathieu.Auctions.DomainModels;
using Wallymathieu.Auctions.Infrastructure.Data;
using Wallymathieu.Auctions.Infrastructure.Services;
using Wallymathieu.Auctions.Services;
using Wallymathieu.Auctions.Tests.Helpers;

namespace Wallymathieu.Auctions.Tests;

public class AuctionDbContextTests
{
    [Fact]
    public void AuctionVersion_ShouldBeDatabaseGeneratedRowVersion()
    {
        var options = new DbContextOptionsBuilder<AuctionDbContext>()
            .UseSqlServer("Server=localhost;Database=auctions;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        using var context = new AuctionDbContext(options);

        var version = context.Model.FindEntityType(typeof(Auction))!
            .FindProperty(nameof(Auction.Version))!;

        Assert.Equal(typeof(byte[]), version.ClrType);
        Assert.True(version.IsConcurrencyToken);
        Assert.Equal(ValueGenerated.OnAddOrUpdate, version.ValueGenerated);
        Assert.Equal("rowversion", version.GetRelationalTypeMapping().StoreType);
    }

    [Fact]
    public async Task SaveChangesAsync_ShouldGenerateVersionForSqlite()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        var options = new DbContextOptionsBuilder<AuctionDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var context = new AuctionDbContext(options);
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var auction = GetEnglishAuction();
        context.Add(auction);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.NotEmpty(auction.Version);
        var initialVersion = auction.Version;
        auction.EndsAt = EndsAt.AddMinutes(1);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.NotEqual(initialVersion, auction.Version);
    }
    [Fact]
    public async Task CreateBid_ShouldRejectStaleAuctionVersion()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        var options = new DbContextOptionsBuilder<AuctionDbContext>()
            .UseSqlite(connection)
            .Options;
        await using (var setupContext = new AuctionDbContext(options))
        {
            await setupContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
            setupContext.Add(VickreyAuction);
            await setupContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var firstContext = new AuctionDbContext(options);
        await using var staleContext = new AuctionDbContext(options);
        _ = await staleContext.GetAuction(TestData.AuctionId, TestContext.Current.CancellationToken);
        var firstAuction = await firstContext.GetAuction(TestData.AuctionId, TestContext.Current.CancellationToken);
        Assert.NotNull(firstAuction);
        Assert.True(firstAuction.TryAddBid(Bid1.At, Bid1, out _));
        firstContext.Entry(firstAuction).State = EntityState.Modified;
        await firstContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        using var serviceProvider = new ServiceCollection()
            .AddSingleton(staleContext)
            .AddSingleton<IUserContext>(new StubUserContext(Buyer2))
            .AddSingleton<ISystemClock>(new FakeSystemClock(Bid2.At))
            .AddAuctionServicesNoCache()
            .BuildServiceProvider();
        var handler = serviceProvider.GetRequiredService<ICreateBidCommandHandler>();

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            handler.Handle(new CreateBidCommand(Bid2.Amount, TestData.AuctionId),
                TestContext.Current.CancellationToken));
    }

    private sealed class StubUserContext(UserId userId) : IUserContext
    {
        public UserId? UserId { get; } = userId;
    }
}