using CommunityToolkit.Mvvm.ComponentModel;

namespace LegionPulse.Models;

public sealed partial class HardwareCapabilities : ObservableObject
{
    [ObservableProperty] private bool isKeyboardRgbSupported = true;
    [ObservableProperty] private bool isAlwaysOnUsbSupported = true;
    [ObservableProperty] private bool isHybridModeSupported = true;
    [ObservableProperty] private bool isOverdriveSupported = true;
    [ObservableProperty] private bool isRefreshRateControlSupported = true;
    [ObservableProperty] private bool isCpuTurboControlSupported = true;
}
