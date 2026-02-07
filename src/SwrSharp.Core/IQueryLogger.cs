namespace SwrSharp.Core;

/// <summary>
/// Logger interface for BlazorQuery operations.
/// Implement this interface to integrate with your logging framework.
/// </summary>
public interface IQueryLogger
{
    /// <summary>
    /// Log a debug message.
    /// </summary>
    void LogDebug(string message, params object[] args);

    /// <summary>
    /// Log an information message.
    /// </summary>
    void LogInformation(string message, params object[] args);

    /// <summary>
    /// Log a warning message.
    /// </summary>
    void LogWarning(string message, params object[] args);

    /// <summary>
    /// Log an error message with exception.
    /// </summary>
    void LogError(Exception exception, string message, params object[] args);

    /// <summary>
    /// Log a critical error message with exception.
    /// </summary>
    void LogCritical(Exception exception, string message, params object[] args);
}

/// <summary>
/// Default no-op logger that does nothing.
/// Use this when logging is not needed.
/// </summary>
public class NullQueryLogger : IQueryLogger
{
    public static readonly IQueryLogger Instance = new NullQueryLogger();

    private NullQueryLogger() { }

    public void LogDebug(string message, params object[] args) { }
    public void LogInformation(string message, params object[] args) { }
    public void LogWarning(string message, params object[] args) { }
    public void LogError(Exception exception, string message, params object[] args) { }
    public void LogCritical(Exception exception, string message, params object[] args) { }
}

/// <summary>
/// Console logger for debugging purposes.
/// </summary>
public class ConsoleQueryLogger : IQueryLogger
{
    public void LogDebug(string message, params object[] args)
    {
        Console.WriteLine($"[DEBUG] {string.Format(message, args)}");
    }

    public void LogInformation(string message, params object[] args)
    {
        Console.WriteLine($"[INFO] {string.Format(message, args)}");
    }

    public void LogWarning(string message, params object[] args)
    {
        Console.WriteLine($"[WARN] {string.Format(message, args)}");
    }

    public void LogError(Exception exception, string message, params object[] args)
    {
        Console.WriteLine($"[ERROR] {string.Format(message, args)}");
        Console.WriteLine($"Exception: {exception}");
    }

    public void LogCritical(Exception exception, string message, params object[] args)
    {
        Console.WriteLine($"[CRITICAL] {string.Format(message, args)}");
        Console.WriteLine($"Exception: {exception}");
    }
}

