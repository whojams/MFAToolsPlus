using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using MFAToolsPlus.Helper;

namespace MFAToolsPlus.Views.Pages;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        DataContext = Instances.SettingsViewModel;
        InitializeComponent();
    }
}

