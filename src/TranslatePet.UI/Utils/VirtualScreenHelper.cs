using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using TranslatePet.Core.Models;

namespace TranslatePet.UI.Utils;

public static class VirtualScreenHelper
{
    public static ScreenRect GetVirtualScreenBounds()
    {
        int x = NativeMethods.GetSystemMetrics(NativeMethods.SM_XVIRTUALSCREEN);
        int y = NativeMethods.GetSystemMetrics(NativeMethods.SM_YVIRTUALSCREEN);
        int width = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXVIRTUALSCREEN);
        int height = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYVIRTUALSCREEN);

        return new ScreenRect(x, y, width, height);
    }

    public static Bitmap CaptureVirtualScreenBitmap()
    {
        var bounds = GetVirtualScreenBounds();
        var bmp = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);

        using var g = Graphics.FromImage(bmp);
        // CopyFromScreen handles multi-monitor negative coordinates and DPI correctly
        g.CopyFromScreen(bounds.X, bounds.Y, 0, 0, new System.Drawing.Size(bounds.Width, bounds.Height), CopyPixelOperation.SourceCopy);

        return bmp;
    }

    public static byte[] BitmapToBytes(Bitmap bmp, ImageFormat format)
    {
        using var ms = new MemoryStream();
        bmp.Save(ms, format);
        return ms.ToArray();
    }

    public static Bitmap CropBitmap(Bitmap src, ScreenRect localRegion)
    {
        // localRegion is relative to the captured bitmap (0, 0 to Width, Height)
        var clampedX = Math.Max(0, Math.Min(localRegion.X, src.Width - 1));
        var clampedY = Math.Max(0, Math.Min(localRegion.Y, src.Height - 1));
        var clampedWidth = Math.Max(1, Math.Min(localRegion.Width, src.Width - clampedX));
        var clampedHeight = Math.Max(1, Math.Min(localRegion.Height, src.Height - clampedY));

        var cropRect = new Rectangle(clampedX, clampedY, clampedWidth, clampedHeight);
        var target = new Bitmap(clampedWidth, clampedHeight, PixelFormat.Format32bppArgb);

        using (var g = Graphics.FromImage(target))
        {
            g.DrawImage(src, new Rectangle(0, 0, clampedWidth, clampedHeight), cropRect, GraphicsUnit.Pixel);
        }

        return target;
    }
}
