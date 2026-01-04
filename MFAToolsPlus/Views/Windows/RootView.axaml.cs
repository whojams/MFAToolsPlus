using Avalonia.Controls;

namespace MFAToolsPlus.Views;

public partial class RootView : Window
{
    public RootView()
    {
        InitializeComponent();
    }
    
    public void BeforeClosed()
    {
        BeforeClosed(false, true);
    }
    
    public void BeforeClosed(bool noLog, bool stopTask)
    {
        
    }
}
