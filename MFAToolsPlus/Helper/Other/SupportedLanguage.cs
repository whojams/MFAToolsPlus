using CommunityToolkit.Mvvm.ComponentModel;
using MFAToolsPlus.ViewModels;

namespace MFAToolsPlus.Helper.Other;

public partial class SupportedLanguage(string key, string name) : ObservableObject
{
    [ObservableProperty] private string _name = name;
    [ObservableProperty] private string _key = key;
}
