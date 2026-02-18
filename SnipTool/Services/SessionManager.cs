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
    private bool _isActive;

    public SessionManager(AppSettings settings)
    {
        _settings = settings;
        _sessionStart = DateTime.Now;
        _isActive = false;
    }

    public void StartNewSession()
    {
        _counter = 0;
        _history.Clear();
        _sessionStart = DateTime.Now;
        _sessionFolder = BuildSessionFolder();
        Directory.CreateDirectory(_sessionFolder);
        _isActive = true;
    }

    public void EndSession()
    {
        _counter = 0;
        _history.Clear();
        _sessionFolder = null;
        _isActive = false;
    }

    public bool IsActive => _isActive;

    public DateTime SessionStart => _sessionStart;

    public string? SessionFolder => _sessionFolder;

    public int Counter => _counter;

    public string GetNextFilePath(string extension)
    {
        if (_isActive)
        {
            EnsureSessionFolder();
        }

        _counter++;
        var fileName = BuildFileNameTemplate(_settings.FileNameTemplate);
        var targetFolder = _isActive ? _sessionFolder : _settings.SaveRootPath;
        if (string.IsNullOrWhiteSpace(targetFolder))
        {
            targetFolder = _settings.SaveRootPath;
        }

        Directory.CreateDirectory(targetFolder);
        return Path.Combine(targetFolder, fileName + extension);
    }

    public void RegisterSavedFile(string path)
    {
        _history.Push(path);
    }

    public string? GetLastFile() => _history.Count > 0 ? _history.Peek() : null;

    public string? GetLastFolder() => _sessionFolder ?? _settings.SaveRootPath;

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

        if (!_isActive)
        {
            return;
        }

        _sessionFolder = BuildSessionFolder();
        Directory.CreateDirectory(_sessionFolder);
    }

    private string BuildSessionFolder()
    {
        var dateFolder = Path.Combine(_settings.SaveRootPath, _sessionStart.ToString("yyyy-MM-dd"));
        var sessionName = _sessionStart.ToString("HHmmss");
        return Path.Combine(dateFolder, sessionName);
    }

    private string BuildFileNameTemplate(string template)
    {
        var timestamp = DateTime.Now.ToString("HHmmss");
        var date = DateTime.Now.ToString("yyyy-MM-dd");
        var counter = _counter.ToString("D3");

        if (template.Contains("HHmmss", StringComparison.Ordinal) || template.Contains("###", StringComparison.Ordinal))
        {
            if (!template.Contains("###", StringComparison.Ordinal))
            {
                template += "_###";
            }

            return template.Replace("HHmmss", timestamp, StringComparison.Ordinal)
                .Replace("###", counter, StringComparison.Ordinal);
        }

        if (!template.Contains("{counter}", StringComparison.Ordinal))
        {
            template += "_{counter}";
        }

        return template.Replace("{date}", date, StringComparison.Ordinal)
            .Replace("{time}", timestamp, StringComparison.Ordinal)
            .Replace("{counter}", counter, StringComparison.Ordinal);
    }
}
