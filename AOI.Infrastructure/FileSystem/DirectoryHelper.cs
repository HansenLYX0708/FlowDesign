namespace AOI.Infrastructure.FileSystem;

public static class DirectoryHelper
{
    public static void Ensure(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
    }

    public static IEnumerable<string> GetFiles(string path, string pattern)
    {
        if (!Directory.Exists(path))
            return Enumerable.Empty<string>();

        return Directory.GetFiles(path, pattern);
    }
}