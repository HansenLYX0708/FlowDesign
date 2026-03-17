using AOI.Core.Logging;
using AOI.Infrastructure.Serialization;
using System.Text.Json;

namespace AOI.Infrastructure.Configuration;

public static class ConfigManager
{
    private static readonly string ConfigFile = "appsettings.json";

    public static AppConfig Current { get; private set; } = new();

    public static void Load()
    {
        if (!File.Exists(ConfigFile))
        {
            Save();
            return;
        }

        Current = JsonSerializerEx.Load<AppConfig>(ConfigFile);

        Logger.Info("Config loaded");
    }

    public static void Save()
    {
        JsonSerializerEx.Save(ConfigFile, Current);

        Logger.Info("Config saved");
    }
}