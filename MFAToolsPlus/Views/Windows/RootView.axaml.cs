using Avalonia;
using Avalonia.Controls;
using MFAToolsPlus.Configuration;
using MFAToolsPlus.Extensions;
using MFAToolsPlus.Extensions.MaaFW;
using MFAToolsPlus.Helper;
using SukiUI.Controls;
using System;
using System.IO;
using System.Threading.Tasks;

namespace MFAToolsPlus.Views;

public partial class RootView : SukiWindow
{
    private bool _isInitializing = true;
    public RootView()
    {
        _isInitializing = true;
        InitializeComponent();

        // 修改Loaded事件处理
        Loaded += (_, _) =>
        {
            LoggerHelper.Info("UI initialization started");

            // 确保在UI线程上执行
            DispatcherHelper.PostOnMainThread(() =>
            {
                // 初始化完成
                _isInitializing = false;

                // 加载UI
                LoadUI();
            });
        };
    }
    
    public void ShowWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }
    
    protected override void OnClosed(EventArgs e)
    {
        BeforeClosed();
        base.OnClosed(e);
    }
    
    public void BeforeClosed()
    {
        BeforeClosed(false);
    }

    public void BeforeClosed(bool noLog)
    {
        if (!GlobalHotkeyService.IsStopped)
        {
            MaaProcessor.Instance.Close();
            if (!noLog)
                LoggerHelper.Info("MFA Closed!");
            MaaProcessor.Instance.SetTasker();
            TrayIconManager.DisposeTrayIcon(Application.Current);

            if (!noLog)
                LoggerHelper.DisposeLogger();
            GlobalHotkeyService.Shutdown();
        }
    }

    public void LoadUI()
    {
        foreach (var rfile in Directory.EnumerateFiles(AppContext.BaseDirectory, "*.backupMFA", SearchOption.AllDirectories))
        {
            try
            {
                File.SetAttributes(rfile, FileAttributes.Normal);
                LoggerHelper.Info("Deleting file: " + rfile);
                File.Delete(rfile);
            }
            catch (Exception ex)
            {
                LoggerHelper.Error($"文件删除失败: {rfile}", ex);
            }
        }


        DispatcherHelper.RunOnMainThread(
            (Action)(async () =>
            {
                await Task.Delay(300);


                var controllerType = Instances.ToolsViewModel.CurrentController;
                var controllerKey = controllerType switch
                {
                    MaaControllerTypes.Adb => "Emulator",
                    MaaControllerTypes.Win32 => "Window",
                    MaaControllerTypes.PlayCover => "TabPlayCover",
                    MaaControllerTypes.Dbg => "TabDbg",
                    _ => "Window"
                };

                ToastHelper.Info("ConnectingTo".ToLocalizationFormatted(true, controllerKey));

                if (controllerType == MaaControllerTypes.PlayCover)
                {
                    Instances.ToolsViewModel.TryReadPlayCoverConfig();
                }
                else
                {
                    Instances.ToolsViewModel.TryReadAdbDeviceFromConfig();
                }


                await MaaProcessor.Instance.TestConnecting();

            }));

        TaskManager.RunTaskAsync(async () =>
        {
            await Task.Delay(1000);
            DispatcherHelper.RunOnMainThread(() =>
            {
                if (ConfigurationManager.Current.GetValue(ConfigurationKeys.AutoMinimize, false))
                {
                    WindowState = WindowState.Minimized;
                }
                if (ConfigurationManager.Current.GetValue(ConfigurationKeys.AutoHide, false))
                {
                    Hide();
                }
            });

            await Task.Delay(300);
        }, name: "公告和最新版本检测");
    }
}
