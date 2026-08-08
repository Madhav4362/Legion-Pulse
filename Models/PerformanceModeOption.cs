using CommunityToolkit.Mvvm.ComponentModel;

namespace LegionPulse.Models;

public sealed partial class PerformanceModeOption : ObservableObject
{
    public PerformanceModeOption(string name, bool isSelected = false)
    {
        Name = name;
        IsSelected = isSelected;
    }

    public string Name { get; }

    [ObservableProperty]
    private bool isSelected;
}
