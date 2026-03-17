namespace AOI.Core.Logging;

public static class Logger
{
    private static ILogger _logger = new ConsoleLogger();

    public static void SetLogger(ILogger logger)
    {
        _logger = logger;
    }

    public static void Info(string message)
        => _logger.Info(message);

    public static void Warn(string message)
        => _logger.Warn(message);

    public static void Error(string message)
        => _logger.Error(message);

    public static void Debug(string message)
        => _logger.Debug(message);

    public static void Error(Exception ex, string message)
        => _logger.Log(LogLevel.Error, ex, message);
}