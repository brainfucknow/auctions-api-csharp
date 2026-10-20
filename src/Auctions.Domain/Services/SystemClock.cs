namespace Wallymathieu.Auctions.Services;

/// <summary>
/// This is the concrete implementation of <see cref="ISystemClock"/>.
/// </summary>
public class SystemClock : ISystemClock
{
    public DateTimeOffset Now => DateTimeOffset.UtcNow;
}