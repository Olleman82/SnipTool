using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SnipTool.Models;

namespace SnipTool.Services;

public sealed class LibraryService
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".mp4"
    };

    public List<CaptureItem> LoadCaptures(string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
        {
            return new List<CaptureItem>();
        }

        var files = Directory.EnumerateFiles(rootPath, "*.*", SearchOption.AllDirectories)
            .Where(path => SupportedExtensions.Contains(Path.GetExtension(path)))
            .Select(path => new FileInfo(path))
            .OrderByDescending(info => info.CreationTimeUtc)
            .ToList();

        var items = new List<CaptureItem>(files.Count);
        foreach (var file in files)
        {
            var folder = file.Directory?.FullName ?? rootPath;
            var extension = Path.GetExtension(file.FullName).ToLowerInvariant();
            var thumbnail = extension == ".mp4"
                ? CreateVideoPlaceholder()
                : LoadThumbnail(file.FullName);

            items.Add(new CaptureItem(
                file.FullName,
                file.Name,
                folder,
                file.CreationTime,
                thumbnail));
        }

        return items;
    }

    public FolderNode BuildFolderTree(string rootPath)
    {
        var rootName = Path.GetFileName(rootPath.TrimEnd(Path.DirectorySeparatorChar));
        if (string.IsNullOrWhiteSpace(rootName))
        {
            rootName = rootPath;
        }

        var root = new FolderNode(rootName, rootPath);
        if (!Directory.Exists(rootPath))
        {
            return root;
        }

        BuildChildren(root, rootPath);
        return root;
    }

    private static void BuildChildren(FolderNode node, string path)
    {
        var directories = Directory.GetDirectories(path).OrderBy(dir => dir, StringComparer.OrdinalIgnoreCase);
        foreach (var dir in directories)
        {
            var name = Path.GetFileName(dir);
            var child = new FolderNode(name, dir);
            node.Children.Add(child);
            BuildChildren(child, dir);
        }
    }

    private static ImageSource? LoadThumbnail(string filePath)
    {
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            image.DecodePixelWidth = 240;
            image.UriSource = new Uri(filePath);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }

    private static ImageSource CreateVideoPlaceholder()
    {
        const int width = 240;
        const int height = 140;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(System.Windows.Media.Color.FromRgb(20, 24, 36)), null, new Rect(0, 0, width, height));
            var triangle = new StreamGeometry();
            using (var ctx = triangle.Open())
            {
                ctx.BeginFigure(new System.Windows.Point(92, 50), true, true);
                ctx.LineTo(new System.Windows.Point(92, 90), true, false);
                ctx.LineTo(new System.Windows.Point(132, 70), true, false);
            }
            triangle.Freeze();
            dc.DrawGeometry(new SolidColorBrush(System.Windows.Media.Color.FromRgb(88, 164, 255)), null, triangle);
        }

        var bmp = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(visual);
        bmp.Freeze();
        return bmp;
    }
}
