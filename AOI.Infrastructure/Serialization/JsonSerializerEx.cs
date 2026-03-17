using System.Text.Json;

namespace AOI.Infrastructure.Serialization;

public static class JsonSerializerEx
{
    private static readonly JsonSerializerOptions Options =
        new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

    public static void Save<T>(string file, T obj)
    {
        var json = JsonSerializer.Serialize(obj, Options);

        File.WriteAllText(file, json);
    }

    public static T Load<T>(string file)
    {
        var json = File.ReadAllText(file);

        return JsonSerializer.Deserialize<T>(json, Options)!;
    }
}