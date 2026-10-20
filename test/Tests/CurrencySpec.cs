using Wallymathieu.Auctions.DomainModels;

namespace Wallymathieu.Auctions.Tests;

public class CurrencySpec
{
    [Fact]
    public void currency_round_trips_through_text_and_code()
    {
        Assert.True(Currency.TryParse("DKK", out var parsed));
        Assert.NotNull(parsed);
        Assert.Equal("DKK", parsed.ToString());
        Assert.Equal(CurrencyCode.DKK, parsed.ToCurrencyCode());
        Assert.Equal(parsed, parsed.ToCurrency());

        Currency fromCode = CurrencyCode.DKK;
        CurrencyCode backToCode = fromCode;
        Assert.Equal(CurrencyCode.DKK, backToCode);
    }

    [Theory]
    [InlineData("USD")]
    [InlineData("dkk")]
    [InlineData("999")]
    public void invalid_currency_is_rejected(string text)
    {
        Assert.False(Currency.TryParse(text, out var currency));
        Assert.Null(currency);
    }
}