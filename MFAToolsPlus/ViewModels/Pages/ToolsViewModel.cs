using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaaFramework.Binding;
using MaaFramework.Binding.Buffers;
using MFAToolsPlus.Helper.Converters;
using MFAToolsPlus.Configuration;
using MFAToolsPlus.Extensions;
using MFAToolsPlus.Extensions.MaaFW;
using MFAToolsPlus.Helper;
using MFAToolsPlus.ViewModels.UsersControls;
using Newtonsoft.Json;
using SukiUI.Dialogs;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MFAToolsPlus.ViewModels.Pages;

public enum LiveViewToolMode
{
    None,
    Roi,
    ColorPick,
    Swipe,
    Ocr,
    Screenshot
}

public enum LiveViewRoiSelectionType
{
    OriginRoi,
    OriginTarget,
    TargetRoi
}

public partial class ToolsViewModel : ViewModelBase
{
    [ObservableProperty] private ObservableCollection<object> _devices = [];
    [ObservableProperty] private object? _currentDevice;

    [ObservableProperty] private MaaControllerTypes _currentController =
        ConfigurationManager.Current.GetValue(ConfigurationKeys.CurrentController, MaaControllerTypes.Adb, MaaControllerTypes.None, new UniversalEnumConverter<MaaControllerTypes>());

    /// <summary>
    /// 控制器选项列表
    /// </summary>
    [ObservableProperty] private ObservableCollection<MaaInterface.MaaResourceController> _controllerOptions = [];

    /// <summary>
    /// 当前选中的控制器
    /// </summary>
    [ObservableProperty] private MaaInterface.MaaResourceController? _selectedController;

    partial void OnSelectedControllerChanged(MaaInterface.MaaResourceController? value)
    {
        if (value != null && value.ControllerType != CurrentController)
        {
            CurrentController = value.ControllerType;
        }
    }
    partial void OnCurrentDeviceChanged(object? value)
    {
        ChangedDevice(value);
    }
    private DateTime? _lastExecutionTime;

    public void ChangedDevice(object? value)
    {
        var igoreToast = false;
        if (value != null)
        {
            var now = DateTime.Now;
            if (_lastExecutionTime == null)
            {
                _lastExecutionTime = now;
            }
            else
            {
                if (now - _lastExecutionTime < TimeSpan.FromSeconds(2))
                    igoreToast = true;
                else
                    _lastExecutionTime = now;
            }
        }
        if (value is DesktopWindowInfo window)
        {
            if (!igoreToast) ToastHelper.Info(LangKeys.WindowSelectionMessage.ToLocalizationFormatted(false, ""), window.Name);
            MaaProcessor.Config.DesktopWindow.Name = window.Name;
            MaaProcessor.Config.DesktopWindow.HWnd = window.Handle;
            MaaProcessor.Instance.SetTasker();
        }
        else if (value is AdbDeviceInfo device)
        {
            if (!igoreToast) ToastHelper.Info(LangKeys.EmulatorSelectionMessage.ToLocalizationFormatted(false, ""), device.Name);
            MaaProcessor.Config.AdbDevice.Name = device.Name;
            MaaProcessor.Config.AdbDevice.AdbPath = device.AdbPath;
            MaaProcessor.Config.AdbDevice.AdbSerial = device.AdbSerial;
            MaaProcessor.Config.AdbDevice.Config = device.Config;
            MaaProcessor.Config.AdbDevice.Info = device;
            MaaProcessor.Instance.SetTasker();
            ConfigurationManager.Current.SetValue(ConfigurationKeys.AdbDevice, device);
        }
    }
    protected override void Initialize()
    {
        InitializeControllerOptions();
    }
    /// <summary>
    /// 初始化控制器列表
    /// 从MaaInterface.Controller加载，如果为空则使用默认的Adb和Win32
    /// </summary>
    public void InitializeControllerOptions()
    {
        try
        {

            // 使用默认的Adb和Win32控制器
            var defaultControllers = CreateDefaultControllers();
            ControllerOptions = new ObservableCollection<MaaInterface.MaaResourceController>(defaultControllers);


            // 根据当前控制器类型选择对应的控制器
            SelectedController = ControllerOptions.FirstOrDefault(c => c.ControllerType == CurrentController)
                ?? ControllerOptions.FirstOrDefault();
        }
        catch (Exception e)
        {
            LoggerHelper.Error(e);
            // 出错时使用默认控制器
            var defaultControllers = CreateDefaultControllers();
            ControllerOptions = new ObservableCollection<MaaInterface.MaaResourceController>(defaultControllers);
            SelectedController = ControllerOptions.FirstOrDefault();
        }
    }
    /// <summary>
    /// 创建默认的Adb和Win32控制器
    /// </summary>
    private List<MaaInterface.MaaResourceController> CreateDefaultControllers()
    {
        var adbController = new MaaInterface.MaaResourceController
        {
            Name = "Adb",
            Type = MaaControllerTypes.Adb.ToJsonKey()
        };
        adbController.InitializeDisplayName();
        List<MaaInterface.MaaResourceController> controllers = [adbController];
        if (OperatingSystem.IsWindows())
        {
            var win32Controller = new MaaInterface.MaaResourceController
            {
                Name = "Win32",
                Type = MaaControllerTypes.Win32.ToJsonKey()
            };
            win32Controller.InitializeDisplayName();
            controllers.Add(win32Controller);
        }
        if (OperatingSystem.IsMacOS())
        {
            var playCoverController = new MaaInterface.MaaResourceController
            {
                Name = "PlayCover",
                Type = MaaControllerTypes.PlayCover.ToJsonKey()
            };
            playCoverController.InitializeDisplayName();
            controllers.Add(playCoverController);
        }
        return controllers;
    }


    [ObservableProperty] private bool _isConnected;
    public void SetConnected(bool isConnected)
    {
        // 使用异步投递避免从非UI线程修改属性时导致死锁
        DispatcherHelper.PostOnMainThread(() => IsConnected = isConnected);
    }

    [RelayCommand]
    private void CustomAdb()
    {
        var deviceInfo = CurrentDevice as AdbDeviceInfo;

        Instances.DialogManager.CreateDialog().WithTitle("AdbEditor").WithViewModel(dialog => new AdbEditorDialogViewModel(deviceInfo, dialog)).Dismiss().ByClickingBackground().TryShow();
    }

    [RelayCommand]
    private void EditPlayCover()
    {
        Instances.DialogManager.CreateDialog().WithTitle("PlayCoverEditor")
            .WithViewModel(dialog => new PlayCoverEditorDialogViewModel(MaaProcessor.Config.PlayCover, dialog))
            .Dismiss().ByClickingBackground().TryShow();
    }

    private CancellationTokenSource? _refreshCancellationTokenSource;

    [RelayCommand]
    private async Task Reconnect()
    {
        if (CurrentController != MaaControllerTypes.PlayCover && CurrentDevice == null)
        {
            ToastHelper.Warn(LangKeys.CannotStart.ToLocalization(), "DeviceNotSelected".ToLocalization());
            LoggerHelper.Warning(LangKeys.CannotStart.ToLocalization());
            return;
        }

        if (CurrentController == MaaControllerTypes.Adb
            && CurrentDevice is AdbDeviceInfo adbInfo
            && string.IsNullOrWhiteSpace(adbInfo.AdbSerial))
        {
            ToastHelper.Warn(LangKeys.CannotStart.ToLocalization(), LangKeys.AdbAddressEmpty.ToLocalization());
            LoggerHelper.Warning(LangKeys.CannotStart.ToLocalization());
            return;
        }

        if (CurrentController == MaaControllerTypes.PlayCover
            && string.IsNullOrWhiteSpace(MaaProcessor.Config.PlayCover.PlayCoverAddress))
        {
            ToastHelper.Warn(LangKeys.CannotStart.ToLocalization(), LangKeys.PlayCoverAddressEmpty.ToLocalization());
            LoggerHelper.Warning(LangKeys.CannotStart.ToLocalization());
            return;
        }

        try
        {
            using var tokenSource = new CancellationTokenSource();
            await MaaProcessor.Instance.ReconnectAsync(tokenSource.Token);
            await MaaProcessor.Instance.TestConnecting();
        }
        catch (Exception ex)
        {
            LoggerHelper.Warning($"Reconnect failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private void Refresh()
    {
        if (CurrentController == MaaControllerTypes.PlayCover)
        {
            SetConnected(false);
            return;
        }

        _refreshCancellationTokenSource?.Cancel();
        _refreshCancellationTokenSource = new CancellationTokenSource();
        var controllerType = CurrentController;
        TaskManager.RunTask(() => AutoDetectDevice(_refreshCancellationTokenSource.Token), _refreshCancellationTokenSource.Token, name: "刷新", handleError: (e) => HandleDetectionError(e, controllerType),
            catchException: true, shouldLog: true);
    }
    public void AutoDetectDevice(CancellationToken token = default)
    {
        if (CurrentController == MaaControllerTypes.PlayCover)
        {
            DispatcherHelper.RunOnMainThread(() =>
            {
                Devices = [];
                CurrentDevice = null;
            });
            SetConnected(false);
            return;
        }

        var controllerType = CurrentController;
        var isAdb = controllerType == MaaControllerTypes.Adb;

        ToastHelper.Info(GetDetectionMessage(controllerType));
        SetConnected(false);
        token.ThrowIfCancellationRequested();
        var (devices, index) = isAdb ? DetectAdbDevices() : DetectWin32Windows();
        token.ThrowIfCancellationRequested();
        UpdateDeviceList(devices, index);
        token.ThrowIfCancellationRequested();
        HandleControllerSettings(controllerType);
        token.ThrowIfCancellationRequested();
        UpdateConnectionStatus(devices.Count > 0, controllerType);
    }

    private string GetDetectionMessage(MaaControllerTypes controllerType) =>
        controllerType == MaaControllerTypes.Adb
            ? "EmulatorDetectionStarted".ToLocalization()
            : "WindowDetectionStarted".ToLocalization();

    private (ObservableCollection<object> devices, int index) DetectAdbDevices()
    {
        var devices = MaaProcessor.Toolkit.AdbDevice.Find();
        var index = CalculateAdbDeviceIndex(devices);
        return (new(devices), index);
    }

    private int CalculateAdbDeviceIndex(IList<AdbDeviceInfo> devices)
    {
        if (CurrentDevice is AdbDeviceInfo info)
        {
            LoggerHelper.Info($"Current device: {JsonConvert.SerializeObject(info)}");

            // 使用指纹匹配设备
            var matchedDevices = devices
                .Where(device => device.MatchesFingerprint(info))
                .ToList();

            LoggerHelper.Info($"Found {matchedDevices.Count} devices matching fingerprint");

            // 多匹配时排序：先比AdbSerial前缀（冒号前），再比设备名称
            if (matchedDevices.Any())
            {
                matchedDevices.Sort((a, b) =>
                {
                    var aPrefix = a.AdbSerial.Split(':', 2)[0];
                    var bPrefix = b.AdbSerial.Split(':', 2)[0];
                    int prefixCompare = string.Compare(aPrefix, bPrefix, StringComparison.Ordinal);
                    return prefixCompare != 0 ? prefixCompare : string.Compare(a.Name, b.Name, StringComparison.Ordinal);
                });
                return devices.IndexOf(matchedDevices.First());
            }
        }

        var config = ConfigurationManager.Current.GetValue(ConfigurationKeys.EmulatorConfig, string.Empty);
        if (string.IsNullOrWhiteSpace(config)) return 0;

        var targetNumber = ExtractNumberFromEmulatorConfig(config);
        return devices.Select((d, i) =>
                TryGetIndexFromConfig(d.Config, out var index) && index == targetNumber ? i : -1)
            .FirstOrDefault(i => i >= 0);
    }


    public static int ExtractNumberFromEmulatorConfig(string emulatorConfig)
    {
        var match = Regex.Match(emulatorConfig, @"\d+");

        if (match.Success)
        {
            return int.Parse(match.Value);
        }

        return 0;
    }
    private (ObservableCollection<object> devices, int index) DetectWin32Windows()
    {
        Thread.Sleep(500);
        var windows = MaaProcessor.Toolkit.Desktop.Window.Find().Where(win => !string.IsNullOrWhiteSpace(win.Name)).ToList();
        var (index, filtered) = CalculateWindowIndex(windows);
        return (new(filtered), index);
    }

    private (int index, List<DesktopWindowInfo> afterFiltered) CalculateWindowIndex(List<DesktopWindowInfo> windows)
    {
        MaaInterface.MaaResourceController? controller = null;

        if (controller?.Win32 == null)
            return (windows.FindIndex(win => !string.IsNullOrWhiteSpace(win.Name)), windows);

        var filtered = windows.Where(win =>
            !string.IsNullOrWhiteSpace(win.Name)).ToList();

        filtered = ApplyRegexFilters(filtered, controller.Win32);
        return (filtered.Count > 0 ? filtered.IndexOf(filtered.First()) : 0, filtered.ToList());
    }


    private List<DesktopWindowInfo> ApplyRegexFilters(List<DesktopWindowInfo> windows, MaaInterface.MaaResourceControllerWin32 win32)
    {
        var filtered = windows;
        if (!string.IsNullOrWhiteSpace(win32.WindowRegex))
        {
            var regex = new Regex(win32.WindowRegex);
            filtered = filtered.Where(w => regex.IsMatch(w.Name)).ToList();
        }

        if (!string.IsNullOrWhiteSpace(win32.ClassRegex))
        {
            var regex = new Regex(win32.ClassRegex);
            filtered = filtered.Where(w => regex.IsMatch(w.ClassName)).ToList();
        }
        return filtered;
    }

    private void UpdateDeviceList(ObservableCollection<object> devices, int index)
    {
        DispatcherHelper.RunOnMainThread(() =>
        {
            Devices = devices;
            if (devices.Count > index)
                CurrentDevice = devices[index];
            else
                CurrentDevice = null;
        });
    }

    private bool TryGetIndexFromConfig(string configJson, out int index)
    {
        index = DeviceDisplayConverter.GetFirstEmulatorIndex(configJson);
        return index != -1;
    }

    public void TryReadAdbDeviceFromConfig(bool inTask = true, bool refresh = false)
    {
        if (CurrentController == MaaControllerTypes.PlayCover)
        {
            SetConnected(false);
            return;
        }

        if (refresh
            || CurrentController != MaaControllerTypes.Adb
            || !ConfigurationManager.Current.GetValue(ConfigurationKeys.RememberAdb, true)
            || MaaProcessor.Config.AdbDevice.AdbPath != "adb"
            || !ConfigurationManager.Current.TryGetValue(ConfigurationKeys.AdbDevice, out AdbDeviceInfo savedDevice,
                new UniversalEnumConverter<AdbInputMethods>(), new UniversalEnumConverter<AdbScreencapMethods>()))
        {
            _refreshCancellationTokenSource?.Cancel();
            _refreshCancellationTokenSource = new CancellationTokenSource();
            if (inTask)
                TaskManager.RunTask(() => AutoDetectDevice(_refreshCancellationTokenSource.Token), name: "刷新设备");
            else
                AutoDetectDevice(_refreshCancellationTokenSource.Token);
            return;
        }
        // 检查是否启用指纹匹配功能
        var useFingerprintMatching = ConfigurationManager.Current.GetValue(ConfigurationKeys.UseFingerprintMatching, false);

        if (useFingerprintMatching)
        {
            // // 使用指纹匹配设备，而不是直接使用保存的设备信息
            // // 因为雷电模拟器等的AdbSerial每次启动都会变化
            // LoggerHelper.Info("Reading saved ADB device from configuration, using fingerprint matching.");
            // LoggerHelper.Info($"Saved device fingerprint: {savedDevice.GenerateDeviceFingerprint()}");
            //
            // // 搜索当前可用的设备
            // var currentDevices = MaaProcessor.Toolkit.AdbDevice.Find();
            //
            // // 尝试通过指纹匹配找到对应的设备（当任一方index为-1时不比较index）
            // AdbDeviceInfo? matchedDevice = null;
            // foreach (var device in currentDevices)
            // {
            //     if (device.MatchesFingerprint(savedDevice))
            //     {
            //         matchedDevice = device;
            //         LoggerHelper.Info($"Found matching device by fingerprint: {device.Name} ({device.AdbSerial})");
            //         break;
            //     }
            // }
            //
            // if (matchedDevice != null)
            // {
            //     // 使用新搜索到的设备信息（AdbSerial等可能已更新）
            //     DispatcherHelper.RunOnMainThread(() =>
            //     {
            //         Devices = new ObservableCollection<object>(currentDevices);
            //         CurrentDevice = matchedDevice;
            //     });
            //     ChangedDevice(matchedDevice);
            // }
            // else
            // {
            //     // 没有找到匹配的设备，执行自动检测
            //     LoggerHelper.Info("No matching device found by fingerprint, performing auto detection.");
            //     _refreshCancellationTokenSource?.Cancel();
            //     _refreshCancellationTokenSource = new CancellationTokenSource();
            //     if (inTask)
            //         TaskManager.RunTask(() => AutoDetectDevice(_refreshCancellationTokenSource.Token), name: "刷新设备");
            //     else
            //         AutoDetectDevice(_refreshCancellationTokenSource.Token);
            // }
        }
        else
        {
            // 不使用指纹匹配，直接使用保存的设备信息
            LoggerHelper.Info("Reading saved ADB device from configuration, fingerprint matching disabled.");
            DispatcherHelper.RunOnMainThread(() =>
            {
                Devices = [savedDevice];
                CurrentDevice = savedDevice;
            });
            ChangedDevice(savedDevice);
        }
    }

    private void HandleControllerSettings(MaaControllerTypes controllerType)
    {
        if (controllerType == MaaControllerTypes.PlayCover)
            return;

        MaaInterface.MaaResourceController? controller = null;

        if (controller == null) return;

        var isAdb = controllerType == MaaControllerTypes.Adb;
        HandleInputSettings(controller, isAdb);
        HandleScreenCapSettings(controller, isAdb);
    }

    private void HandleInputSettings(MaaInterface.MaaResourceController controller, bool isAdb)
    {
        if (isAdb)
        {
            var input = controller.Adb?.Input;
            if (input == null) return;
            Instances.ConnectSettingsUserControlModel.AdbControlInputType = input switch
            {
                1 => AdbInputMethods.AdbShell,
                2 => AdbInputMethods.MinitouchAndAdbKey,
                4 => AdbInputMethods.Maatouch,
                8 => AdbInputMethods.EmulatorExtras,
                _ => Instances.ConnectSettingsUserControlModel.AdbControlInputType
            };
        }
        else
        {
            var mouse = controller.Win32?.Mouse;
            if (mouse != null)
            {
                var parsed = ParseWin32InputMethod(mouse);
                if (parsed != null)
                    Instances.ConnectSettingsUserControlModel.Win32ControlMouseType = parsed.Value;
            }
            var keyboard = controller.Win32?.Keyboard;
            if (keyboard != null)
            {
                var parsed = ParseWin32InputMethod(keyboard);
                if (parsed != null)
                    Instances.ConnectSettingsUserControlModel.Win32ControlKeyboardType = parsed.Value;
            }
            var input = controller.Win32?.Input;
            if (keyboard == null && mouse == null && input != null)
            {
                var parsed = ParseWin32InputMethod(input);
                if (parsed != null)
                {
                    Instances.ConnectSettingsUserControlModel.Win32ControlKeyboardType = parsed.Value;
                    Instances.ConnectSettingsUserControlModel.Win32ControlMouseType = parsed.Value;
                }
            }
        }
    }

    /// <summary>
    /// 解析 Win32InputMethod，支持旧版 long 格式和新版 string 格式
    /// </summary>
    private static Win32InputMethod? ParseWin32InputMethod(object? value)
    {
        if (value == null) return null;

        // 新版 string 格式（枚举名）
        if (value is string strValue)
        {
            if (Enum.TryParse<Win32InputMethod>(strValue, ignoreCase: true, out var result))
                return result;
            return null;
        }

        // 旧版 long 格式
        var longValue = Convert.ToInt64(value);
        return longValue switch
        {
            1 => Win32InputMethod.Seize,
            2 => Win32InputMethod.SendMessage,
            4 => Win32InputMethod.PostMessage,
            8 => Win32InputMethod.LegacyEvent,
            16 => Win32InputMethod.PostThreadMessage,
            32 => Win32InputMethod.SendMessageWithCursorPos,
            64 => Win32InputMethod.PostMessageWithCursorPos,
            _ => null
        };
    }

    /// <summary>
    /// 解析 Win32ScreencapMethod，支持旧版 long 格式和新版 string 格式
    /// </summary>
    private static Win32ScreencapMethod? ParseWin32ScreencapMethod(object? value)
    {
        if (value == null) return null;

        // 新版 string 格式（枚举名）
        if (value is string strValue)
        {
            if (Enum.TryParse<Win32ScreencapMethod>(strValue, ignoreCase: true, out var result))
                return result;
            return null;
        }

        // 旧版 long 格式
        var longValue = Convert.ToInt64(value);
        return longValue switch
        {
            1 => Win32ScreencapMethod.GDI,
            2 => Win32ScreencapMethod.FramePool,
            4 => Win32ScreencapMethod.DXGI_DesktopDup,
            8 => Win32ScreencapMethod.DXGI_DesktopDup_Window,
            16 => Win32ScreencapMethod.PrintWindow,
            32 => Win32ScreencapMethod.ScreenDC,
            _ => null
        };
    }

    private void HandleScreenCapSettings(MaaInterface.MaaResourceController controller, bool isAdb)
    {
        if (isAdb)
        {
            var screenCap = controller.Adb?.ScreenCap;
            if (screenCap == null) return;
            Instances.ConnectSettingsUserControlModel.AdbControlScreenCapType = screenCap switch
            {
                1 => AdbScreencapMethods.EncodeToFileAndPull,
                2 => AdbScreencapMethods.Encode,
                4 => AdbScreencapMethods.RawWithGzip,
                8 => AdbScreencapMethods.RawByNetcat,
                16 => AdbScreencapMethods.MinicapDirect,
                32 => AdbScreencapMethods.MinicapStream,
                64 => AdbScreencapMethods.EmulatorExtras,
                _ => Instances.ConnectSettingsUserControlModel.AdbControlScreenCapType
            };
        }
        else
        {
            var screenCap = controller.Win32?.ScreenCap;
            if (screenCap == null) return;
            var parsed = ParseWin32ScreencapMethod(screenCap);
            if (parsed != null)
                Instances.ConnectSettingsUserControlModel.Win32ControlScreenCapType = parsed.Value;
        }
    }

    private void UpdateConnectionStatus(bool hasDevices, MaaControllerTypes controllerType)
    {
        if (!hasDevices)
        {
            var isAdb = controllerType == MaaControllerTypes.Adb;
            ToastHelper.Info((
                isAdb ? LangKeys.NoEmulatorFound : LangKeys.NoWindowFound).ToLocalization(), (
                isAdb ? LangKeys.NoEmulatorFoundDetail : "").ToLocalization());
        }
    }

    public void TryReadPlayCoverConfig()
    {
        if (ConfigurationManager.Current.TryGetValue(ConfigurationKeys.PlayCoverConfig, out PlayCoverCoreConfig savedConfig))
        {
            MaaProcessor.Config.PlayCover = savedConfig;
        }
    }

    private void HandleDetectionError(Exception ex, MaaControllerTypes controllerType)
    {
        var targetKey = controllerType switch
        {
            MaaControllerTypes.Adb => LangKeys.Emulator,
            MaaControllerTypes.Win32 => LangKeys.Window,
            MaaControllerTypes.PlayCover => LangKeys.TabPlayCover,
            _ => LangKeys.Window
        };
        ToastHelper.Warn(string.Format(
            LangKeys.TaskStackError.ToLocalization(),
            targetKey.ToLocalization(),
            ex.Message));

        LoggerHelper.Error(ex);
    }
    #region 实时画面

    [ObservableProperty] private bool _isLiveViewPaused;
    [ObservableProperty] private double _liveViewScale = 1;
    [ObservableProperty] private LiveViewToolMode _activeToolMode = LiveViewToolMode.None;
    [ObservableProperty] private Rect _selectionRect;
    [ObservableProperty] private bool _hasSelection;
    [ObservableProperty] private Rect _secondarySelectionRect;
    [ObservableProperty] private bool _hasSecondarySelection;
    [ObservableProperty] private bool _isToolPanelVisible;
    [ObservableProperty] private bool _isDragMode = true;
    [ObservableProperty] private bool _isRoiMode;
    [ObservableProperty] private bool _isColorPickMode;
    [ObservableProperty] private bool _isSwipeMode;
    [ObservableProperty] private bool _isOcrMode;
    [ObservableProperty] private bool _isScreenshotMode;
    [ObservableProperty] private bool _isTargetMode;
    [ObservableProperty] private bool _isScreenshotBrushMode;
    [ObservableProperty] private int _screenshotBrushSize = 1;
    [ObservableProperty] private LiveViewRoiSelectionType _roiSelectionType = LiveViewRoiSelectionType.OriginRoi;
    [ObservableProperty] private ObservableCollection<string> _roiModeOptions = ["ROI", "Target"];
    [ObservableProperty] private int _roiModeIndex;

    private static readonly IBrush RoiSelectionStroke = new SolidColorBrush(Colors.DodgerBlue);
    private static readonly IBrush RoiSelectionFill = new SolidColorBrush(Color.FromArgb(40, 64, 128, 255));
    private static readonly IBrush TargetSelectionStrokeDefault = new SolidColorBrush(Colors.Orange);
    private static readonly IBrush TargetSelectionFillDefault = new SolidColorBrush(Color.FromArgb(40, 255, 165, 0));

    [ObservableProperty] private IBrush _selectionStroke = RoiSelectionStroke;
    [ObservableProperty] private IBrush _selectionFill = RoiSelectionFill;
    [ObservableProperty] private IBrush _secondarySelectionStroke = TargetSelectionStrokeDefault;
    [ObservableProperty] private IBrush _secondarySelectionFill = TargetSelectionFillDefault;
    [ObservableProperty] private IBrush _targetSelectionStroke = TargetSelectionStrokeDefault;
    [ObservableProperty] private IBrush _targetSelectionFill = TargetSelectionFillDefault;
    [ObservableProperty] private bool _hasSwipeArrow;
    [ObservableProperty] private Geometry? _swipeArrowGeometry;
    [ObservableProperty] private IBrush _swipeArrowStroke = TargetSelectionStrokeDefault;
    [ObservableProperty] private bool _isSelectingTarget;
    [ObservableProperty] private int _roiTargetIndex;
    [ObservableProperty] private ObservableCollection<string> _roiTargetOptions = ["ROI", "Target"];
    [ObservableProperty] private string _currentRoiSelectionLabel = "当前选择: ROI";

    [ObservableProperty] private string _roiX = "0";
    [ObservableProperty] private string _roiY = "0";
    [ObservableProperty] private string _roiW = "0";
    [ObservableProperty] private string _roiH = "0";

    [ObservableProperty] private string _colorPickX = "0";
    [ObservableProperty] private string _colorPickY = "0";
    [ObservableProperty] private string _colorPickW = "0";
    [ObservableProperty] private string _colorPickH = "0";
    [ObservableProperty] private string _colorPickExpandedX = "0";
    [ObservableProperty] private string _colorPickExpandedY = "0";
    [ObservableProperty] private string _colorPickExpandedW = "0";
    [ObservableProperty] private string _colorPickExpandedH = "0";

    [ObservableProperty] private string _screenshotX = "0";
    [ObservableProperty] private string _screenshotY = "0";
    [ObservableProperty] private string _screenshotW = "0";
    [ObservableProperty] private string _screenshotH = "0";
    [ObservableProperty] private string _screenshotExpandedX = "0";
    [ObservableProperty] private string _screenshotExpandedY = "0";
    [ObservableProperty] private string _screenshotExpandedW = "0";
    [ObservableProperty] private string _screenshotExpandedH = "0";
    [ObservableProperty] private string _screenshotRelativePath =
        ConfigurationManager.Current.GetValue(ConfigurationKeys.LiveViewScreenshotRelativePath, string.Empty);
    [ObservableProperty] private string _screenshotRelativeResult = string.Empty;

    [ObservableProperty] private string _ocrX = "0";
    [ObservableProperty] private string _ocrY = "0";
    [ObservableProperty] private string _ocrW = "0";
    [ObservableProperty] private string _ocrH = "0";
    [ObservableProperty] private string _ocrExpandedX = "0";
    [ObservableProperty] private string _ocrExpandedY = "0";
    [ObservableProperty] private string _ocrExpandedW = "0";
    [ObservableProperty] private string _ocrExpandedH = "0";
    [ObservableProperty] private string _ocrResult = string.Empty;

    [ObservableProperty] private string _swipeStartX = "0";
    [ObservableProperty] private string _swipeStartY = "0";
    [ObservableProperty] private string _swipeEndX = "0";
    [ObservableProperty] private string _swipeEndY = "0";

    [ObservableProperty] private string _originTargetX = "0";
    [ObservableProperty] private string _originTargetY = "0";
    [ObservableProperty] private string _originTargetW = "0";
    [ObservableProperty] private string _originTargetH = "0";

    [ObservableProperty] private string _targetX = "0";
    [ObservableProperty] private string _targetY = "0";
    [ObservableProperty] private string _targetW = "0";
    [ObservableProperty] private string _targetH = "0";

    [ObservableProperty] private string _offsetX = "0";
    [ObservableProperty] private string _offsetY = "0";
    [ObservableProperty] private string _offsetW = "0";
    [ObservableProperty] private string _offsetH = "0";

    [ObservableProperty] private int _colorMode;
    [ObservableProperty] private string _rgbUpperR = "0";
    [ObservableProperty] private string _rgbUpperG = "0";
    [ObservableProperty] private string _rgbUpperB = "0";
    [ObservableProperty] private string _rgbLowerR = "0";
    [ObservableProperty] private string _rgbLowerG = "0";
    [ObservableProperty] private string _rgbLowerB = "0";

    [ObservableProperty] private string _hsvUpperH = "0";
    [ObservableProperty] private string _hsvUpperS = "0";
    [ObservableProperty] private string _hsvUpperV = "0";
    [ObservableProperty] private string _hsvLowerH = "0";
    [ObservableProperty] private string _hsvLowerS = "0";
    [ObservableProperty] private string _hsvLowerV = "0";

    [ObservableProperty] private string _grayUpper = "0";
    [ObservableProperty] private string _grayLower = "0";

    private Rect _roiRect;
    private Rect _originTargetRect;
    private Rect _targetRect;
    private Rect _colorPickRect;
    private Rect _colorPickExpandedRect;
    private Rect _screenshotRect;
    private Rect _screenshotExpandedRect;
    private Rect _ocrRect;
    private Rect _ocrExpandedRect;
    private bool _suppressRoiSync;
    private bool _suppressTargetSync;
    private bool _suppressToolModeSync;
    private bool _suppressRoiTargetSync;

    private readonly int _horizontalExpansion =
        ConfigurationManager.Current.GetValue(ConfigurationKeys.LiveViewHorizontalExpansion, 25);
    private readonly int _verticalExpansion =
        ConfigurationManager.Current.GetValue(ConfigurationKeys.LiveViewVerticalExpansion, 25);
    [ObservableProperty] private bool _isColorPreviewActive;
    private Bitmap? _colorPreviewImage;
    private WriteableBitmap? _screenshotBrushImage;

    [ObservableProperty] private bool _hasBrushPreview;
    [ObservableProperty] private Point _brushPreviewPoint;
    [ObservableProperty] private double _brushPreviewSize = 1;

    private Bitmap? _pausedLiveViewImage;

    public Bitmap? LiveViewDisplayImage
    {
        get
        {
            if (IsScreenshotBrushMode && IsLiveViewPaused && _screenshotBrushImage != null)
            {
                return _screenshotBrushImage;
            }

            if (IsColorPreviewActive && _colorPreviewImage != null)
            {
                return _colorPreviewImage;
            }

            return IsLiveViewPaused ? _pausedLiveViewImage : LiveViewImage;
        }
    }

    partial void OnIsLiveViewPausedChanged(bool value)
    {
        if (value)
        {
            _pausedLiveViewImage = LiveViewImage;
            if (IsColorPreviewActive)
            {
                ApplyColorFilter();
            }

            if (IsScreenshotBrushMode)
            {
                ResetScreenshotBrushPreview();
            }
        }
        else
        {
            _pausedLiveViewImage = null;
            DisableColorPreview();
            ResetScreenshotBrushPreview();
        }

        UpdateBrushPreviewAvailability();
        OnPropertyChanged(nameof(LiveViewDisplayImage));

    }

    partial void OnLiveViewScaleChanged(double value)
    {
        var clamped = Math.Clamp(value, 0.1, 10.0);
        if (Math.Abs(clamped - value) > 0.0001)
        {
            LiveViewScale = clamped;
        }
    }

    partial void OnRoiXChanged(string value) => UpdateRoiRectFromText();
    partial void OnRoiYChanged(string value) => UpdateRoiRectFromText();
    partial void OnRoiWChanged(string value) => UpdateRoiRectFromText();
    partial void OnRoiHChanged(string value) => UpdateRoiRectFromText();

    partial void OnOriginTargetXChanged(string value) => UpdateOriginTargetRectFromText();
    partial void OnOriginTargetYChanged(string value) => UpdateOriginTargetRectFromText();
    partial void OnOriginTargetWChanged(string value) => UpdateOriginTargetRectFromText();
    partial void OnOriginTargetHChanged(string value) => UpdateOriginTargetRectFromText();

    partial void OnTargetXChanged(string value) => UpdateTargetRectFromText();
    partial void OnTargetYChanged(string value) => UpdateTargetRectFromText();
    partial void OnTargetWChanged(string value) => UpdateTargetRectFromText();
    partial void OnTargetHChanged(string value) => UpdateTargetRectFromText();

    partial void OnActiveToolModeChanged(LiveViewToolMode value)
    {
        if (value != LiveViewToolMode.ColorPick)
        {
            DisableColorPreview();
        }

        if (value != LiveViewToolMode.Screenshot)
        {
            IsScreenshotBrushMode = false;
            ResetScreenshotBrushPreview();
        }

        IsToolPanelVisible = value != LiveViewToolMode.None;
        if (value != LiveViewToolMode.None)
        {
            IsDragMode = false;
            if (!IsLiveViewPaused)
            {
                IsLiveViewPaused = true;
            }
        }

        SyncToolModeFlags();

        if (value == LiveViewToolMode.Roi)
        {
            ApplySelectionPreview();
        }
    }

    partial void OnIsDragModeChanged(bool value)
    {
        if (value)
        {
            DisableColorPreview();
            ActiveToolMode = LiveViewToolMode.None;
            HasSelection = false;
            HasSecondarySelection = false;
        }
    }

    partial void OnIsTargetModeChanged(bool value)
    {
        if (_suppressRoiTargetSync) return;
        _suppressRoiTargetSync = true;
        RoiModeIndex = value ? 1 : 0;
        _suppressRoiTargetSync = false;

        if (value && RoiSelectionType == LiveViewRoiSelectionType.OriginRoi)
        {
            RoiSelectionType = LiveViewRoiSelectionType.OriginTarget;
        }
        else if (!value && RoiSelectionType == LiveViewRoiSelectionType.OriginTarget)
        {
            RoiSelectionType = LiveViewRoiSelectionType.OriginRoi;
        }

        ApplySelectionPreview();
        UpdateOffsets();
    }

    partial void OnIsSelectingTargetChanged(bool value)
    {
        if (!_suppressRoiTargetSync)
        {
            _suppressRoiTargetSync = true;
            RoiTargetIndex = value ? 1 : 0;
            _suppressRoiTargetSync = false;
        }

        CurrentRoiSelectionLabel = value ? "当前选择: Target" : "当前选择: ROI";
        ApplySelectionPreview();
    }

    partial void OnRoiTargetIndexChanged(int value)
    {
        if (_suppressRoiTargetSync) return;
        _suppressRoiTargetSync = true;
        IsSelectingTarget = value == 1;
        _suppressRoiTargetSync = false;
    }

    partial void OnRoiModeIndexChanged(int value)
    {
        if (_suppressRoiTargetSync) return;
        _suppressRoiTargetSync = true;
        IsTargetMode = value == 1;
        _suppressRoiTargetSync = false;
    }

    partial void OnIsRoiModeChanged(bool value)
    {
        if (_suppressToolModeSync) return;
        if (value)
        {
            ActiveToolMode = LiveViewToolMode.Roi;
        }
        else if (ActiveToolMode == LiveViewToolMode.Roi)
        {
            ActiveToolMode = LiveViewToolMode.None;
        }
    }

    partial void OnIsColorPickModeChanged(bool value)
    {
        if (_suppressToolModeSync) return;
        if (value)
        {
            ActiveToolMode = LiveViewToolMode.ColorPick;
        }
        else if (ActiveToolMode == LiveViewToolMode.ColorPick)
        {
            ActiveToolMode = LiveViewToolMode.None;
        }
    }

    partial void OnIsSwipeModeChanged(bool value)
    {
        if (_suppressToolModeSync) return;
        if (value)
        {
            ActiveToolMode = LiveViewToolMode.Swipe;
        }
        else if (ActiveToolMode == LiveViewToolMode.Swipe)
        {
            ActiveToolMode = LiveViewToolMode.None;
        }
    }

    partial void OnIsOcrModeChanged(bool value)
    {
        if (_suppressToolModeSync) return;
        if (value)
        {
            ActiveToolMode = LiveViewToolMode.Ocr;
        }
        else if (ActiveToolMode == LiveViewToolMode.Ocr)
        {
            ActiveToolMode = LiveViewToolMode.None;
        }
    }

    partial void OnIsScreenshotModeChanged(bool value)
    {
        if (_suppressToolModeSync) return;
        if (value)
        {
            ActiveToolMode = LiveViewToolMode.Screenshot;
        }
        else if (ActiveToolMode == LiveViewToolMode.Screenshot)
        {
            ActiveToolMode = LiveViewToolMode.None;
        }
    }
    partial void OnIsScreenshotBrushModeChanged(bool value)
    {
        if (!value)
        {
            ResetScreenshotBrushPreview();
        }

        UpdateBrushPreviewAvailability();
        ApplySelectionPreview();
    }


    partial void OnScreenshotRelativePathChanged(string value)
    {
        ConfigurationManager.Current.SetValue(ConfigurationKeys.LiveViewScreenshotRelativePath, value);
    }

    partial void OnScreenshotBrushSizeChanged(int value)
    {
        BrushPreviewSize = Math.Max(1, value);
    }

    [RelayCommand]
    private void ToggleLiveViewPause()
    {
        IsLiveViewPaused = !IsLiveViewPaused;
    }

    // [RelayCommand]
    // private void ZoomIn()
    // {
    //     if (!IsDragMode) return;
    //     LiveViewScale = Math.Clamp(LiveViewScale + 0.1, 0.1, 1.0);
    // }
    //
    // [RelayCommand]
    // private void ZoomOut()
    // {
    //     if (!IsDragMode) return;
    //     LiveViewScale = Math.Clamp(LiveViewScale - 0.1, 0.1, 1.0);
    // }
    //
    // [RelayCommand]
    // private void ResetZoom()
    // {
    //     if (!IsDragMode) return;
    //     LiveViewScale = 1.0;
    // }

    [RelayCommand]
    private void ActivateTool(LiveViewToolMode mode)
    {
        if (!IsLiveViewPaused)
        {
            IsLiveViewPaused = true;
        }

        ActiveToolMode = mode;
        ApplySelectionPreview();
    }
    [RelayCommand]
    private void SelectRoi()
    {
        if (!IsLiveViewPaused)
        {
            IsLiveViewPaused = true;
        }

        IsTargetMode = false;
        ActiveToolMode = LiveViewToolMode.Roi;
        RoiSelectionType = LiveViewRoiSelectionType.OriginRoi;
        ApplySelectionPreview();
    }

    [RelayCommand]
    private void SelectOriginTarget()
    {
        if (!IsLiveViewPaused)
        {
            IsLiveViewPaused = true;
        }

        IsTargetMode = true;
        ActiveToolMode = LiveViewToolMode.Roi;
        RoiSelectionType = LiveViewRoiSelectionType.OriginTarget;
        ApplySelectionPreview();
    }

    [RelayCommand]
    private void SelectTarget()
    {
        if (!IsLiveViewPaused)
        {
            IsLiveViewPaused = true;
        }

        ActiveToolMode = LiveViewToolMode.Roi;
        RoiSelectionType = LiveViewRoiSelectionType.TargetRoi;
        ApplySelectionPreview();
    }

    [RelayCommand]
    private void ClearRoi()
    {
        _suppressRoiSync = true;
        _roiRect = default;
        RoiX = "0";
        RoiY = "0";
        RoiW = "0";
        RoiH = "0";
        _suppressRoiSync = false;
        UpdateOffsets();
        RefreshSelectionRects();
    }

    [RelayCommand]
    private void ClearOriginTarget()
    {
        _suppressTargetSync = true;
        _originTargetRect = default;
        OriginTargetX = "0";
        OriginTargetY = "0";
        OriginTargetW = "0";
        OriginTargetH = "0";
        _suppressTargetSync = false;
        UpdateOffsets();
        RefreshSelectionRects();
    }

    [RelayCommand]
    private void ClearTarget()
    {
        _suppressTargetSync = true;
        _targetRect = default;
        TargetX = "0";
        TargetY = "0";
        TargetW = "0";
        TargetH = "0";
        _suppressTargetSync = false;
        UpdateOffsets();
        RefreshSelectionRects();
    }

    [RelayCommand]
    private void CopyRoiToTarget()
    {
        if (_roiRect.Width <= 0 || _roiRect.Height <= 0) return;
        SetTargetRect(_roiRect);
        UpdateOffsets();
        RefreshSelectionRects();
    }

    [RelayCommand]
    private void CopyOriginTargetToTarget()
    {
        if (_originTargetRect.Width <= 0 || _originTargetRect.Height <= 0) return;
        SetTargetRect(_originTargetRect);
        UpdateOffsets();
        RefreshSelectionRects();
    }

    [RelayCommand]
    private void CopyTargetToOrigin()
    {
        if (_targetRect.Width <= 0 || _targetRect.Height <= 0) return;
        if (IsTargetMode)
            SetOriginTargetRect(_targetRect);
        else
            SetRoiRect(_targetRect);
        UpdateOffsets();
        RefreshSelectionRects();
    }

    [RelayCommand]
    private void CopyRoiToClipboard()
    {
        CopyTextToClipboard(BuildRectClipboardText(RoiX, RoiY, RoiW, RoiH), "复制ROI到剪贴板");
    }

    [RelayCommand]
    private async Task PasteRoiFromClipboard()
    {
        if (await TryGetClipboardRectAsync(SetRoiRect))
        {
            UpdateOffsets();
            RefreshSelectionRects();
        }
    }

    [RelayCommand]
    private void CopyOriginTargetToClipboard()
    {
        CopyTextToClipboard(BuildRectClipboardText(OriginTargetX, OriginTargetY, OriginTargetW, OriginTargetH), "复制OriginTarget到剪贴板");
    }

    [RelayCommand]
    private async Task PasteOriginTargetFromClipboard()
    {
        if (await TryGetClipboardRectAsync(SetOriginTargetRect))
        {
            UpdateOffsets();
            RefreshSelectionRects();
        }
    }

    [RelayCommand]
    private void CopyTargetToClipboard()
    {
        CopyTextToClipboard(BuildRectClipboardText(TargetX, TargetY, TargetW, TargetH), "复制Target到剪贴板");
    }

    [RelayCommand]
    private async Task PasteTargetFromClipboard()
    {
        if (await TryGetClipboardRectAsync(SetTargetRect))
        {
            UpdateOffsets();
            RefreshSelectionRects();
        }
    }

    [RelayCommand]
    private void CopyOffsetToClipboard()
    {
        CopyTextToClipboard(BuildRectClipboardText(OffsetX, OffsetY, OffsetW, OffsetH), "复制Offset到剪贴板");
    }

    private static string BuildRectClipboardText(string xText, string yText, string wText, string hText)
    {
        if (double.TryParse(xText, out var x)
            && double.TryParse(yText, out var y)
            && double.TryParse(wText, out var w)
            && double.TryParse(hText, out var h))
        {
            return $"[{(int)Math.Round(x)}, {(int)Math.Round(y)}, {(int)Math.Round(w)}, {(int)Math.Round(h)}]";
        }

        return "[0, 0, 0, 0]";
    }

    private void CopyTextToClipboard(string text, string taskName)
    {
        TaskManager.RunTaskAsync(() =>
        {
            var clipboard = Instances.RootView?.Clipboard;
            if (clipboard == null) return;

            DispatcherHelper.PostOnMainThread(async () => await clipboard.SetTextAsync(text));
        }, name: taskName);
    }

    private async Task<bool> TryGetClipboardRectAsync(Action<Rect> applyAction)
    {
        var clipboard = Instances.RootView?.Clipboard;
        if (clipboard == null)
        {
            return false;
        }

        var text = await clipboard.GetTextAsync();
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var matches = Regex.Matches(text, @"-?\d+");
        if (matches.Count < 4)
        {
            return false;
        }

        if (!double.TryParse(matches[0].Value, out var x)
            || !double.TryParse(matches[1].Value, out var y)
            || !double.TryParse(matches[2].Value, out var w)
            || !double.TryParse(matches[3].Value, out var h))
        {
            return false;
        }

        if (w <= 0 || h <= 0)
        {
            return false;
        }

        var rect = new Rect(Math.Max(0, x), Math.Max(0, y), Math.Max(1, w), Math.Max(1, h));
        applyAction(rect);
        return true;
    }

    [RelayCommand]
    private void SetDragMode()
    {
        IsDragMode = true;
    }

    [RelayCommand]
    private void SetScreenshotMode(string? mode)
    {
        IsScreenshotBrushMode = string.Equals(mode, "Brush", StringComparison.OrdinalIgnoreCase);
    }

    [RelayCommand]
    private void ResetScreenshotBrush()
    {
        ResetScreenshotBrushPreview();
    }

    private void SyncToolModeFlags()
    {
        _suppressToolModeSync = true;
        IsRoiMode = ActiveToolMode == LiveViewToolMode.Roi;
        IsColorPickMode = ActiveToolMode == LiveViewToolMode.ColorPick;
        IsSwipeMode = ActiveToolMode == LiveViewToolMode.Swipe;
        IsOcrMode = ActiveToolMode == LiveViewToolMode.Ocr;
        IsScreenshotMode = ActiveToolMode == LiveViewToolMode.Screenshot;
        _suppressToolModeSync = false;
    }


    [RelayCommand]
    private void SetColorMode(string? mode)
    {
        if (int.TryParse(mode, out var value))
        {
            ColorMode = value;
        }
    }

    public void UpdateSelection(Rect rect, bool hasSelection)
    {
        UpdateSelectionWithSecondary(rect, hasSelection, RoiSelectionType);
    }

    public void ApplySelection(Rect rect)
    {
        switch (RoiSelectionType)
        {
            case LiveViewRoiSelectionType.OriginRoi:
                SetRoiRect(rect);
                break;
            case LiveViewRoiSelectionType.OriginTarget:
                SetOriginTargetRect(rect);
                break;
            case LiveViewRoiSelectionType.TargetRoi:
                SetTargetRect(rect);
                break;
        }

        UpdateSelection(rect, rect.Width > 0 && rect.Height > 0);
        UpdateOffsets();
    }

    public void ApplySelectionForTool(Rect rect)
    {
        switch (ActiveToolMode)
        {
            case LiveViewToolMode.ColorPick:
                SetColorPickRect(rect);
                break;
            case LiveViewToolMode.Screenshot:
                SetScreenshotRect(rect);
                break;
            case LiveViewToolMode.Ocr:
                SetOcrRect(rect);
                break;
        }

        ApplySelectionPreview();
    }

    public void SetSwipeStart(Point point)
    {
        SwipeStartX = ((int)Math.Round(point.X)).ToString();
        SwipeStartY = ((int)Math.Round(point.Y)).ToString();
        UpdateSwipeArrow();
    }

    public void SetSwipeEnd(Point point)
    {
        SwipeEndX = ((int)Math.Round(point.X)).ToString();
        SwipeEndY = ((int)Math.Round(point.Y)).ToString();
        UpdateSwipeArrow();
    }

    public void UpdateColorRangeFromSelection(Rect rect)
    {
        if (LiveViewDisplayImage is not Bitmap bitmap)
        {
            return;
        }

        SetColorPickRect(rect);

        var x = (int)Math.Clamp(rect.X, 0, bitmap.Size.Width - 1);
        var y = (int)Math.Clamp(rect.Y, 0, bitmap.Size.Height - 1);
        var w = (int)Math.Clamp(rect.Width, 1, bitmap.Size.Width - x);
        var h = (int)Math.Clamp(rect.Height, 1, bitmap.Size.Height - y);

        if (w <= 0 || h <= 0)
        {
            return;
        }

        var width = (int)bitmap.Size.Width;
        var height = (int)bitmap.Size.Height;
        var stride = width * 4;
        var buffer = new byte[height * stride];
        unsafe
        {
            fixed (byte* ptr = buffer)
            {
                bitmap.CopyPixels(new PixelRect(0, 0, width, height), (IntPtr)ptr, buffer.Length, stride);
            }
        }

        int minR = 255, minG = 255, minB = 255;
        int maxR = 0, maxG = 0, maxB = 0;
        int minH = 180, minS = 255, minV = 255;
        int maxH = 0, maxS = 0, maxV = 0;
        int minGray = 255, maxGray = 0;

        for (var yy = y; yy < y + h; yy++)
        {
            var rowStart = yy * stride;
            for (var xx = x; xx < x + w; xx++)
            {
                var index = rowStart + xx * 4;
                var b = buffer[index];
                var g = buffer[index + 1];
                var r = buffer[index + 2];

                minR = Math.Min(minR, r);
                minG = Math.Min(minG, g);
                minB = Math.Min(minB, b);
                maxR = Math.Max(maxR, r);
                maxG = Math.Max(maxG, g);
                maxB = Math.Max(maxB, b);

                var gray = (r + g + b) / 3;
                minGray = Math.Min(minGray, gray);
                maxGray = Math.Max(maxGray, gray);

                RgbToHsv(r, g, b, out var hVal, out var sVal, out var vVal);
                minH = Math.Min(minH, hVal);
                minS = Math.Min(minS, sVal);
                minV = Math.Min(minV, vVal);
                maxH = Math.Max(maxH, hVal);
                maxS = Math.Max(maxS, sVal);
                maxV = Math.Max(maxV, vVal);
            }
        }

        RgbUpperR = maxR.ToString();
        RgbUpperG = maxG.ToString();
        RgbUpperB = maxB.ToString();
        RgbLowerR = minR.ToString();
        RgbLowerG = minG.ToString();
        RgbLowerB = minB.ToString();

        HsvUpperH = maxH.ToString();
        HsvUpperS = maxS.ToString();
        HsvUpperV = maxV.ToString();
        HsvLowerH = minH.ToString();
        HsvLowerS = minS.ToString();
        HsvLowerV = minV.ToString();

        GrayUpper = maxGray.ToString();
        GrayLower = minGray.ToString();

        if (IsColorPreviewActive)
        {
            ApplyColorFilter();
        }
    }

    [RelayCommand]
    private void ToggleColorPreview()
    {
        IsColorPreviewActive = !IsColorPreviewActive;
        if (IsColorPreviewActive)
        {
            ApplyColorFilter();
        }
        else
        {
            _colorPreviewImage = null;
            OnPropertyChanged(nameof(LiveViewDisplayImage));
        }
    }

    private void DisableColorPreview()
    {
        if (!IsColorPreviewActive)
        {
            return;
        }

        IsColorPreviewActive = false;
        _colorPreviewImage = null;
        OnPropertyChanged(nameof(LiveViewDisplayImage));
    }

    [RelayCommand]
    private void CopyColorPickRect()
    {
        CopyTextToClipboard(BuildRectClipboardText(ColorPickX, ColorPickY, ColorPickW, ColorPickH), "复制取色ROI到剪贴板");
    }

    [RelayCommand]
    private async Task PasteColorPickRect()
    {
        if (await TryGetClipboardRectAsync(SetColorPickRect))
        {
            ApplySelectionPreview();
        }
    }

    [RelayCommand]
    private void CopyColorPickExpandedRect()
    {
        CopyTextToClipboard(BuildRectClipboardText(ColorPickExpandedX, ColorPickExpandedY, ColorPickExpandedW, ColorPickExpandedH), "复制取色扩展ROI到剪贴板");
    }

    [RelayCommand]
    private void CopyScreenshotRect()
    {
        CopyTextToClipboard(BuildRectClipboardText(ScreenshotX, ScreenshotY, ScreenshotW, ScreenshotH), "复制截图ROI到剪贴板");
    }

    [RelayCommand]
    private async Task PasteScreenshotRect()
    {
        if (await TryGetClipboardRectAsync(SetScreenshotRect))
        {
            ApplySelectionPreview();
        }
    }

    [RelayCommand]
    private void CopyScreenshotExpandedRect()
    {
        CopyTextToClipboard(BuildRectClipboardText(ScreenshotExpandedX, ScreenshotExpandedY, ScreenshotExpandedW, ScreenshotExpandedH), "复制截图扩展ROI到剪贴板");
    }

    [RelayCommand]
    private void CopyOcrRect()
    {
        CopyTextToClipboard(BuildRectClipboardText(OcrX, OcrY, OcrW, OcrH), "复制OCR ROI到剪贴板");
    }

    [RelayCommand]
    private async Task PasteOcrRect()
    {
        if (await TryGetClipboardRectAsync(SetOcrRect))
        {
            ApplySelectionPreview();
        }
    }

    [RelayCommand]
    private void CopyOcrExpandedRect()
    {
        CopyTextToClipboard(BuildRectClipboardText(OcrExpandedX, OcrExpandedY, OcrExpandedW, OcrExpandedH), "复制OCR扩展ROI到剪贴板");
    }

    [RelayCommand]
    private void CopyOcrResult()
    {
        CopyTextToClipboard(OcrResult ?? string.Empty, "复制OCR结果到剪贴板");
    }

    [RelayCommand]
    private async Task ImportImage()
    {
        var topLevel = TopLevel.GetTopLevel(Instances.RootView);
        if (topLevel == null)
        {
            return;
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = LangKeys.LoadImageTitle.ToLocalization(),
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Images")
                {
                    Patterns = ["*.png", "*.jpg", "*.jpeg", "*.bmp", "*.webp"]
                }
            ]
        });

        var file = files?.FirstOrDefault();
        if (file == null)
        {
            return;
        }

        if (!IsLiveViewPaused)
        {
            IsLiveViewPaused = true;
        }

        await using var stream = await file.OpenReadAsync();
        var bitmap = new Bitmap(stream);
        LiveViewImage = bitmap;
        _pausedLiveViewImage = bitmap;
        OnPropertyChanged(nameof(LiveViewDisplayImage));

        if (IsColorPreviewActive)
        {
            ApplyColorFilter();
        }
    }

    [RelayCommand]
    private async Task RunOcr()
    {
        var sourceBitmap = LiveViewImage ?? LiveViewDisplayImage;
        if (sourceBitmap is not Bitmap bitmap)
        {
            ToastHelper.Warn(LangKeys.Tip.ToLocalization(), LangKeys.LiveViewNoScreenshot.ToLocalization());
            return;
        }

        if (!TryParseRect(OcrX, OcrY, OcrW, OcrH, out var rect))
        {
            ToastHelper.Warn(LangKeys.Tip.ToLocalization(), LangKeys.LiveViewSelectOcrRegion.ToLocalization());
            return;
        }

        var tasker = await MaaProcessor.Instance.GetTaskerAsync();
        if (tasker == null)
        {
            ToastHelper.Warn(LangKeys.Tip.ToLocalization(), LangKeys.LiveViewRecognizerUnavailable.ToLocalization());
            return;
        }

        var result = RecognitionHelper.ReadTextFromMaaTasker(
            tasker,
            bitmap,
            (int)Math.Round(rect.X),
            (int)Math.Round(rect.Y),
            Math.Max(1, (int)Math.Round(rect.Width)),
            Math.Max(1, (int)Math.Round(rect.Height)));

        OcrResult = result;
    }

    [RelayCommand]
    private async Task SaveScreenshot()
    {
        if (LiveViewDisplayImage is not Bitmap bitmap)
        {
            ToastHelper.Warn(LangKeys.Tip.ToLocalization(), LangKeys.LiveViewNoScreenshot.ToLocalization());
            return;
        }

        if (_screenshotRect.Width <= 0 || _screenshotRect.Height <= 0)
        {
            ToastHelper.Warn(LangKeys.Tip.ToLocalization(), LangKeys.LiveViewSelectScreenshotRegion.ToLocalization());
            return;
        }

        var cropped = CropBitmap(bitmap, _screenshotRect);
        if (cropped == null)
        {
            ToastHelper.Warn(LangKeys.Tip.ToLocalization(), LangKeys.LiveViewInvalidScreenshotRegion.ToLocalization());
            return;
        }

        var topLevel = TopLevel.GetTopLevel(Instances.RootView);
        if (topLevel == null)
        {
            return;
        }

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = LangKeys.SaveScreenshot.ToLocalization(),
            SuggestedFileName = "screenshot.png",
            FileTypeChoices =
            [
                new FilePickerFileType("PNG")
                {
                    Patterns = ["*.png"]
                },
                new FilePickerFileType("JPG")
                {
                    Patterns = ["*.jpg", "*.jpeg"]
                },
                new FilePickerFileType("BMP")
                {
                    Patterns = ["*.bmp"]
                }
            ]
        });

        if (file?.TryGetLocalPath() is not { } path)
        {
            return;
        }

        await using (var stream = File.Create(path))
        {
            cropped.Save(stream);
        }

        ScreenshotRelativeResult = string.IsNullOrWhiteSpace(ScreenshotRelativePath)
            ? path
            : Path.Combine(ScreenshotRelativePath, Path.GetFileName(path));

        ToastHelper.Info(LangKeys.Tip.ToLocalization(),
            string.Format(LangKeys.LiveViewScreenshotSaved.ToLocalization(), path));
    }

    [RelayCommand]
    private void CopySwipeStart()
    {
        CopyTextToClipboard(BuildPointClipboardText(SwipeStartX, SwipeStartY), "复制Swipe起点到剪贴板");
    }

    [RelayCommand]
    private void CopySwipeEnd()
    {
        CopyTextToClipboard(BuildPointClipboardText(SwipeEndX, SwipeEndY), "复制Swipe终点到剪贴板");
    }

    private static string BuildPointClipboardText(string xText, string yText)
    {
        if (double.TryParse(xText, out var x)
            && double.TryParse(yText, out var y))
        {
            return $"[{(int)Math.Round(x)}, {(int)Math.Round(y)}]";
        }

        return "[0, 0]";
    }

    private void UpdateRoiRectFromText()
    {
        if (_suppressRoiSync)
        {
            return;
        }

        if (!TryParseRect(RoiX, RoiY, RoiW, RoiH, out var rect))
        {
            return;
        }

        _roiRect = rect;
        UpdateOffsets();
        ApplySelectionPreview();
    }

    private void UpdateOriginTargetRectFromText()
    {
        if (_suppressTargetSync)
        {
            return;
        }

        if (!TryParseRect(OriginTargetX, OriginTargetY, OriginTargetW, OriginTargetH, out var rect))
        {
            return;
        }

        _originTargetRect = rect;
        UpdateOffsets();
        ApplySelectionPreview();
    }

    private void UpdateTargetRectFromText()
    {
        if (_suppressTargetSync)
        {
            return;
        }

        if (!TryParseRect(TargetX, TargetY, TargetW, TargetH, out var rect))
        {
            return;
        }

        _targetRect = rect;
        UpdateOffsets();
        ApplySelectionPreview();
    }

    private void SetRoiRect(Rect rect)
    {
        _suppressRoiSync = true;
        _roiRect = rect;
        RoiX = ((int)Math.Round(rect.X)).ToString();
        RoiY = ((int)Math.Round(rect.Y)).ToString();
        RoiW = Math.Max(1, (int)Math.Round(rect.Width)).ToString();
        RoiH = Math.Max(1, (int)Math.Round(rect.Height)).ToString();
        _suppressRoiSync = false;
        RefreshSelectionRects();
    }

    private void SetColorPickRect(Rect rect)
    {
        _colorPickRect = rect;
        ColorPickX = ((int)Math.Round(rect.X)).ToString();
        ColorPickY = ((int)Math.Round(rect.Y)).ToString();
        ColorPickW = Math.Max(1, (int)Math.Round(rect.Width)).ToString();
        ColorPickH = Math.Max(1, (int)Math.Round(rect.Height)).ToString();
        UpdateColorPickExpandedRect();
    }

    private void SetScreenshotRect(Rect rect)
    {
        _screenshotRect = rect;
        ScreenshotX = ((int)Math.Round(rect.X)).ToString();
        ScreenshotY = ((int)Math.Round(rect.Y)).ToString();
        ScreenshotW = Math.Max(1, (int)Math.Round(rect.Width)).ToString();
        ScreenshotH = Math.Max(1, (int)Math.Round(rect.Height)).ToString();
        UpdateScreenshotExpandedRect();
    }

    private void SetOcrRect(Rect rect)
    {
        _ocrRect = rect;
        OcrX = ((int)Math.Round(rect.X)).ToString();
        OcrY = ((int)Math.Round(rect.Y)).ToString();
        OcrW = Math.Max(1, (int)Math.Round(rect.Width)).ToString();
        OcrH = Math.Max(1, (int)Math.Round(rect.Height)).ToString();
        UpdateOcrExpandedRect();
    }

    private void UpdateColorPickExpandedRect()
    {
        _colorPickExpandedRect = BuildExpandedRect(_colorPickRect);
        ColorPickExpandedX = ((int)Math.Round(_colorPickExpandedRect.X)).ToString();
        ColorPickExpandedY = ((int)Math.Round(_colorPickExpandedRect.Y)).ToString();
        ColorPickExpandedW = Math.Max(1, (int)Math.Round(_colorPickExpandedRect.Width)).ToString();
        ColorPickExpandedH = Math.Max(1, (int)Math.Round(_colorPickExpandedRect.Height)).ToString();
    }

    private void UpdateScreenshotExpandedRect()
    {
        _screenshotExpandedRect = BuildExpandedRect(_screenshotRect);
        ScreenshotExpandedX = ((int)Math.Round(_screenshotExpandedRect.X)).ToString();
        ScreenshotExpandedY = ((int)Math.Round(_screenshotExpandedRect.Y)).ToString();
        ScreenshotExpandedW = Math.Max(1, (int)Math.Round(_screenshotExpandedRect.Width)).ToString();
        ScreenshotExpandedH = Math.Max(1, (int)Math.Round(_screenshotExpandedRect.Height)).ToString();
    }

    private void UpdateOcrExpandedRect()
    {
        _ocrExpandedRect = BuildExpandedRect(_ocrRect);
        OcrExpandedX = ((int)Math.Round(_ocrExpandedRect.X)).ToString();
        OcrExpandedY = ((int)Math.Round(_ocrExpandedRect.Y)).ToString();
        OcrExpandedW = Math.Max(1, (int)Math.Round(_ocrExpandedRect.Width)).ToString();
        OcrExpandedH = Math.Max(1, (int)Math.Round(_ocrExpandedRect.Height)).ToString();
    }

    private Rect BuildExpandedRect(Rect rect)
    {
        if (LiveViewDisplayImage is not Bitmap bitmap)
        {
            return rect;
        }

        var width = bitmap.Size.Width;
        var height = bitmap.Size.Height;

        var x = Math.Max(0, rect.X - _horizontalExpansion);
        var y = Math.Max(0, rect.Y - _verticalExpansion);
        var right = Math.Min(width, rect.X + rect.Width + _horizontalExpansion);
        var bottom = Math.Min(height, rect.Y + rect.Height + _verticalExpansion);

        var expandedWidth = Math.Max(0, right - x);
        var expandedHeight = Math.Max(0, bottom - y);

        return expandedWidth > 0 && expandedHeight > 0
            ? new Rect(x, y, expandedWidth, expandedHeight)
            : default;
    }

    private void SetOriginTargetRect(Rect rect)
    {
        _suppressTargetSync = true;
        _originTargetRect = rect;
        OriginTargetX = ((int)Math.Round(rect.X)).ToString();
        OriginTargetY = ((int)Math.Round(rect.Y)).ToString();
        OriginTargetW = Math.Max(1, (int)Math.Round(rect.Width)).ToString();
        OriginTargetH = Math.Max(1, (int)Math.Round(rect.Height)).ToString();
        _suppressTargetSync = false;
        RefreshSelectionRects();
    }

    private void SetTargetRect(Rect rect)
    {
        _suppressTargetSync = true;
        _targetRect = rect;
        TargetX = ((int)Math.Round(rect.X)).ToString();
        TargetY = ((int)Math.Round(rect.Y)).ToString();
        TargetW = Math.Max(1, (int)Math.Round(rect.Width)).ToString();
        TargetH = Math.Max(1, (int)Math.Round(rect.Height)).ToString();
        _suppressTargetSync = false;
        RefreshSelectionRects();
    }

    private void UpdateOffsets()
    {
        var baseRect = IsTargetMode ? _originTargetRect : _roiRect;
        var offset = new Rect(
            _targetRect.X - baseRect.X,
            _targetRect.Y - baseRect.Y,
            _targetRect.Width - baseRect.Width,
            _targetRect.Height - baseRect.Height);

        OffsetX = ((int)Math.Round(offset.X)).ToString();
        OffsetY = ((int)Math.Round(offset.Y)).ToString();
        OffsetW = ((int)Math.Round(offset.Width)).ToString();
        OffsetH = ((int)Math.Round(offset.Height)).ToString();
    }

    private void ApplySelectionPreview()
    {
        switch (ActiveToolMode)
        {
            case LiveViewToolMode.Roi:
                HasSwipeArrow = false;
                RefreshSelectionRects();
                break;
            case LiveViewToolMode.ColorPick:
                HasSwipeArrow = false;
                UpdateSelectionPreviewWithExpanded(_colorPickRect, _colorPickExpandedRect);
                break;
            case LiveViewToolMode.Screenshot:
                HasSwipeArrow = false;
                if (IsScreenshotBrushMode)
                {
                    ClearSelectionPreview();
                }
                else
                {
                    UpdateSelectionPreviewWithExpanded(_screenshotRect, _screenshotExpandedRect);
                }
                break;
            case LiveViewToolMode.Ocr:
                HasSwipeArrow = false;
                UpdateSelectionPreviewWithExpanded(_ocrRect, _ocrExpandedRect);
                break;
            case LiveViewToolMode.Swipe:
                ClearSelectionPreview();
                UpdateSwipeArrow();
                break;
        }
    }

    private void ApplyColorFilter()
    {
        var bitmap = GetLiveViewBaseImage();
        if (bitmap is null)
        {
            return;
        }

        if (!TryGetColorRange(out var range))
        {
            return;
        }

        var width = (int)bitmap.Size.Width;
        var height = (int)bitmap.Size.Height;
        var stride = width * 4;
        var buffer = new byte[height * stride];
        unsafe
        {
            fixed (byte* ptr = buffer)
            {
                bitmap.CopyPixels(new PixelRect(0, 0, width, height), (IntPtr)ptr, buffer.Length, stride);
            }
        }

        for (var y = 0; y < height; y++)
        {
            var rowStart = y * stride;
            for (var x = 0; x < width; x++)
            {
                var index = rowStart + x * 4;
                var b = buffer[index];
                var g = buffer[index + 1];
                var r = buffer[index + 2];

                if (!IsColorInRange(range, r, g, b))
                {
                    buffer[index] = 0;
                    buffer[index + 1] = 0;
                    buffer[index + 2] = 0;
                }
            }
        }

        _colorPreviewImage?.Dispose();
        var writeable = new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        using (var fb = writeable.Lock())
        {
            unsafe
            {
                fixed (byte* ptr = buffer)
                {
                    Buffer.MemoryCopy(ptr, (void*)fb.Address, fb.RowBytes * height, buffer.Length);
                }
            }
        }

        _colorPreviewImage = writeable;
        OnPropertyChanged(nameof(LiveViewDisplayImage));
    }

    private static WriteableBitmap? CropBitmap(Bitmap source, Rect rect)
    {
        var width = (int)source.Size.Width;
        var height = (int)source.Size.Height;

        var x = (int)Math.Clamp(rect.X, 0, Math.Max(0, width - 1));
        var y = (int)Math.Clamp(rect.Y, 0, Math.Max(0, height - 1));
        var w = (int)Math.Clamp(rect.Width, 1, Math.Max(0, width - x));
        var h = (int)Math.Clamp(rect.Height, 1, Math.Max(0, height - y));

        if (w <= 0 || h <= 0)
        {
            return null;
        }

        var stride = w * 4;
        var buffer = new byte[h * stride];

        unsafe
        {
            fixed (byte* ptr = buffer)
            {
                source.CopyPixels(new PixelRect(x, y, w, h), (IntPtr)ptr, buffer.Length, stride);
            }
        }

        var cropped = new WriteableBitmap(new PixelSize(w, h), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        using (var fb = cropped.Lock())
        {
            unsafe
            {
                fixed (byte* ptr = buffer)
                {
                    Buffer.MemoryCopy(ptr, (void*)fb.Address, fb.RowBytes * h, buffer.Length);
                }
            }
        }

        return cropped;
    }

    private Bitmap? GetLiveViewBaseImage()
    {
        return IsLiveViewPaused ? _pausedLiveViewImage ?? LiveViewImage : LiveViewImage;
    }

    private void ResetScreenshotBrushPreview()
    {
        _screenshotBrushImage?.Dispose();
        _screenshotBrushImage = null;
        OnPropertyChanged(nameof(LiveViewDisplayImage));
    }

    private void UpdateBrushPreviewAvailability()
    {
        HasBrushPreview = IsScreenshotBrushMode && IsLiveViewPaused && GetLiveViewBaseImage() != null;
        if (!HasBrushPreview)
        {
            BrushPreviewPoint = default;
        }
    }

    public void UpdateScreenshotBrushPreview(Point point)
    {
        if (!IsScreenshotBrushMode || !IsLiveViewPaused)
        {
            return;
        }

        BrushPreviewPoint = point;
        HasBrushPreview = true;
    }

    private void EnsureScreenshotBrushImage()
    {
        var source = GetLiveViewBaseImage();
        if (source is null)
        {
            return;
        }

        var width = (int)source.Size.Width;
        var height = (int)source.Size.Height;

        if (_screenshotBrushImage != null
            && _screenshotBrushImage.PixelSize.Width == width
            && _screenshotBrushImage.PixelSize.Height == height)
        {
            return;
        }

        _screenshotBrushImage?.Dispose();
        _screenshotBrushImage = new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);

        var stride = width * 4;
        var buffer = new byte[height * stride];
        unsafe
        {
            fixed (byte* ptr = buffer)
            {
                source.CopyPixels(new PixelRect(0, 0, width, height), (IntPtr)ptr, buffer.Length, stride);
            }
        }

        using (var fb = _screenshotBrushImage.Lock())
        {
            unsafe
            {
                fixed (byte* ptr = buffer)
                {
                    Buffer.MemoryCopy(ptr, (void*)fb.Address, fb.RowBytes * height, buffer.Length);
                }
            }
        }
    }

    public void StartScreenshotBrush(Point point)
    {
        if (!IsScreenshotBrushMode)
        {
            return;
        }

        EnsureScreenshotBrushImage();
        DrawBrushLine(point, point);
    }

    public void UpdateScreenshotBrush(Point point, Point previousPoint)
    {
        if (!IsScreenshotBrushMode)
        {
            return;
        }

        EnsureScreenshotBrushImage();
        DrawBrushLine(previousPoint, point);
    }

    private void DrawBrushLine(Point start, Point end)
    {
        if (_screenshotBrushImage == null)
        {
            return;
        }

        var width = _screenshotBrushImage.PixelSize.Width;
        var height = _screenshotBrushImage.PixelSize.Height;

        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var steps = (int)Math.Max(Math.Abs(dx), Math.Abs(dy));
        if (steps <= 0)
        {
            steps = 1;
        }

        using var fb = _screenshotBrushImage.Lock();
        unsafe
        {
            var dstPtr = (byte*)fb.Address;
            var stride = fb.RowBytes;
            var radius = Math.Max(1, ScreenshotBrushSize);
            var half = radius / 2;

            for (var i = 0; i <= steps; i++)
            {
                var x = start.X + dx * i / steps;
                var y = start.Y + dy * i / steps;
                var cx = (int)Math.Round(x);
                var cy = (int)Math.Round(y);

                for (var yy = cy - half; yy <= cy + half; yy++)
                {
                    if (yy < 0 || yy >= height) continue;
                    var rowStart = yy * stride;
                    for (var xx = cx - half; xx <= cx + half; xx++)
                    {
                        if (xx < 0 || xx >= width) continue;
                        var index = rowStart + xx * 4;
                        dstPtr[index] = 0;
                        dstPtr[index + 1] = 255;
                        dstPtr[index + 2] = 0;
                        dstPtr[index + 3] = 255;
                    }
                }
            }
        }

        OnPropertyChanged(nameof(LiveViewDisplayImage));
    }

    private static byte[] BitmapToBytes(Bitmap bitmap)
    {
        using var stream = new MemoryStream();
        bitmap.Save(stream);
        return stream.ToArray();
    }

    private static bool TrySetEncodedData(MaaImageBuffer buffer, byte[] data)
    {
        var method = buffer.GetType().GetMethod("TrySetEncodedData", new[]
        {
            typeof(byte[])
        });
        if (method == null)
        {
            return false;
        }

        return method.Invoke(buffer, new object[]
            {
                data
            }) is bool result
            && result;
    }

    private static string BuildOcrTaskJson(Rect rect)
    {
        var payload = new
        {
            recognition = "OCR",
            roi = new[]
            {
                (int)Math.Round(rect.X),
                (int)Math.Round(rect.Y),
                Math.Max(1, (int)Math.Round(rect.Width)),
                Math.Max(1, (int)Math.Round(rect.Height))
            }
        };

        return JsonConvert.SerializeObject(payload, new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Ignore
        });
    }

    private sealed class OcrRecognitionQuery
    {
        [JsonProperty("best")]
        public OcrRecognitionResult? Best { get; set; }
    }

    private sealed class OcrRecognitionResult
    {
        [JsonProperty("text")]
        public string? Text { get; set; }
    }

    private readonly struct ColorRange(
        int lowerR,
        int lowerG,
        int lowerB,
        int upperR,
        int upperG,
        int upperB,
        int lowerH,
        int lowerS,
        int lowerV,
        int upperH,
        int upperS,
        int upperV,
        int lowerGray,
        int upperGray,
        int mode)
    {
        public int LowerR { get; } = lowerR;
        public int LowerG { get; } = lowerG;
        public int LowerB { get; } = lowerB;
        public int UpperR { get; } = upperR;
        public int UpperG { get; } = upperG;
        public int UpperB { get; } = upperB;
        public int LowerH { get; } = lowerH;
        public int LowerS { get; } = lowerS;
        public int LowerV { get; } = lowerV;
        public int UpperH { get; } = upperH;
        public int UpperS { get; } = upperS;
        public int UpperV { get; } = upperV;
        public int LowerGray { get; } = lowerGray;
        public int UpperGray { get; } = upperGray;
        public int Mode { get; } = mode;
    }

    private bool TryGetColorRange(out ColorRange range)
    {
        range = default;
        if (!int.TryParse(RgbLowerR, out var lowerR)) return false;
        if (!int.TryParse(RgbLowerG, out var lowerG)) return false;
        if (!int.TryParse(RgbLowerB, out var lowerB)) return false;
        if (!int.TryParse(RgbUpperR, out var upperR)) return false;
        if (!int.TryParse(RgbUpperG, out var upperG)) return false;
        if (!int.TryParse(RgbUpperB, out var upperB)) return false;

        if (!int.TryParse(HsvLowerH, out var lowerH)) return false;
        if (!int.TryParse(HsvLowerS, out var lowerS)) return false;
        if (!int.TryParse(HsvLowerV, out var lowerV)) return false;
        if (!int.TryParse(HsvUpperH, out var upperH)) return false;
        if (!int.TryParse(HsvUpperS, out var upperS)) return false;
        if (!int.TryParse(HsvUpperV, out var upperV)) return false;

        if (!int.TryParse(GrayLower, out var lowerGray)) return false;
        if (!int.TryParse(GrayUpper, out var upperGray)) return false;

        range = new ColorRange(lowerR, lowerG, lowerB, upperR, upperG, upperB,
            lowerH, lowerS, lowerV, upperH, upperS, upperV, lowerGray, upperGray, ColorMode);
        return true;
    }

    private static bool IsColorInRange(ColorRange range, byte r, byte g, byte b)
    {
        return range.Mode switch
        {
            0 => r >= range.LowerR
                && r <= range.UpperR
                && g >= range.LowerG
                && g <= range.UpperG
                && b >= range.LowerB
                && b <= range.UpperB,
            1 => IsHsvInRange(range, r, g, b),
            _ => IsGrayInRange(range, r, g, b)
        };
    }

    private static bool IsHsvInRange(ColorRange range, byte r, byte g, byte b)
    {
        RgbToHsv(r, g, b, out var h, out var s, out var v);
        return h >= range.LowerH
            && h <= range.UpperH
            && s >= range.LowerS
            && s <= range.UpperS
            && v >= range.LowerV
            && v <= range.UpperV;
    }

    private static bool IsGrayInRange(ColorRange range, byte r, byte g, byte b)
    {
        var gray = (r + g + b) / 3;
        return gray >= range.LowerGray && gray <= range.UpperGray;
    }

    private void RefreshSelectionRects()
    {
        var primary = IsTargetMode ? _originTargetRect : _roiRect;
        SelectionRect = primary;
        HasSelection = primary.Width > 0 && primary.Height > 0;

        SecondarySelectionRect = _targetRect;
        HasSecondarySelection = _targetRect.Width > 0 && _targetRect.Height > 0;
    }

    private void UpdateSwipeArrow()
    {
        if (!double.TryParse(SwipeStartX, out var startX)
            || !double.TryParse(SwipeStartY, out var startY)
            || !double.TryParse(SwipeEndX, out var endX)
            || !double.TryParse(SwipeEndY, out var endY))
        {
            HasSwipeArrow = false;
            SwipeArrowGeometry = null;
            return;
        }

        var start = new Point(startX, startY);
        var end = new Point(endX, endY);
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);
        if (length < 1)
        {
            HasSwipeArrow = false;
            SwipeArrowGeometry = null;
            return;
        }

        var angle = Math.Atan2(dy, dx);
        const double headLength = 12;
        const double headAngle = Math.PI / 6;

        var left = new Point(
            end.X - headLength * Math.Cos(angle - headAngle),
            end.Y - headLength * Math.Sin(angle - headAngle));
        var right = new Point(
            end.X - headLength * Math.Cos(angle + headAngle),
            end.Y - headLength * Math.Sin(angle + headAngle));

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(start, false);
            ctx.LineTo(end);
            ctx.BeginFigure(end, false);
            ctx.LineTo(left);
            ctx.BeginFigure(end, false);
            ctx.LineTo(right);
        }

        SwipeArrowGeometry = geometry;
        HasSwipeArrow = true;
    }

    private void UpdateSelectionPreviewWithExpanded(Rect origin, Rect expanded)
    {
        SelectionRect = origin;
        HasSelection = origin.Width > 0 && origin.Height > 0;
        SecondarySelectionRect = expanded;
        HasSecondarySelection = expanded.Width > 0 && expanded.Height > 0;
    }

    private void ClearSelectionPreview()
    {
        SelectionRect = default;
        SecondarySelectionRect = default;
        HasSelection = false;
        HasSecondarySelection = false;
    }

    private void UpdateSelectionWithSecondary(Rect rect, bool hasSelection, LiveViewRoiSelectionType selectionType)
    {
        switch (selectionType)
        {
            case LiveViewRoiSelectionType.OriginRoi:
                SelectionRect = rect;
                HasSelection = hasSelection;
                SecondarySelectionRect = _targetRect;
                HasSecondarySelection = _targetRect.Width > 0 && _targetRect.Height > 0;
                break;
            case LiveViewRoiSelectionType.OriginTarget:
                SelectionRect = rect;
                HasSelection = hasSelection;
                SecondarySelectionRect = _targetRect;
                HasSecondarySelection = _targetRect.Width > 0 && _targetRect.Height > 0;
                break;
            case LiveViewRoiSelectionType.TargetRoi:
                SecondarySelectionRect = rect;
                HasSecondarySelection = hasSelection;
                var primary = IsTargetMode ? _originTargetRect : _roiRect;
                SelectionRect = primary;
                HasSelection = primary.Width > 0 && primary.Height > 0;
                break;
        }
    }

    private static bool TryParseRect(string xText, string yText, string wText, string hText, out Rect rect)
    {
        rect = default;
        if (!double.TryParse(xText, out var x)) return false;
        if (!double.TryParse(yText, out var y)) return false;
        if (!double.TryParse(wText, out var w)) return false;
        if (!double.TryParse(hText, out var h)) return false;
        if (w <= 0 || h <= 0) return false;

        rect = new Rect(Math.Max(0, x), Math.Max(0, y), w, h);
        return true;
    }

    private static void RgbToHsv(byte r, byte g, byte b, out int h, out int s, out int v)
    {
        var rf = r / 255.0;
        var gf = g / 255.0;
        var bf = b / 255.0;

        var max = Math.Max(rf, Math.Max(gf, bf));
        var min = Math.Min(rf, Math.Min(gf, bf));
        var delta = max - min;

        double hue;
        if (delta == 0)
        {
            hue = 0;
        }
        else if (max == rf)
        {
            hue = 60 * (((gf - bf) / delta) % 6);
        }
        else if (max == gf)
        {
            hue = 60 * (((bf - rf) / delta) + 2);
        }
        else
        {
            hue = 60 * (((rf - gf) / delta) + 4);
        }

        if (hue < 0)
        {
            hue += 360;
        }

        var sat = max == 0 ? 0 : delta / max;
        h = (int)Math.Round(hue / 2);
        s = (int)Math.Round(sat * 255);
        v = (int)Math.Round(max * 255);
    }

    /// <summary>
    /// Live View 刷新率变化事件，参数为计算后的间隔（秒）
    /// </summary>
    public event Action<double>? LiveViewRefreshRateChanged;

    /// <summary>
    /// Live View 是否启用
    /// </summary>
    [ObservableProperty] private bool _enableLiveView =
        ConfigurationManager.Current.GetValue(ConfigurationKeys.EnableLiveView, true);

    /// <summary>
    /// Live View 刷新率（FPS），范围 1-60，默认 10
    /// </summary>
    [ObservableProperty] private double _liveViewRefreshRate =
        ConfigurationManager.Current.GetValue(ConfigurationKeys.LiveViewRefreshRate, 30.0);

    [ObservableProperty] private Bitmap? _liveViewImage;
    private WriteableBitmap? _liveViewWriteableBitmap;
    private int _liveViewProcessing;
    [ObservableProperty] private double _liveViewFps;
    private DateTime _liveViewFpsWindowStart = DateTime.UtcNow;
    private int _liveViewFrameCount;
    private int _liveViewImageCount;
    private int _liveViewImageNewestCount;

    private static int _liveViewSemaphoreCurrentCount = 2;
    private const int LiveViewSemaphoreMaxCount = 5;
    private static int _liveViewSemaphoreFailCount = 0;
    private static readonly SemaphoreSlim _liveViewSemaphore = new(_liveViewSemaphoreCurrentCount, LiveViewSemaphoreMaxCount);

    private readonly WriteableBitmap?[] _liveViewImageCache = new WriteableBitmap?[LiveViewSemaphoreMaxCount];
    public double GetLiveViewRefreshInterval() => 1.0 / LiveViewRefreshRate;
    /// <summary>
    /// Live View 是否可见（已连接且有图像）
    /// </summary>
    public bool IsLiveViewVisible => EnableLiveView && IsConnected && LiveViewImage != null;

    partial void OnIsConnectedChanged(bool value)
    {
        OnPropertyChanged(nameof(IsLiveViewVisible));
    }

    partial void OnLiveViewImageChanged(Bitmap? value)
    {
        OnPropertyChanged(nameof(IsLiveViewVisible));
        if (!IsLiveViewPaused)
        {
            OnPropertyChanged(nameof(LiveViewDisplayImage));
        }

        if (IsColorPreviewActive)
        {
            ApplyColorFilter();
        }
    }

    /// <summary>
    /// 更新 Live View 图像（仿 WPF：直接写入缓冲）
    /// </summary>
    public async Task UpdateLiveViewImageAsync(MaaImageBuffer? buffer)
    {
        if (IsLiveViewPaused)
        {
            buffer?.Dispose();
            return;
        }

        if (!await _liveViewSemaphore.WaitAsync(0))
        {
            if (++_liveViewSemaphoreFailCount < 3)
            {
                buffer?.Dispose();
                return;
            }

            _liveViewSemaphoreFailCount = 0;

            if (_liveViewSemaphoreCurrentCount < LiveViewSemaphoreMaxCount)
            {
                _liveViewSemaphoreCurrentCount++;
                _liveViewSemaphore.Release();
                LoggerHelper.Info($"LiveView Semaphore Full, increase semaphore count to {_liveViewSemaphoreCurrentCount}");
            }

            buffer?.Dispose();
            return;
        }

        try
        {
            var count = Interlocked.Increment(ref _liveViewImageCount);
            var index = count % _liveViewImageCache.Length;

            if (buffer == null)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    LiveViewImage = null;
                    _liveViewWriteableBitmap?.Dispose();
                    _liveViewWriteableBitmap = null;
                    Array.Fill(_liveViewImageCache, null);
                    _liveViewImageNewestCount = 0;
                    _liveViewImageCount = 0;
                });
                return;
            }

            if (!buffer.TryGetRawData(out var rawData, out var width, out var height, out _))
            {
                return;
            }

            if (width <= 0 || height <= 0)
            {
                return;
            }

            if (count <= _liveViewImageNewestCount)
            {
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _liveViewImageCache[index] = WriteBgrToBitmap(rawData, width, height, buffer.Channels, _liveViewImageCache[index]);
                LiveViewImage = _liveViewImageCache[index];
            });

            Interlocked.Exchange(ref _liveViewImageNewestCount, count);
            _liveViewSemaphoreFailCount = 0;

            var now = DateTime.UtcNow;
            Interlocked.Increment(ref _liveViewFrameCount);
            var totalSeconds = (now - _liveViewFpsWindowStart).TotalSeconds;
            if (totalSeconds >= 1)
            {
                var frameCount = Interlocked.Exchange(ref _liveViewFrameCount, 0);
                _liveViewFpsWindowStart = now;
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    LiveViewFps = frameCount / totalSeconds;
                });
            }
        }
        finally
        {
            buffer?.Dispose();
            _liveViewSemaphore.Release();
        }
    }

    private static WriteableBitmap WriteBgrToBitmap(IntPtr bgrData, int width, int height, int channels, WriteableBitmap? targetBitmap)
    {
        const int dstBytesPerPixel = 4;

        if (width <= 0 || height <= 0)
        {
            return targetBitmap
                ?? new WriteableBitmap(
                    new PixelSize(1, 1),
                    new Vector(96, 96),
                    PixelFormat.Bgra8888,
                    AlphaFormat.Premul);
        }

        if (targetBitmap == null
            || targetBitmap.PixelSize.Width != width
            || targetBitmap.PixelSize.Height != height)
        {
            targetBitmap?.Dispose();
            targetBitmap = new WriteableBitmap(
                new PixelSize(width, height),
                new Vector(96, 96),
                PixelFormat.Bgra8888,
                AlphaFormat.Premul);
        }

        using var framebuffer = targetBitmap.Lock();
        unsafe
        {
            var dstStride = framebuffer.RowBytes;
            if (dstStride <= 0)
            {
                return targetBitmap;
            }

            var dstPtr = (byte*)framebuffer.Address;

            if (channels == 4)
            {
                var srcStride = width * dstBytesPerPixel;
                var rowCopy = Math.Min(srcStride, dstStride);
                var srcPtr = (byte*)bgrData;
                for (var y = 0; y < height; y++)
                {
                    Buffer.MemoryCopy(srcPtr + y * srcStride, dstPtr + y * dstStride, dstStride, rowCopy);
                }

                return targetBitmap;
            }

            if (channels == 3)
            {
                var srcStride = width * 3;
                var rowBuffer = ArrayPool<byte>.Shared.Rent(width * dstBytesPerPixel);
                try
                {
                    var srcPtr = (byte*)bgrData;
                    var rowCopy = Math.Min(width * dstBytesPerPixel, dstStride);
                    for (var y = 0; y < height; y++)
                    {
                        var srcRow = srcPtr + y * srcStride;
                        for (var x = 0; x < width; x++)
                        {
                            var srcIndex = x * 3;
                            var dstIndex = x * dstBytesPerPixel;
                            rowBuffer[dstIndex] = srcRow[srcIndex];
                            rowBuffer[dstIndex + 1] = srcRow[srcIndex + 1];
                            rowBuffer[dstIndex + 2] = srcRow[srcIndex + 2];
                            rowBuffer[dstIndex + 3] = 255;
                        }

                        fixed (byte* rowPtr = rowBuffer)
                        {
                            Buffer.MemoryCopy(rowPtr, dstPtr + y * dstStride, dstStride, rowCopy);
                        }
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(rowBuffer);
                }

                return targetBitmap;
            }
        }

        return targetBitmap;
    }

    #endregion
}
