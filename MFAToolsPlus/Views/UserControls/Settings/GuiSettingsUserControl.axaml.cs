using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using MFAToolsPlus.Extensions.MaaFW;
using MFAToolsPlus.Helper;
using MFAToolsPlus.Helper.Other;
using MFAToolsPlus.ViewModels.UsersControls.Settings;
using SukiUI;
using System;

namespace MFAToolsPlus.Views.UserControls.Settings;

public partial class GuiSettingsUserControl : UserControl
{
    public GuiSettingsUserControl()
    {
        DataContext = Instances.GuiSettingsUserControlModel;
        InitializeComponent();
    }

    private void Delete(object? sender, RoutedEventArgs e)
    {
        var menuItem = sender as MenuItem;
        if (menuItem?.DataContext is ThemeItem themeItemViewModel && DataContext is GuiSettingsUserControlModel vm)
        {
            if (vm.CurrentColorTheme != themeItemViewModel.Theme && !themeItemViewModel.IsSelected)
            {
                var theme = SukiTheme.GetInstance();
                theme.RemoveColorTheme(themeItemViewModel.Theme);
                vm.RemoveOtherColor(themeItemViewModel.Theme);
            }
        }
    }
}
