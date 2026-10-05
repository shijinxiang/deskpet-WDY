using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Point = System.Windows.Point;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using TranslatePet.Core.Models;
using TranslatePet.UI.Utils;

namespace TranslatePet.UI.Views;

public partial class SnippingOverlayWindow : Window
{
    private readonly byte[] _fullScreenBytes;
    private readonly ScreenRect _physicalBounds;
    private Point _startPoint;
    private bool _isSelecting;
    private Rect _selectedDipRect;
    private double _scaleX = 1.0;
    private double _scaleY = 1.0;

    public ScreenRect? SelectedPhysicalRegion { get; private set; }
    public Rect SelectedDipRect => _selectedDipRect;

    public SnippingOverlayWindow(byte[] fullScreenBytes, ScreenRect physicalBounds)
    {
        InitializeComponent();
        _fullScreenBytes = fullScreenBytes;
        _physicalBounds = physicalBounds;

        // 设置全屏跨监视器范围 (WPF DIP)
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 计算物理像素与 DIP 比例
        _scaleX = (double)_physicalBounds.Width / Width;
        _scaleY = (double)_physicalBounds.Height / Height;

        // 加载屏幕快照背景
        using var ms = new MemoryStream(_fullScreenBytes);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = ms;
        bitmap.EndInit();
        bitmap.Freeze();

        BackgroundImage.Source = bitmap;

        // 全屏暗色初始化
        UpdateMasks(new Rect(0, 0, 0, 0));
        Activate();
        Focus();
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Right)
        {
            CancelAndClose();
            return;
        }

        if (e.ChangedButton == MouseButton.Left)
        {
            if (e.ClickCount == 2 && _selectedDipRect.Width > 10 && _selectedDipRect.Height > 10)
            {
                // 双击选区直接确认
                ConfirmSelection();
                return;
            }

            _startPoint = e.GetPosition(RootGrid);
            _isSelecting = true;
            SelectionBorder.Visibility = Visibility.Visible;
            ActionBar.Visibility = Visibility.Collapsed;
            HintBanner.Visibility = Visibility.Collapsed;
            CaptureMouse();
        }
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isSelecting) return;

        var cur = e.GetPosition(RootGrid);
        var x = Math.Min(_startPoint.X, cur.X);
        var y = Math.Min(_startPoint.Y, cur.Y);
        var w = Math.Abs(cur.X - _startPoint.X);
        var h = Math.Abs(cur.Y - _startPoint.Y);

        _selectedDipRect = new Rect(x, y, w, h);
        UpdateMasks(_selectedDipRect);
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_isSelecting && e.ChangedButton == MouseButton.Left)
        {
            _isSelecting = false;
            ReleaseMouseCapture();

            if (_selectedDipRect.Width > 10 && _selectedDipRect.Height > 10)
            {
                // 选区有效，显示操作工具栏
                PositionActionBar();
            }
            else
            {
                // 选区太小，重置
                SelectionBorder.Visibility = Visibility.Collapsed;
                ActionBar.Visibility = Visibility.Collapsed;
                HintBanner.Visibility = Visibility.Visible;
                UpdateMasks(new Rect(0, 0, 0, 0));
            }
        }
    }

    private void UpdateMasks(Rect rect)
    {
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            MaskTop.Height = Height;
            MaskBottom.Height = 0;
            MaskLeft.Width = 0;
            MaskRight.Width = 0;
            return;
        }

        MaskTop.Height = Math.Max(0, rect.Y);
        MaskBottom.Height = Math.Max(0, Height - rect.Bottom);

        MaskLeft.Height = rect.Height;
        MaskLeft.Width = Math.Max(0, rect.X);
        MaskLeft.Margin = new Thickness(0, rect.Y, 0, 0);

        MaskRight.Height = rect.Height;
        MaskRight.Width = Math.Max(0, Width - rect.Right);
        MaskRight.Margin = new Thickness(0, rect.Y, 0, 0);

        Canvas.SetLeft(SelectionBorder, rect.X);
        Canvas.SetTop(SelectionBorder, rect.Y);
        SelectionBorder.Width = rect.Width;
        SelectionBorder.Height = rect.Height;
    }

    private void PositionActionBar()
    {
        var physW = (int)Math.Round(_selectedDipRect.Width * _scaleX);
        var physH = (int)Math.Round(_selectedDipRect.Height * _scaleY);
        SizeTextBlock.Text = $"{physW} × {physH} px";

        ActionBar.Visibility = Visibility.Visible;
        ActionBar.UpdateLayout();

        double barX = _selectedDipRect.Right - ActionBar.ActualWidth;
        if (barX < 10) barX = 10;
        if (barX + ActionBar.ActualWidth > Width - 10) barX = Width - ActionBar.ActualWidth - 10;

        double barY = _selectedDipRect.Bottom + 8;
        if (barY + ActionBar.ActualHeight > Height - 10)
        {
            barY = _selectedDipRect.Top - ActionBar.ActualHeight - 8;
            if (barY < 10) barY = 10;
        }

        Canvas.SetLeft(ActionBar, barX);
        Canvas.SetTop(ActionBar, barY);
    }

    private void ConfirmSelection()
    {
        if (_selectedDipRect.Width < 5 || _selectedDipRect.Height < 5)
        {
            CancelAndClose();
            return;
        }

        var physX = (int)Math.Round(_selectedDipRect.X * _scaleX);
        var physY = (int)Math.Round(_selectedDipRect.Y * _scaleY);
        var physW = (int)Math.Round(_selectedDipRect.Width * _scaleX);
        var physH = (int)Math.Round(_selectedDipRect.Height * _scaleY);

        SelectedPhysicalRegion = new ScreenRect(physX, physY, physW, physH);
        DialogResult = true;
        Close();
    }

    private void CancelAndClose()
    {
        SelectedPhysicalRegion = null;
        DialogResult = false;
        Close();
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            CancelAndClose();
        }
        else if (e.Key == Key.Enter)
        {
            if (_selectedDipRect.Width > 10 && _selectedDipRect.Height > 10)
            {
                ConfirmSelection();
            }
        }
    }

    private void OnConfirmClick(object sender, RoutedEventArgs e) => ConfirmSelection();
    private void OnCancelClick(object sender, RoutedEventArgs e) => CancelAndClose();
}
