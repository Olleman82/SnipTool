using System;
using System.Collections.Generic;
using System.IO;
using SnipTool.Models;

namespace SnipTool.Services;

public sealed class SessionManager
{
    private readonly AppSettings _settings;
    private readonly Stack<string> _history = new();
    private int _counter;
    private string? _sessionFolder;
    private DateTime _sessionStart;

    public SessionManager(AppSettings settings)
    {
        _settings = settings;
        StartNewSession();
    }

    public void StartNewSession()
    {
        _counter = 0;
        _history.Clear();
        _sessionStart = DateTime.Now;
        _sessionFolder = null;
    }

    public string GetNextFilePath(string extension)
    {
        EnsureSessionFolder();
        _counter++;

        var timestamp = DateTime.Now.ToString("HHmmss");
        var template = _settings.FileNameTemplate;
        if (!template.Contains("###", StringComparison.Ordinal))
        {
            template += "_###";
        }

        var fileName = template.Replace("HHmmss", timestamp, StringComparison.Ordinal)
            .Replace("###", _counter.ToString("D3"), StringComparison.Ordinal);

        return Path.Combine(_sessionFolder!, fileName + extension);
    }

    public void RegisterSavedFile(string path)
    {
        _history.Push(path);
    }

    public string? GetLastFile() => _history.Count > 0 ? _history.Peek() : null;

    public string? GetLastFolder() => _sessionFolder;

    public bool UndoLast()
    {
        if (_history.Count == 0)
        {
            return false;
        }

        var path = _history.Pop();
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void EnsureSessionFolder()
    {
        if (_sessionFolder != null)
        {
            return;
        }

        var dateFolder = Path.Combine(_settings.SaveRootPath, DateTime.Now.ToString("yyyy-MM-dd"));
        var sessionName = _sessionStart.ToString("HHmmss");
        _sessionFolder = Path.Combine(dateFolder, sessionName);
        Directory.CreateDirectory(_sessionFolder);
    }
}
