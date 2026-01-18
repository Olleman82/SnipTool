using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;
using ScreenRecorderLib;

namespace SnipTool.Services;

public sealed class VideoCaptureService
{
    private Recorder? _recorder;
    private string? _currentPath;
    private readonly LogService? _log;
    private RecorderStatus _lastStatus = RecorderStatus.Idle;
    private int _stopToken;
    private bool _completionRaised;
    private readonly Stopwatch _stopwatch = new();

    public bool IsRecording { get; private set; }
    public bool IsPaused { get; private set; }
    public bool IsStopping { get; private set; }
    public string? CurrentPath => _currentPath;
    public string? LastCompletedPath { get; private set; }
    public TimeSpan Elapsed => _stopwatch.Elapsed;

    public event Action<bool>? RecordingStateChanged;
    public event Action<string>? RecordingCompleted;
    public event Action<string>? RecordingFailed;
    public event Action<RecorderStatus>? StatusChanged;

    public VideoCaptureService(LogService? log = null)
    {
        _log = log;
    }

    public bool StartFullscreenRecording(string path, bool includeAudio)
    {
        var source = DisplayRecordingSource.MainMonitor;
        if (source == null)
        {
            _log?.Warn("Video start failed: no main monitor source");
            return false;
        }

        source.IsBorderRequired = false;
        source.IsCursorCaptureEnabled = true;
        source.RecorderApi = RecorderApi.DesktopDuplication;
        return StartRecording(source, path, includeAudio);
    }

    public bool StartWindowRecording(IntPtr hwnd, string path, bool includeAudio)
    {
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }

        var source = new WindowRecordingSource
        {
            Handle = hwnd,
            IsBorderRequired = false,
            IsCursorCaptureEnabled = true
        };

        return StartRecording(source, path, includeAudio);
    }

    public bool StartRegionRecording(string deviceName, ScreenRect sourceRect, string path, bool includeAudio)
    {
        var source = new DisplayRecordingSource
        {
            DeviceName = deviceName,
            SourceRect = sourceRect,
            IsBorderRequired = false,
            IsCursorCaptureEnabled = true,
            RecorderApi = RecorderApi.DesktopDuplication
        };

        return StartRecording(source, path, includeAudio);
    }

    private bool StartRecording(RecordingSourceBase source, string path, bool includeAudio)
    {
        if (IsRecording)
        {
            _log?.Warn("Video start requested while already recording");
            return false;
        }

        try
        {
            LastCompletedPath = null;
            _completionRaised = false;
            IsStopping = false;
            var options = new RecorderOptions
            {
                SourceOptions = new SourceOptions
                {
                    RecordingSources = new List<RecordingSourceBase> { source }
                },
                AudioOptions = new AudioOptions
                {
                    IsAudioEnabled = includeAudio,
                    IsInputDeviceEnabled = includeAudio,
                    IsOutputDeviceEnabled = includeAudio
                },
                VideoEncoderOptions = new VideoEncoderOptions
                {
                    Framerate = 30,
                    Bitrate = 8_000_000,
                    IsFixedFramerate = false
                }
            };

            _recorder = Recorder.CreateRecorder(options);
            _recorder.OnRecordingComplete += OnRecordingComplete;
            _recorder.OnRecordingFailed += OnRecordingFailed;
            _recorder.OnStatusChanged += OnStatusChanged;

            _currentPath = path;
            _log?.Info($"Video recording start (async): {path} (audio={includeAudio})");
            
            _ = Task.Run(() => 
            {
                try 
                {
                    _recorder.Record(path);
                    _log?.Info("Internal recorder.Record() called");
                }
                catch (Exception ex)
                {
                    _log?.Error("Internal recorder.Record() failed", ex);
                    SafeRaiseFailed(ex.Message);
                }
            });

            IsRecording = true;
            IsPaused = false;
            _lastStatus = RecorderStatus.Recording;
            _stopwatch.Restart();
            SafeRaiseRecordingState(true);
            SafeRaiseStatus(RecorderStatus.Recording);
            return true;
        }
        catch (Exception ex)
        {
            _log?.Error("Video recording start failed", ex);
            RecordingFailed?.Invoke(ex.Message);
            CleanupRecorder();
            return false;
        }
    }

    public void StopRecording()
    {
        if (!IsRecording || _recorder == null || IsStopping)
        {
            _log?.Info($"StopRecording early return: IsRecording={IsRecording}, _recorder={_recorder != null}, IsStopping={IsStopping}");
            return;
        }

        _log?.Info("Video recording stop requested (async)");
        IsStopping = true;
        
        _ = Task.Run(() =>
        {
            try
            {
                _log?.Info("Calling _recorder.Stop() from task");
                _recorder.Stop();
                _log?.Info("_recorder.Stop() returned in task");
            }
            catch (Exception ex)
            {
                _log?.Error("_recorder.Stop() failed in task", ex);
                CleanupRecorder();
            }
        });

        var token = Interlocked.Increment(ref _stopToken);
        _ = Task.Run(async () =>
        {
            await Task.Delay(4000);
            if (IsRecording && token == _stopToken)
            {
                _log?.Warn("Video stop timeout; forcing cleanup");
                CleanupRecorder();
                if (!_completionRaised)
                {
                    SafeRaiseFailed("Recording stopped unexpectedly");
                }
            }
        });
    }

    public void Pause()
    {
        if (!IsRecording || _recorder == null || IsPaused)
        {
            return;
        }

        _log?.Info("Video recording pause requested (async)");
        _stopwatch.Stop();
        IsPaused = true;
        
        _ = Task.Run(() =>
        {
            try
            {
                _recorder.Pause();
            }
            catch (Exception ex)
            {
                _log?.Error("_recorder.Pause() failed", ex);
            }
        });
    }

    public void Resume()
    {
        if (!IsRecording || _recorder == null || !IsPaused)
        {
            return;
        }

        _log?.Info("Video recording resume requested (async)");
        _stopwatch.Start();
        IsPaused = false;
        
        _ = Task.Run(() =>
        {
            try
            {
                _recorder.Resume();
            }
            catch (Exception ex)
            {
                _log?.Error("_recorder.Resume() failed", ex);
            }
        });
    }

    public bool DeleteLastRecording()
    {
        if (IsRecording || string.IsNullOrWhiteSpace(LastCompletedPath))
        {
            return false;
        }

        try
        {
            if (System.IO.File.Exists(LastCompletedPath))
            {
                System.IO.File.Delete(LastCompletedPath);
                _log?.Info($"Deleted recording: {LastCompletedPath}");
            }
            LastCompletedPath = null;
            return true;
        }
        catch (Exception ex)
        {
            _log?.Error("Delete recording failed", ex);
            return false;
        }
    }

    private void OnRecordingComplete(object? sender, RecordingCompleteEventArgs e)
    {
        _log?.Info($"OnRecordingComplete called: FilePath={e.FilePath ?? _currentPath}");
        _completionRaised = true;
        CleanupRecorder();
        var path = e.FilePath ?? _currentPath ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(path))
        {
            LastCompletedPath = path;
        }
        SafeRaiseCompleted(path);
    }

    private void OnRecordingFailed(object? sender, RecordingFailedEventArgs e)
    {
        var error = e.Error ?? "Recording failed";
        _log?.Error($"Video recording failed: {error}");
        _completionRaised = true;
        CleanupRecorder();
        SafeRaiseFailed(error);
    }

    private void OnStatusChanged(object? sender, RecordingStatusEventArgs e)
    {
        if (e.Status == _lastStatus)
        {
            return;
        }

        _lastStatus = e.Status;
        IsPaused = e.Status == RecorderStatus.Paused;
        _log?.Info($"Video status: {e.Status}");
        SafeRaiseStatus(e.Status);

        if (e.Status == RecorderStatus.Idle)
        {
            try
            {
                if (!_completionRaised && !string.IsNullOrWhiteSpace(_currentPath) && System.IO.File.Exists(_currentPath))
                {
                    LastCompletedPath = _currentPath;
                    _completionRaised = true;
                    SafeRaiseCompleted(_currentPath);
                }
            }
            finally
            {
                CleanupRecorder();
            }
        }
    }

    private void CleanupRecorder()
    {
        if (_recorder == null && !IsRecording)
        {
            return; // Already cleaned up
        }

        if (_recorder != null)
        {
            _recorder.OnRecordingComplete -= OnRecordingComplete;
            _recorder.OnRecordingFailed -= OnRecordingFailed;
            _recorder.OnStatusChanged -= OnStatusChanged;
            _recorder.Dispose();
            _recorder = null;
        }

        if (IsRecording)
        {
            IsRecording = false;
            IsPaused = false;
            IsStopping = false;
            _stopwatch.Stop();
            _stopwatch.Reset();
            SafeRaiseRecordingState(false);
            SafeRaiseStatus(RecorderStatus.Idle);
        }

        _lastStatus = RecorderStatus.Idle;
        Interlocked.Increment(ref _stopToken);
    }

    private void SafeRaiseRecordingState(bool isRecording)
    {
        try
        {
            RecordingStateChanged?.Invoke(isRecording);
        }
        catch (Exception ex)
        {
            _log?.Error("RecordingStateChanged handler failed", ex);
        }
    }

    private void SafeRaiseCompleted(string path)
    {
        try
        {
            RecordingCompleted?.Invoke(path);
        }
        catch (Exception ex)
        {
            _log?.Error("RecordingCompleted handler failed", ex);
        }
    }

    private void SafeRaiseFailed(string error)
    {
        try
        {
            RecordingFailed?.Invoke(error);
        }
        catch (Exception ex)
        {
            _log?.Error("RecordingFailed handler failed", ex);
        }
    }

    private void SafeRaiseStatus(RecorderStatus status)
    {
        try
        {
            StatusChanged?.Invoke(status);
        }
        catch (Exception ex)
        {
            _log?.Error("StatusChanged handler failed", ex);
        }
    }
}
