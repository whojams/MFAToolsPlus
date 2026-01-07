using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using MFAToolsPlus.Helper;
using System;
using System.Timers;

namespace MFAToolsPlus.Views.Pages;

public partial class ToolsView : UserControl
{
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
}

