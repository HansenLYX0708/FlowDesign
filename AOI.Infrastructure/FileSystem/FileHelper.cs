namespace AOI.Infrastructure.FileSystem;

public static class FileHelper
{
    public static void EnsureFile(string path)
    {
        if (!File.Exists(path))
        {
            File.Create(path).Dispose();
        }
    }

    public static string ReadText(string path)
    {
        return File.ReadAllText(path);
    }

    public static void WriteText(string path, string content)
    {
        File.WriteAllText(path, content);
    }
}