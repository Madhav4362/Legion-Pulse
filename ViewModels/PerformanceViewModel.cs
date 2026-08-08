using System;
using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LegionPulse.Models;
using LegionPulse.Services;

namespace LegionPulse.ViewModels;

public sealed partial class PerformanceViewModel : ViewModelBase
{
    private readonly ISystemControlService _controlService;
    private readonly ISystemMonitorService _monitorService;

    public PerformanceViewModel(ISystemControlService controlService, ISystemMonitorService monitorService)
    {
        _controlService = controlService;
        _monitorService = monitorService;

        PerformanceModes = DashboardViewModel.CreatePerformanceModes(_controlService.ActivePerformanceMode);

        _controlService.PerformanceModeChanged += OnPerformanceModeChanged;
        _monitorService.MetricsUpdated += OnMetricsUpdated;

        UpdateProperties(_monitorService.CurrentMetrics);
    }

    public ObservableCollection<PerformanceModeOption> PerformanceModes { get; }

    [ObservableProperty] private string cpuUsageText = "23%";
    [ObservableProperty] private string cpuTempText = "68°C";
    [ObservableProperty] private string cpuClockText = "3.80 GHz";
    [ObservableProperty] private string cpuNameText = "Intel Core i9-14900HX";
    [ObservableProperty] private string cpuPowerText = "45W";
    [ObservableProperty] private string gpuUsageText = "41%";
    [ObservableProperty] private string gpuTempText = "72°C";
    [ObservableProperty] private string gpuVramText = "5.2 GB";
    [ObservableProperty] private string gpuNameText = "NVIDIA RTX 4070";
    [ObservableProperty] private string gpuPowerText = "80W";
    [ObservableProperty] private string memoryUsedText = "8.4 GB";
    [ObservableProperty] private string memoryText = "8.4 / 16 GB";
    [ObservableProperty] private string memoryUsagePercentText = "53% Used";
    [ObservableProperty] private string powerDrawText = "15 W";
    [ObservableProperty] private string powerBreakdownText = "CPU: 45W | GPU: 80W";
    [ObservableProperty] private string activePerformanceModeName = "Balanced Mode";
    [ObservableProperty] private string systemModelNameText = "Lenovo Legion 7i Gen 9";
    [ObservableProperty] private string thermalStateText = "Normal";

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
        CpuUsageText = $"{m.CpuUsagePercent:F0}%";
        CpuTempText = $"{m.CpuTemperature:F0}°C";
        CpuClockText = $"{m.CpuClockGhz:F2} GHz";
        CpuNameText = m.CpuName;
        CpuPowerText = $"{m.CpuPowerWatts:F0}W";
        GpuUsageText = $"{m.GpuUsagePercent:F0}%";
        GpuTempText = $"{m.GpuTemperature:F0}°C";
        GpuVramText = $"{m.GpuVramUsedGb:F1} GB";
        GpuNameText = m.GpuName;
        GpuPowerText = $"{m.GpuPowerWatts:F0}W";
        MemoryUsedText = $"{m.MemoryUsedGb:F1} GB";
        MemoryText = $"{m.MemoryUsedGb:F1} / {m.MemoryTotalGb:F0} GB";
        MemoryUsagePercentText = $"{m.MemoryUsagePercent:F0}% Used";
        PowerDrawText = $"{m.PowerDrawWatts:F1} W";
        PowerBreakdownText = $"CPU: {m.CpuPowerWatts:F0}W | GPU: {m.GpuPowerWatts:F0}W";
        SystemModelNameText = m.SystemModelName;
        ThermalStateText = m.ThermalStateText;
    }
}
