using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using MFAToolsPlus.Helper;
using MFAToolsPlus.ViewModels.Pages;
using System;
using System.Timers;

namespace MFAToolsPlus.Views.Pages;

public partial class ToolsView : UserControl
{
    private bool _isSelecting;
    private Point _selectionStart;
    private Point _lastBrushPoint;

    public ToolsView()
    {
        DataContext = Instances.ToolsViewModel;
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }
    
    #region 实时图像

    private readonly Timer _liveViewTimer = new();
    private bool _liveViewTimerStarted;

    private void StartLiveViewLoop()
    {
        if (_liveViewTimerStarted)
            return;

        _liveViewTimer.Elapsed += OnLiveViewTimerElapsed;
        UpdateLiveViewTimerInterval();
        _liveViewTimer.Start();
        _liveViewTimerStarted = true;
    }

    public void StopLiveViewLoop()
    {
        if (!_liveViewTimerStarted)
            return;

        _liveViewTimer.Stop();
        _liveViewTimer.Elapsed -= OnLiveViewTimerElapsed;
        _liveViewTimerStarted = false;
    }

    private void UpdateLiveViewTimerInterval()
    {
        var interval = Instances.ToolsViewModel.GetLiveViewRefreshInterval();
        _liveViewTimer.Interval = Math.Max(1, interval * 1000);
    }
    private void OnLiveViewTimerElapsed(object? sender, EventArgs e)
    {
        try
        {
            if (MaaProcessor.IsClosed)
                return;
            if (Instances.ToolsViewModel.EnableLiveView && Instances.ToolsViewModel.IsConnected)
            {
                MaaProcessor.Instance.PostScreencap();
                var buffer = MaaProcessor.Instance.GetLiveViewBuffer();
                _ = Instances.ToolsViewModel.UpdateLiveViewImageAsync(buffer);
            }
            else
            {
                _ = Instances.ToolsViewModel.UpdateLiveViewImageAsync(null);
            }
        }
        catch
        {
            // ignored
        }
    }

    #endregion

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        StartLiveViewLoop();
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        StopLiveViewLoop();
    }

    private void OnLiveViewPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not ToolsViewModel vm || vm.ActiveToolMode == LiveViewToolMode.None)
        {
            return;
        }

        if (!vm.IsLiveViewPaused || vm.LiveViewDisplayImage == null)
        {
            return;
        }

        _isSelecting = true;
        _selectionStart = GetImagePoint(e.GetPosition(LiveViewViewer), vm);

        if (vm.ActiveToolMode == LiveViewToolMode.Screenshot && vm.IsScreenshotBrushMode)
        {
            _lastBrushPoint = _selectionStart;
            vm.UpdateScreenshotBrushPreview(_selectionStart);
            vm.StartScreenshotBrush(_selectionStart);
        }
        else if (vm.ActiveToolMode == LiveViewToolMode.Swipe)
        {
            vm.SetSwipeStart(_selectionStart);
        }
        else
        {
            vm.UpdateSelection(new Rect(_selectionStart, _selectionStart), true);
        }

        e.Pointer.Capture(LiveViewViewer);
        e.Handled = true;
    }

    private void OnLiveViewPointerMoved(object? sender, PointerEventArgs e)
    {
        if (DataContext is not ToolsViewModel vm)
        {
            return;
        }

        var current = GetImagePoint(e.GetPosition(LiveViewViewer), vm);
        if (vm.ActiveToolMode == LiveViewToolMode.Screenshot && vm.IsScreenshotBrushMode)
        {
            vm.UpdateScreenshotBrushPreview(current);
            if (_isSelecting)
            {
                vm.UpdateScreenshotBrush(current, _lastBrushPoint);
                _lastBrushPoint = current;
            }

            e.Handled = true;
            return;
        }

        if (!_isSelecting)
        {
            return;
        }

        var rect = NormalizeRect(_selectionStart, current);
        vm.UpdateSelection(rect, true);
        e.Handled = true;
    }

    private void OnLiveViewPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isSelecting || DataContext is not ToolsViewModel vm)
        {
            return;
        }

        _isSelecting = false;
        e.Pointer.Capture(null);

        var end = GetImagePoint(e.GetPosition(LiveViewViewer), vm);

        if (vm.ActiveToolMode == LiveViewToolMode.Screenshot && vm.IsScreenshotBrushMode)
        {
            vm.UpdateScreenshotBrushPreview(end);
            vm.UpdateScreenshotBrush(end, _lastBrushPoint);
            e.Handled = true;
            return;
        }

        if (vm.ActiveToolMode == LiveViewToolMode.Swipe)
        {
            vm.SetSwipeEnd(end);
            e.Handled = true;
            return;
        }

        var rect = NormalizeRect(_selectionStart, end);
        var hasSelection = rect.Width > 2 && rect.Height > 2;
        vm.UpdateSelection(rect, hasSelection);

        if (hasSelection)
        {
            switch (vm.ActiveToolMode)
            {
                case LiveViewToolMode.Roi:
                    vm.ApplySelection(rect);
                    break;
                case LiveViewToolMode.ColorPick:
                    vm.UpdateColorRangeFromSelection(rect);
                    vm.ApplySelectionForTool(rect);
                    break;
                case LiveViewToolMode.Screenshot:
                case LiveViewToolMode.Ocr:
                    vm.ApplySelectionForTool(rect);
                    break;
            }
        }

        e.Handled = true;
    }

    private static Rect NormalizeRect(Point start, Point end)
    {
        var x = Math.Min(start.X, end.X);
        var y = Math.Min(start.Y, end.Y);
        var w = Math.Abs(end.X - start.X);
        var h = Math.Abs(end.Y - start.Y);
        return new Rect(x, y, w, h);
    }

    private Point GetImagePoint(Point viewerPoint, ToolsViewModel vm)
    {
        if (vm.LiveViewDisplayImage == null)
        {
            return viewerPoint;
        }

        var viewerBounds = LiveViewViewer.Bounds;
        var scale = LiveViewViewer.Scale;
        var imageSize = vm.LiveViewDisplayImage.Size;

        var imageWidth = imageSize.Width;
        var imageHeight = imageSize.Height;

        var displayWidth = imageWidth * scale;
        var displayHeight = imageHeight * scale;

        var offsetX = (viewerBounds.Width - displayWidth) / 2 + LiveViewViewer.TranslateX;
        var offsetY = (viewerBounds.Height - displayHeight) / 2 + LiveViewViewer.TranslateY;

        var x = (viewerPoint.X - offsetX) / scale;
        var y = (viewerPoint.Y - offsetY) / scale;

        x = Math.Clamp(x, 0, imageWidth);
        y = Math.Clamp(y, 0, imageHeight);

        return new Point(Math.Round(x), Math.Round(y));
    }
}
