using Avalonia.Collections;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MFAToolsPlus.Configuration;
using MFAToolsPlus.Helper;
using MFAToolsPlus.Helper.Other;
using MFAToolsPlus.ViewModels;
using SukiUI;
using SukiUI.Enums;
using SukiUI.Models;
using System;
using System.IO;
using System.Linq;
using System.Windows.Input;

namespace MFAToolsPlus.ViewModels.Pages;

public partial class SettingsViewModel : ViewModelBase
{
    private bool _hotkeysInitialized;

    protected override void Initialize()
    {
        _hotKeyShowGui = MFAHotKey.Parse(GlobalConfiguration.GetValue(ConfigurationKeys.ShowGui, ""));
        _hotKeyPause = MFAHotKey.Parse(GlobalConfiguration.GetValue(ConfigurationKeys.Pause, ""));
        _hotKeyToolRoi = MFAHotKey.Parse(GlobalConfiguration.GetValue(ConfigurationKeys.HotKeyToolRoi, ""));
        _hotKeyToolColor = MFAHotKey.Parse(GlobalConfiguration.GetValue(ConfigurationKeys.HotKeyToolColor, ""));
        _hotKeyToolOcr = MFAHotKey.Parse(GlobalConfiguration.GetValue(ConfigurationKeys.HotKeyToolOcr, ""));
        _hotKeyToolScreenshot = MFAHotKey.Parse(GlobalConfiguration.GetValue(ConfigurationKeys.HotKeyToolScreenshot, ""));
        _hotKeyToolSwipe = MFAHotKey.Parse(GlobalConfiguration.GetValue(ConfigurationKeys.HotKeyToolSwipe, ""));
        _hotKeyToolKey = MFAHotKey.Parse(GlobalConfiguration.GetValue(ConfigurationKeys.HotKeyToolKey, ""));
        _hotKeyToolNeuralNetworkDetect = MFAHotKey.Parse(GlobalConfiguration.GetValue(ConfigurationKeys.HotKeyToolNeuralNetworkDetect, ""));
        _hotKeyToolNone = MFAHotKey.Parse(GlobalConfiguration.GetValue(ConfigurationKeys.HotKeyToolNone, ""));

        DispatcherHelper.PostOnMainThread(InitializeHotkeysAfterStartup);
    }

    private void InitializeHotkeysAfterStartup()
    {
        if (_hotkeysInitialized)
        {
            return;
        }

        _hotkeysInitialized = true;

        SetHotKey(ref _hotKeyShowGui, _hotKeyShowGui, ConfigurationKeys.ShowGui,
            Instances.RootViewModel.ToggleVisibleCommand);

        SetHotKey(ref _hotKeyPause, _hotKeyPause, ConfigurationKeys.Pause,
            Instances.ToolsViewModel.ToggleLiveViewPauseCommand);

        SetHotKey(ref _hotKeyToolRoi, _hotKeyToolRoi, ConfigurationKeys.HotKeyToolRoi, ActivateToolRoiCommand);
        SetHotKey(ref _hotKeyToolColor, _hotKeyToolColor, ConfigurationKeys.HotKeyToolColor, ActivateToolColorCommand);
        SetHotKey(ref _hotKeyToolOcr, _hotKeyToolOcr, ConfigurationKeys.HotKeyToolOcr, ActivateToolOcrCommand);
        SetHotKey(ref _hotKeyToolScreenshot, _hotKeyToolScreenshot, ConfigurationKeys.HotKeyToolScreenshot, ActivateToolScreenshotCommand);
        SetHotKey(ref _hotKeyToolSwipe, _hotKeyToolSwipe, ConfigurationKeys.HotKeyToolSwipe, ActivateToolSwipeCommand);
        SetHotKey(ref _hotKeyToolKey, _hotKeyToolKey, ConfigurationKeys.HotKeyToolKey, ActivateToolKeyCommand);
        SetHotKey(ref _hotKeyToolNeuralNetworkDetect, _hotKeyToolNeuralNetworkDetect, ConfigurationKeys.HotKeyToolNeuralNetworkDetect, ActivateToolNeuralNetworkDetectCommand);
        SetHotKey(ref _hotKeyToolNone, _hotKeyToolNone, ConfigurationKeys.HotKeyToolNone, ActivateToolNoneCommand);
    }

    [RelayCommand]
    private void ActivateToolRoi() => Instances.ToolsViewModel.ActivateToolCommand.Execute(LiveViewToolMode.Roi);

    [RelayCommand]
    private void ActivateToolColor() => Instances.ToolsViewModel.ActivateToolCommand.Execute(LiveViewToolMode.ColorPick);

    [RelayCommand]
    private void ActivateToolOcr() => Instances.ToolsViewModel.ActivateToolCommand.Execute(LiveViewToolMode.Ocr);

    [RelayCommand]
    private void ActivateToolScreenshot() => Instances.ToolsViewModel.ActivateToolCommand.Execute(LiveViewToolMode.Screenshot);

    [RelayCommand]
    private void ActivateToolSwipe() => Instances.ToolsViewModel.ActivateToolCommand.Execute(LiveViewToolMode.Swipe);

    [RelayCommand]
    private void ActivateToolKey() => Instances.ToolsViewModel.ActivateToolCommand.Execute(LiveViewToolMode.Key);

    [RelayCommand]
    private void ActivateToolNeuralNetworkDetect() => Instances.ToolsViewModel.ActivateToolCommand.Execute(LiveViewToolMode.NeuralNetworkDetect);

    [RelayCommand]
    private void ActivateToolNone() => Instances.ToolsViewModel.ActivateToolCommand.Execute(LiveViewToolMode.None);

    #region HotKey
    [ObservableProperty] private bool _enableHotKey = true;
    private MFAHotKey _hotKeyShowGui = MFAHotKey.NOTSET;

    public MFAHotKey HotKeyShowGui
    {
        get => _hotKeyShowGui;
        set => SetHotKey(ref _hotKeyShowGui, value, ConfigurationKeys.ShowGui, Instances.RootViewModel.ToggleVisibleCommand);
    }

    private MFAHotKey _hotKeyPause = MFAHotKey.NOTSET;

    public MFAHotKey HotKeyPause
    {
        get => _hotKeyPause;
        set => SetHotKey(ref _hotKeyPause, value, ConfigurationKeys.Pause, Instances.ToolsViewModel.ToggleLiveViewPauseCommand);
    }

    private MFAHotKey _hotKeyToolRoi = MFAHotKey.NOTSET;
    public MFAHotKey HotKeyToolRoi
    {
        get => _hotKeyToolRoi;
        set => SetHotKey(ref _hotKeyToolRoi, value, ConfigurationKeys.HotKeyToolRoi, ActivateToolRoiCommand);
    }

    private MFAHotKey _hotKeyToolColor = MFAHotKey.NOTSET;
    public MFAHotKey HotKeyToolColor
    {
        get => _hotKeyToolColor;
        set => SetHotKey(ref _hotKeyToolColor, value, ConfigurationKeys.HotKeyToolColor, ActivateToolColorCommand);
    }

    private MFAHotKey _hotKeyToolOcr = MFAHotKey.NOTSET;
    public MFAHotKey HotKeyToolOcr
    {
        get => _hotKeyToolOcr;
        set => SetHotKey(ref _hotKeyToolOcr, value, ConfigurationKeys.HotKeyToolOcr, ActivateToolOcrCommand);
    }

    private MFAHotKey _hotKeyToolScreenshot = MFAHotKey.NOTSET;
    public MFAHotKey HotKeyToolScreenshot
    {
        get => _hotKeyToolScreenshot;
        set => SetHotKey(ref _hotKeyToolScreenshot, value, ConfigurationKeys.HotKeyToolScreenshot, ActivateToolScreenshotCommand);
    }

    private MFAHotKey _hotKeyToolSwipe = MFAHotKey.NOTSET;
    public MFAHotKey HotKeyToolSwipe
    {
        get => _hotKeyToolSwipe;
        set => SetHotKey(ref _hotKeyToolSwipe, value, ConfigurationKeys.HotKeyToolSwipe, ActivateToolSwipeCommand);
    }

    private MFAHotKey _hotKeyToolKey = MFAHotKey.NOTSET;
    public MFAHotKey HotKeyToolKey
    {
        get => _hotKeyToolKey;
        set => SetHotKey(ref _hotKeyToolKey, value, ConfigurationKeys.HotKeyToolKey, ActivateToolKeyCommand);
    }

    private MFAHotKey _hotKeyToolNeuralNetworkDetect = MFAHotKey.NOTSET;
    public MFAHotKey HotKeyToolNeuralNetworkDetect
    {
        get => _hotKeyToolNeuralNetworkDetect;
        set => SetHotKey(ref _hotKeyToolNeuralNetworkDetect, value, ConfigurationKeys.HotKeyToolNeuralNetworkDetect, ActivateToolNeuralNetworkDetectCommand);
    }

    private MFAHotKey _hotKeyToolNone = MFAHotKey.NOTSET;
    public MFAHotKey HotKeyToolNone
    {
        get => _hotKeyToolNone;
        set => SetHotKey(ref _hotKeyToolNone, value, ConfigurationKeys.HotKeyToolNone, ActivateToolNoneCommand);
    }

    public void SetHotKey(ref MFAHotKey value, MFAHotKey? newValue, string type, ICommand command)
    {
        if (newValue != null)
        {
            if (!GlobalHotkeyService.Register(newValue.Gesture, command))
            {
                newValue = MFAHotKey.ERROR;
            }
            GlobalConfiguration.SetValue(type, newValue.ToString());
            SetProperty(ref value, newValue);
        }
    }

    #endregion HotKey
    
    #region 资源

    [ObservableProperty] private bool _showResourceIssues;
    [ObservableProperty] private string _resourceIssues = string.Empty;
    [ObservableProperty] private string _resourceGithub = string.Empty;

    [ObservableProperty] private string _resourceContact = string.Empty;
    [ObservableProperty] private string _resourceDescription = string.Empty;
    [ObservableProperty] private string _resourceLicense = string.Empty;
    [ObservableProperty] private bool _hasResourceContact;
    [ObservableProperty] private bool _hasResourceDescription;
    [ObservableProperty] private bool _hasResourceLicense;

    #endregion
}
