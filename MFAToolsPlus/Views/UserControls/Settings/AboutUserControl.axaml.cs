using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using MFAToolsPlus.Extensions;
using MFAToolsPlus.Helper;
using MFAToolsPlus.Views.Windows;
using System;
using System.IO;

namespace MFAToolsPlus.Views.UserControls.Settings;

public partial class AboutUserControl : UserControl
{
    public AboutUserControl()
    {
        InitializeComponent();

    }
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

    
    private void ClearCache_Click(object? sender, RoutedEventArgs e)
    {
        if (!Instances.RootViewModel.Idle)
        {
            ToastHelper.Warn(
                LangKeys.Warning.ToLocalization(),
                LangKeys.StopTaskBeforeClearCache.ToLocalization());
            return;
        }

        MaaProcessor.Instance.SetTasker();

        var baseDirectory = AppContext.BaseDirectory;
        var debugDirectory = Path.Combine(baseDirectory, "debug");
        var logsDirectory = Path.Combine(baseDirectory, "logs");

        try
        {
            ClearDirectory(debugDirectory);
            ClearDirectory(logsDirectory);
            Directory.CreateDirectory(debugDirectory);
            Directory.CreateDirectory(logsDirectory);
            ToastHelper.Success(LangKeys.ClearCacheSuccess.ToLocalization());
        }
        catch (Exception ex)
        {
            LoggerHelper.Error($"清理缓存失败: {ex.Message}");
            ToastHelper.Error(LangKeys.ClearCacheFailed.ToLocalization(), ex.Message);
        }
    }
    
    private static void ClearDirectory(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            return;
        }

        foreach (var entry in Directory.EnumerateFileSystemEntries(directoryPath))
        {
            try
            {
                if (Directory.Exists(entry))
                {
                    Directory.Delete(entry, true);
                }
                else
                {
                    File.Delete(entry);
                }
            }
            catch (Exception ex)
            {
                LoggerHelper.Warning($"清理缓存项失败: {entry}, {ex.Message}");
            }
        }
    }
}

