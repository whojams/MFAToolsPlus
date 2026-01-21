using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using System;
using System.Collections.Generic;

namespace SukiUI.Controls;

public partial class SukiImageBrowser : Window
{
    private readonly List<Bitmap?> _images = new();
    private Bitmap? _pendingBitmap;
    private bool _awaitingLayout;
    private int _currentIndex;
    private double? _pendingScale;
    private double? _pendingTranslateX;
    private double? _pendingTranslateY;

    public SukiImageBrowser()
    {
        InitializeComponent();
        Opened += OnOpened;
        ImageViewer.LayoutUpdated += OnLayoutUpdated;
    }

    public void SetImages(IEnumerable<Bitmap?>? bitmaps)
    {
        _images.Clear();
        if (bitmaps != null)
        {
            foreach (var bitmap in bitmaps)
            {
                if (bitmap != null)
                {
                    _images.Add(bitmap);
                }
            }
        }

        _currentIndex = 0;
        if (_images.Count == 0)
        {
            SetPendingImage(null, preserveTransform: false);
            UpdateNavigationVisibility();
            return;
        }

        SetPendingImage(_images[_currentIndex], preserveTransform: false);
        UpdateNavigationVisibility();
    }
    
    public void SetImage(Bitmap? bitmap)
    {
        _images.Clear();
        _currentIndex = 0;
        UpdateNavigationVisibility();
        SetPendingImage(bitmap, preserveTransform: false);
    }

    private void OnPrevClick(object? sender, RoutedEventArgs e)
    {
        if (_images.Count == 0 || _currentIndex <= 0)
        {
            return;
        }

        _currentIndex--;
        SetPendingImage(_images[_currentIndex], preserveTransform: true);
        UpdateNavigationVisibility();
    }

    private void OnNextClick(object? sender, RoutedEventArgs e)
    {
        if (_images.Count == 0 || _currentIndex >= _images.Count - 1)
        {
            return;
        }

        _currentIndex++;
        SetPendingImage(_images[_currentIndex], preserveTransform: true);
        UpdateNavigationVisibility();
    }

    private void SetPendingImage(Bitmap? bitmap, bool preserveTransform)
    {
        _pendingBitmap = bitmap;
        if (preserveTransform)
        {
            _pendingScale = ImageViewer.Scale;
            _pendingTranslateX = ImageViewer.TranslateX;
            _pendingTranslateY = ImageViewer.TranslateY;
        }
        else
        {
            _pendingScale = null;
            _pendingTranslateX = null;
            _pendingTranslateY = null;
        }

        ApplyImage();
    }

    private void UpdateNavigationVisibility()
    {
        PrevButton.IsVisible = _images.Count > 0 && _currentIndex > 0;
        NextButton.IsVisible = _images.Count > 0 && _currentIndex < _images.Count - 1;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        ApplyImage();
    }

    private void ApplyImage()
    {
        if (_pendingBitmap == null)
        {
            _awaitingLayout = false;
            ImageViewer.Source = null;
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
        var scale = _pendingScale;
        var translateX = _pendingTranslateX;
        var translateY = _pendingTranslateY;
        _pendingScale = null;
        _pendingTranslateX = null;
        _pendingTranslateY = null;

        ImageViewer.Source = null;
        ImageViewer.Source = _pendingBitmap;
        ImageViewer.MinScale = 0.1;
        ImageViewer.MaxScale = 10;

        if (scale.HasValue)
        {
            ImageViewer.Scale = Math.Clamp(scale.Value, ImageViewer.MinScale, ImageViewer.MaxScale);
            if (translateX.HasValue)
            {
                ImageViewer.TranslateX = translateX.Value;
            }

            if (translateY.HasValue)
            {
                ImageViewer.TranslateY = translateY.Value;
            }
        }
        else
        {
            ImageViewer.Scale = 1;
        }

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

