using CommunityToolkit.Mvvm.ComponentModel;

namespace LegionPulse.Models;

public sealed partial class BatteryOptimizationProfile : ObservableObject
{
    [ObservableProperty] private string name = "Smart Battery Saver";
    [ObservableProperty] private bool enableWindowsBatterySaver = true;
    [ObservableProperty] private bool limitRefreshRate60Hz = true;
    [ObservableProperty] private bool switchQuietThermalMode = true;
    [ObservableProperty] private bool turnOffKeyboardRgb = true;
    [ObservableProperty] private bool disableAlwaysOnUsb = true;
    [ObservableProperty] private bool enableHybridIgpuMode = true;
    [ObservableProperty] private bool disableOverdrive = true;
    [ObservableProperty] private bool reduceBrightness = true;
    [ObservableProperty] private bool enableDgpuPowerSaving = true;
    [ObservableProperty] private bool disableCpuTurboBoost = true;
    [ObservableProperty] private bool enableConservationMode = true;

    public void SetAll(bool enable)
    {
        EnableWindowsBatterySaver = enable;
        LimitRefreshRate60Hz = enable;
        SwitchQuietThermalMode = enable;
        TurnOffKeyboardRgb = enable;
        DisableAlwaysOnUsb = enable;
        EnableHybridIgpuMode = enable;
        DisableOverdrive = enable;
        ReduceBrightness = enable;
        EnableDgpuPowerSaving = enable;
        DisableCpuTurboBoost = enable;
        EnableConservationMode = enable;
    }
}
