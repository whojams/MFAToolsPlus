using CommunityToolkit.Mvvm.ComponentModel;

namespace MFAToolsPlus.Helper.Other;


public partial class TripletRow : ObservableObject
{
    [ObservableProperty] private string _first;
    [ObservableProperty] private string _second;
    [ObservableProperty] private string _third;

    public TripletRow(string first = "0", string second = "0", string third = "0")
    {
        _first = first;
        _second = second;
        _third = third;
    }
}

public partial class SingleRow : ObservableObject
{
    [ObservableProperty] private string _value;

    public SingleRow(string value = "0")
    {
        _value = value;
    }
}