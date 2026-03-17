namespace AOI.Core.Utils;

public static class Guard
{
    public static void NotNull(object? obj, string name)
    {
        if (obj == null)
            throw new ArgumentNullException(name);
    }

    public static void NotEmpty(string? str, string name)
    {
        if (string.IsNullOrWhiteSpace(str))
            throw new ArgumentException(name);
    }
}