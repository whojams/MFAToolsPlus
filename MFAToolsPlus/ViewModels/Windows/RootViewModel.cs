using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;

namespace MFAToolsPlus.ViewModels;

public partial class RootViewModel : ViewModelBase
{
    [ObservableProperty] private bool _isWindowVisible = true;
    
    [ObservableProperty] private string? _windowUpdateInfo = "";
    
    [ObservableProperty] private Action? _tempResourceUpdateAction;
    
    [ObservableProperty] private bool _isUpdating;
    
    [ObservableProperty] private bool _idle = true;
    
    public static string Version => "v1.1.0";

    public void SetUpdating(bool isUpdating)
    {
        IsUpdating = isUpdating;
    }
    
    [RelayCommand]
    private void TryUpdate()
    {
        TempResourceUpdateAction?.Invoke();
    }
    
    [RelayCommand]
    public void ToggleVisible()
    {
        IsWindowVisible = !IsWindowVisible;
    }
}
