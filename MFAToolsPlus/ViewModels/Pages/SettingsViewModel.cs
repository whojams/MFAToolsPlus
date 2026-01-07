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
        _hotKeyLinkStart = MFAHotKey.Parse(GlobalConfiguration.GetValue(ConfigurationKeys.LinkStart, ""));

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

        // SetHotKey(ref _hotKeyLinkStart, _hotKeyLinkStart, ConfigurationKeys.LinkStart,
        //     Instances.TaskQueueViewModel.ToggleCommand);
    }

    #region HotKey
    [ObservableProperty] private bool _enableHotKey = true;
    private MFAHotKey _hotKeyShowGui = MFAHotKey.NOTSET;

    public MFAHotKey HotKeyShowGui
    {
        get => _hotKeyShowGui;
        set => SetHotKey(ref _hotKeyShowGui, value, ConfigurationKeys.ShowGui, Instances.RootViewModel.ToggleVisibleCommand);
    }

    private MFAHotKey _hotKeyLinkStart = MFAHotKey.NOTSET;

    // public MFAHotKey HotKeyLinkStart
    // {
    //     get => _hotKeyLinkStart;
    //     set => SetHotKey(ref _hotKeyLinkStart, value, ConfigurationKeys.LinkStart, Instances.TaskQueueViewModel.ToggleCommand);
    // }

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
