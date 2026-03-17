namespace AOI.Core.Extensions;

public static class ObjectExtensions
{
    public static T NotNull<T>(this T? obj, string name)
    {
        if (obj == null)
            throw new ArgumentNullException(name);

        return obj;
    }
}