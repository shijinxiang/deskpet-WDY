using TranslatePet.Core.Models;
using Xunit;

namespace TranslatePet.Tests;

public class ScreenRectTests
{
    [Fact]
    public void ScreenRect_ValidDimensions_CalculatesCorrectRightAndBottom()
    {
        var rect = new ScreenRect(100, 200, 300, 400);

        Assert.Equal(100, rect.X);
        Assert.Equal(200, rect.Y);
        Assert.Equal(300, rect.Width);
        Assert.Equal(400, rect.Height);
        Assert.Equal(400, rect.Right);
        Assert.Equal(600, rect.Bottom);
        Assert.False(rect.IsEmpty);
    }

    [Fact]
    public void ScreenRect_ZeroOrNegativeDimensions_IsEmptyTrue()
    {
        var zeroRect = new ScreenRect(10, 10, 0, 50);
        var negativeRect = new ScreenRect(10, 10, 50, -5);

        Assert.True(zeroRect.IsEmpty);
        Assert.True(negativeRect.IsEmpty);
    }

    [Fact]
    public void ScreenRect_NegativeCoordinates_SupportedForMultiMonitor()
    {
        var leftScreenRect = new ScreenRect(-1920, 0, 1920, 1080);

        Assert.Equal(-1920, leftScreenRect.X);
        Assert.Equal(0, leftScreenRect.Right);
        Assert.False(leftScreenRect.IsEmpty);
    }
}
