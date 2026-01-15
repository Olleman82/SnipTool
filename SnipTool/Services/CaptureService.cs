using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace SnipTool.Services;

public sealed class CaptureService
{
    public Bitmap CaptureRectangle(Rectangle rect)
    {
        var bmp = new Bitmap(rect.Width, rect.Height, PixelFormat.Format32bppArgb);
        using var gfx = Graphics.FromImage(bmp);
        gfx.CopyFromScreen(rect.Left, rect.Top, 0, 0, rect.Size, CopyPixelOperation.SourceCopy);
        return bmp;
    }

    public void SaveBitmap(Bitmap bmp, string path)
    {
        var ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
        var format = ext == ".jpg" || ext == ".jpeg" ? ImageFormat.Jpeg : ImageFormat.Png;
        bmp.Save(path, format);
    }

    public void CopyToClipboard(Bitmap bmp)
    {
        var hBitmap = bmp.GetHbitmap();
        try
        {
            var source = Imaging.CreateBitmapSourceFromHBitmap(hBitmap, IntPtr.Zero, Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            System.Windows.Clipboard.SetImage(source);
        }
        finally
        {
            DeleteObject(hBitmap);
        }
    }

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);
}
