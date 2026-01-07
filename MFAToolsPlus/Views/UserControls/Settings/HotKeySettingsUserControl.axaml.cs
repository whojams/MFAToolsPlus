using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using MFAToolsPlus.Helper;

namespace MFAToolsPlus.Views.UserControls.Settings;

public partial class HotKeySettingsUserControl : UserControl
{
    public HotKeySettingsUserControl()
    {
        DataContext = Instances.SettingsViewModel;
        InitializeComponent();
    }
}

