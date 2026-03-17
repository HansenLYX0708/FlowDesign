namespace AOI.Core.Logging;

public interface ILogger
{
    void Log(LogLevel level, string message);

    void Log(LogLevel level, Exception ex, string message);

    void Info(string message);

    void Warn(string message);

    void Error(string message);

    void Debug(string message);
}