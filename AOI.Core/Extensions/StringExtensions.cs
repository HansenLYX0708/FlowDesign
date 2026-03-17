namespace AOI.Core.Extensions;

public static class StringExtensions
{
    public static bool IsNullOrEmpty(this string? str)
    {
        return string.IsNullOrWhiteSpace(str);
    }
}