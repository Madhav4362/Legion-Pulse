using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LegionPulse.Services;

namespace LegionPulse.ViewModels;

public sealed partial class SettingsViewModel : ViewModelBase
{
    private readonly ISystemControlService _controlService;

    public SettingsViewModel(ISystemControlService controlService)
    {
        _controlService = controlService;
        selectedAppearance = _controlService.CurrentTheme;
    }

    [ObservableProperty]
    private string selectedAppearance = "Dark";

    public bool IsDarkSelected => SelectedAppearance.Equals("Dark", StringComparison.OrdinalIgnoreCase);

    public bool IsLightSelected => SelectedAppearance.Equals("Light", StringComparison.OrdinalIgnoreCase);

    partial void OnSelectedAppearanceChanged(string value)
    {
        OnPropertyChanged(nameof(IsDarkSelected));
        OnPropertyChanged(nameof(IsLightSelected));
    }

    [RelayCommand]
    private void SelectAppearance(string? appearance)
    {
        if (appearance is "Dark" or "Light")
        {
            SelectedAppearance = appearance;
            _controlService.SetTheme(appearance);
        }
    }
}
