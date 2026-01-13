using Avalonia.Controls;
using MFAToolsPlus.Helper;

namespace MFAToolsPlus.Views.UserControls.Settings;

public partial class ToolSettingsUserControl : UserControl
{
    public ToolSettingsUserControl()
    {
        DataContext = Instances.ToolSettingsUserControlModel;
        InitializeComponent();
    }
}