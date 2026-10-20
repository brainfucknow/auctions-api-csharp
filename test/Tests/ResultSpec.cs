using Wallymathieu.Auctions;

namespace Wallymathieu.Auctions.Tests;

public class ResultSpec
{
    [Fact]
    public void ok_result_maps_and_matches_only_the_ok_value()
    {
        var result = Result.Ok<int, string>(21);

        var mapped = result.Select(value => value * 2);
        var mappedError = result.SelectError(error => error.Length);
        var matched = result.Match(value => $"ok:{value}", error => $"error:{error}");
        var observed = 0;
        result.Match(value => { observed = value; }, _ => { observed = -1; });

        Assert.True(result.IsOk);
        Assert.False(result.IsError);
        Assert.Equal("ok:21", matched);
        Assert.Equal(21, observed);
        Assert.Equal(42, mapped.Match(value => value, _ => -1));
        Assert.Equal(21, mappedError.Match(value => value, _ => -1));
    }

    [Fact]
    public void error_result_maps_and_matches_only_the_error_value()
    {
        var result = Result.Error<int, string>("failure");

        var mapped = result.Select(value => value * 2);
        var mappedError = result.SelectError(error => error.Length);
        var matched = result.Match(value => $"ok:{value}", error => $"error:{error}");
        var observed = string.Empty;
        result.Match(_ => { observed = "ok"; }, error => { observed = error; });

        Assert.False(result.IsOk);
        Assert.True(result.IsError);
        Assert.Equal("error:failure", matched);
        Assert.Equal("failure", observed);
        Assert.Equal("failure", mapped.Match(_ => string.Empty, error => error));
        Assert.Equal(7, mappedError.Match(_ => -1, error => error));
    }

    [Fact]
    public void mapping_requires_callbacks_for_both_result_cases()
    {
        var result = Result.Ok<int, string>(21);

        Assert.Throws<ArgumentNullException>(() => result.Select<string>(null!));
        Assert.Throws<ArgumentNullException>(() => result.SelectError<int>(null!));
        Assert.Throws<ArgumentNullException>(() => result.Match(null!, _ => { }));
        Assert.Throws<ArgumentNullException>(() => result.Match(_ => { }, null!));
        Assert.Throws<ArgumentNullException>(() => result.Match<string>(null!, _ => string.Empty));
        Assert.Throws<ArgumentNullException>(() => result.Match(_ => string.Empty, null!));
    }
}