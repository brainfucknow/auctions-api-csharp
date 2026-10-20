using Wallymathieu.Auctions.DomainModels;

namespace Wallymathieu.Auctions.Tests;

public class AmountSpec
{
    [Fact]
    public void amount_round_trips_through_its_text_representation()
    {
        var amount = new Amount(125, CurrencyCode.SEK);

        Assert.Equal(new Amount(0, CurrencyCode.SEK), Amount.Zero(CurrencyCode.SEK));
        Assert.Equal("SEK125", amount.ToString());
        Assert.Equal(amount, Amount.Parse(amount.ToString()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("SEK")]
    [InlineData("125")]
    [InlineData("USD125")]
    [InlineData("prefixSEK125")]
    [InlineData("SEK125suffix")]
    [InlineData("SEK9223372036854775808")]
    public void invalid_amount_text_is_rejected(string text)
    {
        Assert.False(Amount.TryParse(text, out var amount));
        Assert.Null(amount);
    }

    [Fact]
    public void parse_reports_invalid_text()
    {
        var exception = Assert.Throws<ArgumentException>(() => Amount.Parse("not-an-amount"));

        Assert.StartsWith("Could not parse value", exception.Message, StringComparison.Ordinal);
        Assert.Equal("s", exception.ParamName);
    }

    [Fact]
    public void arithmetic_preserves_currency()
    {
        var left = new Amount(15, CurrencyCode.DKK);
        var right = new Amount(4, CurrencyCode.DKK);

        Assert.Equal(new Amount(19, CurrencyCode.DKK), left + right);
        Assert.Equal(new Amount(11, CurrencyCode.DKK), left - right);
    }

    [Fact]
    public void arithmetic_between_different_currencies_is_rejected()
    {
        var sek = new Amount(15, CurrencyCode.SEK);
        var dkk = new Amount(4, CurrencyCode.DKK);

        Assert.Throws<ArgumentException>(() => sek + dkk);
        Assert.Throws<ArgumentException>(() => sek - dkk);
    }

    [Fact]
    public void comparisons_use_the_amount_value()
    {
        var lower = new Amount(4, CurrencyCode.SEK);
        var higher = new Amount(15, CurrencyCode.SEK);

        Assert.True(lower < higher);
        Assert.True(lower <= higher);
        Assert.True(higher > lower);
        Assert.True(higher >= lower);
        Assert.Equal(-1, lower.CompareTo(higher));
        Assert.Equal(0, lower.CompareTo(lower));
        Assert.Equal(1, lower.CompareTo(null));
    }

    [Fact]
    public void comparisons_between_different_currencies_are_rejected()
    {
        var sek = new Amount(4, CurrencyCode.SEK);
        var dkk = new Amount(15, CurrencyCode.DKK);

        Assert.Throws<ArgumentException>(() => sek < dkk);
        Assert.Throws<ArgumentException>(() => sek <= dkk);
        Assert.Throws<ArgumentException>(() => sek > dkk);
        Assert.Throws<ArgumentException>(() => sek >= dkk);
    }
}