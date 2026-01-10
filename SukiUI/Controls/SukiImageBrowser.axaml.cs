using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using System;

namespace SukiUI.Controls;

public partial class SukiImageBrowser : Window
{
    private Bitmap? _pendingBitmap;
    private bool _awaitingLayout;

    public SukiImageBrowser()
    {
        InitializeComponent();
        Opened += OnOpened;
        ImageViewer.LayoutUpdated += OnLayoutUpdated;
    }

    public void SetImage(Bitmap? bitmap)
    {
        _pendingBitmap = bitmap;
        ApplyImage();
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        ApplyImage();
    }

    private void ApplyImage()
    {
        if (_pendingBitmap == null)
        {
            return;
        }

        if (!ImageViewer.IsLoaded)
        {
            ImageViewer.Loaded -= OnImageViewerLoaded;
            ImageViewer.Loaded += OnImageViewerLoaded;
            _awaitingLayout = true;
            return;
        }

        if (ImageViewer.Bounds.Width <= 0 || ImageViewer.Bounds.Height <= 0)
        {
            _awaitingLayout = true;
            return;
        }

        _awaitingLayout = false;
        ImageViewer.Source = null;
        ImageViewer.Source = _pendingBitmap;
        ImageViewer.MinScale = 0.1;
        ImageViewer.MaxScale = 10;
        ImageViewer.Scale = 1;
        ImageViewer.InvalidateMeasure();
        ImageViewer.InvalidateArrange();
        ImageViewer.InvalidateVisual();
    }

    private void OnImageViewerLoaded(object? sender, RoutedEventArgs e)
    {
        ImageViewer.Loaded -= OnImageViewerLoaded;
        ApplyImage();
    }

    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        if (!_awaitingLayout)
        {
            return;
        }

        ApplyImage();
    }
}

