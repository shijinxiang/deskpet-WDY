using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace TranslatePet.UI.Utils;

public static class ImageProcessor
{
    public static void RemoveBackground(string inputPath, string outputPath)
    {
        if (!File.Exists(inputPath)) return;

        using var src = new Bitmap(inputPath);
        int w = src.Width;
        int h = src.Height;

        using var dest = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        var srcData = src.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var destData = dest.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

        int stride = srcData.Stride;
        byte[] srcBytes = new byte[stride * h];
        byte[] destBytes = new byte[stride * h];

        Marshal.Copy(srcData.Scan0, srcBytes, 0, srcBytes.Length);
        Array.Copy(srcBytes, destBytes, srcBytes.Length);

        bool[,] isBg = new bool[w, h];
        var queue = new Queue<System.Drawing.Point>();

        for (int x = 0; x < w; x++)
        {
            if (CheckWhite(srcBytes, x, 0, stride)) { isBg[x, 0] = true; queue.Enqueue(new System.Drawing.Point(x, 0)); }
            if (CheckWhite(srcBytes, x, h - 1, stride)) { isBg[x, h - 1] = true; queue.Enqueue(new System.Drawing.Point(x, h - 1)); }
        }
        for (int y = 0; y < h; y++)
        {
            if (CheckWhite(srcBytes, 0, y, stride) && !isBg[0, y]) { isBg[0, y] = true; queue.Enqueue(new System.Drawing.Point(0, y)); }
            if (CheckWhite(srcBytes, w - 1, y, stride) && !isBg[w - 1, y]) { isBg[w - 1, y] = true; queue.Enqueue(new System.Drawing.Point(w - 1, y)); }
        }

        int[] dx = { 0, 0, 1, -1, 1, -1, 1, -1 };
        int[] dy = { 1, -1, 0, 0, 1, 1, -1, -1 };

        while (queue.Count > 0)
        {
            var p = queue.Dequeue();
            for (int i = 0; i < 8; i++)
            {
                int nx = p.X + dx[i];
                int ny = p.Y + dy[i];
                if (nx >= 0 && nx < w && ny >= 0 && ny < h && !isBg[nx, ny])
                {
                    if (CheckWhite(srcBytes, nx, ny, stride))
                    {
                        isBg[nx, ny] = true;
                        queue.Enqueue(new System.Drawing.Point(nx, ny));
                    }
                }
            }
        }

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int idx = y * stride + x * 4;
                if (isBg[x, y])
                {
                    destBytes[idx + 3] = 0; // Transparent
                }
                else
                {
                    // Smooth edges
                    bool edge = false;
                    for (int i = 0; i < 4; i++)
                    {
                        int nx = x + dx[i];
                        int ny = y + dy[i];
                        if (nx >= 0 && nx < w && ny >= 0 && ny < h && isBg[nx, ny])
                        {
                            edge = true;
                            break;
                        }
                    }
                    if (edge)
                    {
                        byte b = srcBytes[idx];
                        byte g = srcBytes[idx + 1];
                        byte r = srcBytes[idx + 2];
                        if (r > 230 && g > 230 && b > 230)
                        {
                            destBytes[idx + 3] = (byte)Math.Max(0, 255 - ((r + g + b) / 3 - 230) * 8);
                        }
                    }
                }
            }
        }

        Marshal.Copy(destBytes, 0, destData.Scan0, destBytes.Length);
        src.UnlockBits(srcData);
        dest.UnlockBits(destData);

        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        dest.Save(outputPath, ImageFormat.Png);
    }

    private static bool CheckWhite(byte[] bytes, int x, int y, int stride)
    {
        int idx = y * stride + x * 4;
        byte b = bytes[idx];
        byte g = bytes[idx + 1];
        byte r = bytes[idx + 2];
        return r > 238 && g > 238 && b > 238;
    }
}
