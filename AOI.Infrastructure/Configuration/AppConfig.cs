namespace AOI.Infrastructure.Configuration;

public class AppConfig
{
    public string MachineName { get; set; } = "AOI";

    public string Version { get; set; } = "1.0";

    public string DevicePluginPath { get; set; } = "plugins";

    public int TcpPort { get; set; } = 9000;
}