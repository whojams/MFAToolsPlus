using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using MFAToolsPlus.Helper;

namespace MFAToolsPlus.Views.UserControls.Settings;

public partial class PerformanceUserControl : UserControl
{
    public PerformanceUserControl()
    {
        DataContext = Instances.PerformanceUserControlModel;
        InitializeComponent();
    }
}

