using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using TranslatePet.Core.Interfaces;
using TranslatePet.Core.Models;
using TranslatePet.UI.Utils;

namespace TranslatePet.UI.Services;

public class WpfCaptureService : ICaptureService
{
    public Task<byte[]> CaptureVirtualScreenAsync()
    {
        return Task.Run(() =>
        {
            using var bmp = VirtualScreenHelper.CaptureVirtualScreenBitmap();
            return VirtualScreenHelper.BitmapToBytes(bmp, ImageFormat.Png);
        });
    }

    public Task<byte[]> CropRegionAsync(byte[] fullScreenBytes, ScreenRect region)
    {
        return Task.Run(() =>
        {
            using var ms = new MemoryStream(fullScreenBytes);
            using var fullBmp = new Bitmap(ms);

            using var cropped = VirtualScreenHelper.CropBitmap(fullBmp, region);
            return VirtualScreenHelper.BitmapToBytes(cropped, ImageFormat.Png);
        });
    }
}
