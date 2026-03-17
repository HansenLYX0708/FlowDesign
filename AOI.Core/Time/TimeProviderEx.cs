namespace AOI.Core.Time;

public static class TimeProviderEx
{
    public static DateTime Now => DateTime.Now;

    public static long Timestamp => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}