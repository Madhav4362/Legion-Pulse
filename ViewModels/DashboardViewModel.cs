using System;
using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LegionPulse.Models;
using LegionPulse.Services;

namespace LegionPulse.ViewModels;

public sealed partial class DashboardViewModel : ViewModelBase
{
    private readonly INavigationService _navigationService;
    private readonly ISystemMonitorService _monitorService;
    private readonly ISystemControlService _controlService;

    public DashboardViewModel(
        INavigationService navigationService,
        ISystemMonitorService monitorService,
        ISystemControlService controlService)
    {
        _navigationService = navigationService;
        _monitorService = monitorService;
        _controlService = controlService;

        PerformanceModes = CreatePerformanceModes(_controlService.ActivePerformanceMode);

        _monitorService.MetricsUpdated += OnMetricsUpdated;
        _controlService.PerformanceModeChanged += OnPerformanceModeChanged;

        UpdateProperties(_monitorService.CurrentMetrics);
    }

    public ObservableCollection<PerformanceModeOption> PerformanceModes { get; }

    public string GreetingName => Environment.UserName;

    [ObservableProperty] private string batteryPercentageText = "82%";
    [ObservableProperty] private double batteryPercentage = 82;
    [ObservableProperty] private string remainingTimeText = "4h 37m remaining";
    [ObservableProperty] private string statusText = "Charging";
    [ObservableProperty] private string powerSourceText = "AC Adapter Connected";
    [ObservableProperty] private string batteryHealthText = "98%";
    [ObservableProperty] private string cycleCountText = "128";
    [ObservableProperty] private string fullCapacityWhText = "79.4 Wh";
    [ObservableProperty] private string cpuUsageText = "23%";
    [ObservableProperty] private string cpuUsageValText = "23";
    [ObservableProperty] private string gpuUsageText = "41%";
    [ObservableProperty] private string gpuUsageValText = "41";
    [ObservableProperty] private string powerDrawText = "15 W";
    [ObservableProperty] private string powerDrawValText = "15";
    [ObservableProperty] private string cpuNameText = "Intel Core i9-14900HX";
    [ObservableProperty] private string cpuClockText = "3.80 GHz";
    [ObservableProperty] private string cpuTempText = "68°C";
    [ObservableProperty] private string cpuFanText = "2200 RPM";
    [ObservableProperty] private string gpuNameText = "NVIDIA RTX 4070";
    [ObservableProperty] private string gpuClockText = "1850 MHz";
    [ObservableProperty] private string gpuTempText = "72°C";
    [ObservableProperty] private string gpuVramText = "5.2 GB";
    [ObservableProperty] private string activePerformanceModeName = "Balanced Mode";
    [ObservableProperty] private string adapterWattsText = "230W";
    [ObservableProperty] private string cpuPowerText = "45W";
    [ObservableProperty] private string gpuPowerText = "80W";
    [ObservableProperty] private string greetingTitleText = $"Welcome back {Environment.UserName}";
    [ObservableProperty] private string gpuCoreLabelText = "Core (41%)";
    [ObservableProperty] private string totalCpuGpuPowerText = "125W";

    [RelayCommand]
    private void ViewBatteryDetails() => _navigationService.NavigateTo(AppPage.Battery);

    [RelayCommand]
    private void SelectPerformanceMode(PerformanceModeOption? option)
    {
        if (option is null) return;

        _controlService.SetPerformanceMode(option.Name);
    }

    private void OnPerformanceModeChanged(object? sender, string selectedMode)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            ActivePerformanceModeName = $"{selectedMode} Mode";
            foreach (var mode in PerformanceModes)
            {
                mode.IsSelected = mode.Name.Equals(selectedMode, StringComparison.OrdinalIgnoreCase);
            }
        });
    }

    private void OnMetricsUpdated(object? sender, SystemMetrics m)
    {
        Application.Current?.Dispatcher.Invoke(() => UpdateProperties(m));
    }

    private void UpdateProperties(SystemMetrics m)
    {
        BatteryPercentage = m.BatteryPercentage;
        BatteryPercentageText = $"{m.BatteryPercentage}%";
        RemainingTimeText = m.RemainingTimeText;
        StatusText = m.StatusText;
        PowerSourceText = m.PowerSourceText;
        BatteryHealthText = $"{m.BatteryHealthPercent}%";
        CycleCountText = m.CycleCount.ToString();
        FullCapacityWhText = $"{m.FullCapacityWh:F1} Wh";
        CpuUsageText = $"{m.CpuUsagePercent:F0}%";
        CpuUsageValText = $"{m.CpuUsagePercent:F0}";
        GpuUsageText = $"{m.GpuUsagePercent:F0}%";
        GpuUsageValText = $"{m.GpuUsagePercent:F0}";
        PowerDrawText = $"{m.PowerDrawWatts:F1} W";
        PowerDrawValText = $"{m.PowerDrawWatts:F0}";
        CpuNameText = m.CpuName;
        CpuClockText = $"{m.CpuClockGhz:F2} GHz";
        CpuTempText = $"{m.CpuTemperature:F0}°C";
        CpuFanText = $"{m.CpuFanRpm} RPM";
        GpuNameText = m.GpuName;
        GpuClockText = $"{m.GpuClockMhz:F0} MHz";
        GpuTempText = $"{m.GpuTemperature:F0}°C";
        GpuVramText = $"{m.GpuVramUsedGb:F1} GB";
        AdapterWattsText = m.AdapterText;
        CpuPowerText = $"{m.CpuPowerWatts:F0}W";
        GpuPowerText = $"{m.GpuPowerWatts:F0}W";
        GpuCoreLabelText = $"Core ({m.GpuUsagePercent:F0}%)";
        TotalCpuGpuPowerText = $"{m.CpuPowerWatts + m.GpuPowerWatts:F0}W";
    }

    internal static ObservableCollection<PerformanceModeOption> CreatePerformanceModes(string selected)
    {
        return new ObservableCollection<PerformanceModeOption>
        {
            new("Quiet", selected.Equals("Quiet", StringComparison.OrdinalIgnoreCase)),
            new("Balanced", selected.Equals("Balanced", StringComparison.OrdinalIgnoreCase)),
            new("Performance", selected.Equals("Performance", StringComparison.OrdinalIgnoreCase))
        };
    }
}
