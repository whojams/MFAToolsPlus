using CommunityToolkit.Mvvm.ComponentModel;

using MFAToolsPlus.ViewModels;
using MFAToolsPlus.ViewModels.UsersControls.Settings;
using SukiUI.Models;


namespace MFAToolsPlus.Helper.Other;

public partial class ThemeItem(SukiColorTheme theme, GuiSettingsUserControlModel settingsModel) : ViewModelBase
{
    [ObservableProperty] private bool _isSelected = settingsModel.CurrentColorTheme.DisplayName == theme.DisplayName;

    [ObservableProperty] private SukiColorTheme _theme = theme;

    partial void OnIsSelectedChanged(bool value)
    {
        if (value)
            Instances.GuiSettingsUserControlModel.CurrentColorTheme = Theme;
    }
}
