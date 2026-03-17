namespace AOI.Core.Logging;

public class ConsoleLogger : ILogger
{
    private readonly object _lock = new();

    public void Log(LogLevel level, string message)
    {
        lock (_lock)
        {
            Console.WriteLine(
                $"[{DateTime.Now:HH:mm:ss}] [{level}] {message}");
        }
    }

    public void Log(LogLevel level, Exception ex, string message)
    {
        lock (_lock)
        {
            Console.WriteLine(
                $"[{DateTime.Now:HH:mm:ss}] [{level}] {message} {ex}");
        }
    }

    public void Info(string message)
        => Log(LogLevel.Info, message);

    public void Warn(string message)
        => Log(LogLevel.Warn, message);

    public void Error(string message)
        => Log(LogLevel.Error, message);

    public void Debug(string message)
        => Log(LogLevel.Debug, message);
}