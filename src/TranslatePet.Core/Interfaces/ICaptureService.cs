using TranslatePet.Core.Models;

namespace TranslatePet.Core.Interfaces;

public interface ICaptureService
{
    /// <summary>
    /// 截取全虚拟桌面（支持跨多屏、负坐标、高DPI）
    /// </summary>
    Task<byte[]> CaptureVirtualScreenAsync();

    /// <summary>
    /// 根据虚拟桌面物理坐标裁剪指定矩形区域并返回图片字节数据（PNG/JPEG）
    /// </summary>
    Task<byte[]> CropRegionAsync(byte[] fullScreenBytes, ScreenRect region);
}
