namespace Wallymathieu.Auctions;

public interface IResult
{
    bool IsOk { get; }
    bool IsError { get; }
}