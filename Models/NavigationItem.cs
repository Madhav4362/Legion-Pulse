using CommunityToolkit.Mvvm.ComponentModel;

namespace LegionPulse.Models;

public sealed partial class NavigationItem : ObservableObject
{
    public NavigationItem(AppPage page, string label, string icon)
    {
        Page = page;
        Label = label;
        Icon = icon;
    }

    public AppPage Page { get; }

    public string Label { get; }

    public string Icon { get; }

    [ObservableProperty]
    private bool isSelected;
}
