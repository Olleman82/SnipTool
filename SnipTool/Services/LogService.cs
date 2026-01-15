using System;
using System.IO;

namespace SnipTool.Services;

public sealed class LogService
{
    private readonly string _logPath;
    private readonly object _lock = new();

    public LogService()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SnipTool", "logs");
        Directory.CreateDirectory(dir);
        _logPath = Path.Combine(dir, "app.log");
    }

    public void Info(string message)
    {
        Write("INFO", message);
    }

    public void Warn(string message)
    {
        Write("WARN", message);
    }

    public void Error(string message, Exception? ex = null)
    {
        Write("ERROR", ex == null ? message : $"{message} | {ex}");
    }

    private void Write(string level, string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}";
        lock (_lock)
        {
            File.AppendAllText(_logPath, line + Environment.NewLine);
        }
    }
}
