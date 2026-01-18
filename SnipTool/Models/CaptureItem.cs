using System;
using System.Windows.Media;

namespace SnipTool.Models;

public sealed class CaptureItem
{
    public CaptureItem(string filePath, string fileName, string folderPath, DateTime capturedAt, ImageSource? thumbnail)
    {
        FilePath = filePath;
        FileName = fileName;
        FolderPath = folderPath;
        CapturedAt = capturedAt;
        Thumbnail = thumbnail;
        Extension = System.IO.Path.GetExtension(filePath).ToLowerInvariant();
    }

    public string FilePath { get; }
    public string FileName { get; }
    public string FolderPath { get; }
    public DateTime CapturedAt { get; }
    public ImageSource? Thumbnail { get; }
    public string Extension { get; }
    public bool IsVideo => Extension == ".mp4";

    public string FolderName => System.IO.Path.GetFileName(FolderPath.TrimEnd(System.IO.Path.DirectorySeparatorChar));
}
