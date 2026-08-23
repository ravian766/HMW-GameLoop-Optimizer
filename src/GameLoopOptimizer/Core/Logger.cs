using System.Collections.Concurrent;
using System.IO;

namespace GameLoopOptimizer.Core;

public class LogEventArgs : EventArgs
{
    public DateTime Timestamp { get; }
    public string Level { get; }
    public string Module { get; }
    public string Message { get; }
    public string Formatted => $"[{Timestamp:HH:mm:ss}] [{Level.ToUpper()}] [{Module}] {Message}";

    public LogEventArgs(string level, string module, string message)
    {
        Timestamp = DateTime.Now;
        Level = level;
        Module = module;
        Message = message;
    }
}

public static class Logger
{
    private static readonly ConcurrentQueue<LogEventArgs> _logs = new();
    private static readonly object _fileLock = new();
    private static readonly string _logDir;
    private static readonly string _logFilePath;

    public static event EventHandler<LogEventArgs>? LogAdded;

    static Logger()
    {
        _logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GameLoopOptimizer", "logs");
        try
        {
            if (!Directory.Exists(_logDir))
            {
                Directory.CreateDirectory(_logDir);
            }
            _logFilePath = Path.Combine(_logDir, $"optimizer_{DateTime.Now:yyyyMMdd}.log");
        }
        catch
        {
            _logFilePath = Path.Combine(Path.GetTempPath(), "gameloop_optimizer.log");
        }
    }

    public static IReadOnlyList<LogEventArgs> GetAllLogs() => _logs.ToArray();

    public static void Info(string module, string message) => Log("INFO", module, message);
    public static void Warn(string module, string message) => Log("WARN", module, message);
    public static void Error(string module, string message) => Log("ERROR", module, message);
    public static void Success(string module, string message) => Log("SUCCESS", module, message);

    private static void Log(string level, string module, string message)
    {
        var entry = new LogEventArgs(level, module, message);
        _logs.Enqueue(entry);

        // Keep last 1000 in memory
        while (_logs.Count > 1000 && _logs.TryDequeue(out _)) { }

        try
        {
            lock (_fileLock)
            {
                File.AppendAllText(_logFilePath, entry.Formatted + Environment.NewLine);
            }
        }
        catch
        {
            // Ignore file write exceptions to avoid crashing logger
        }

        try
        {
            LogAdded?.Invoke(null, entry);
        }
        catch
        {
            // Ignore subscriber exceptions
        }
    }

    public static void Clear()
    {
        while (_logs.TryDequeue(out _)) { }
    }
}
