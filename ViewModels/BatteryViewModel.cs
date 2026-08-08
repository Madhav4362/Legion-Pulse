using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LegionPulse.Models;
using LegionPulse.Services;

namespace LegionPulse.ViewModels;

public sealed partial class BatteryViewModel : ViewModelBase
{
    private readonly ISystemControlService _controlService;
    private readonly ISystemMonitorService _monitorService;

    public BatteryViewModel(ISystemControlService controlService, ISystemMonitorService monitorService)
    {
        _controlService = controlService;
        _monitorService = monitorService;

        _controlService.SmartSaverChanged += OnSmartSaverChanged;
        _monitorService.MetricsUpdated += OnMetricsUpdated;

        ActiveProfile = _controlService.ActiveProfile;
        Capabilities = _controlService.SystemCapabilities;
        IsSmartSaverEnabled = _controlService.IsSmartSaverEnabled;

        UpdateProperties(_monitorService.CurrentMetrics);
        UpdateBatteryHistory();
    }

    [ObservableProperty] private bool isSmartSaverEnabled;
    [ObservableProperty] private bool isAdvancedOptionsExpanded = false;
    [ObservableProperty] private BatteryOptimizationProfile activeProfile;
    [ObservableProperty] private HardwareCapabilities capabilities;

    [ObservableProperty] private string energySaverErrorMessage = "";
    [ObservableProperty] private bool hasEnergySaverError = false;

    [ObservableProperty] private string batteryPercentageText = "82%";
    [ObservableProperty] private string remainingTimeText = "6h 18m";
    [ObservableProperty] private string statusText = "Charging";
    [ObservableProperty] private string powerSourceText = "AC Adapter Connected";
    [ObservableProperty] private string timeUntilFullText = "38 min";
    [ObservableProperty] private string batteryHealthText = "98%";
    [ObservableProperty] private string healthStatusText = "Excellent";
    [ObservableProperty] private string wearLevelText = "2%";
    [ObservableProperty] private string cycleCountText = "128";
    [ObservableProperty] private string manufacturerText = "LG";
    [ObservableProperty] private string chemistryText = "Li-ion";
    [ObservableProperty] private string serialNumberText = "Unavailable";
    [ObservableProperty] private string voltageText = "16.7 V";
    [ObservableProperty] private string powerDrawText = "11.2 W";
    [ObservableProperty] private string batteryTempText = "31°C";
    [ObservableProperty] private string chargeRateText = "42 W";
    [ObservableProperty] private string capacityUsedText = "14.5 Wh";
    [ObservableProperty] private string designCapacityText = "80.0 Wh";
    [ObservableProperty] private string fullCapacityText = "80.0 Wh";
    [ObservableProperty] private string adapterText = "230W Lenovo";

    // History & Period Selection
    [ObservableProperty] private string selectedPeriod = "Today";
    [ObservableProperty] private bool hasHistory = false;
    [ObservableProperty] private string chartPathData = "";
    [ObservableProperty] private string chartFillData = "";
    [ObservableProperty] private List<string> historyTimeLabels = new();

    public bool HasNoHistory => !HasHistory;

    partial void OnHasHistoryChanged(bool value)
    {
        OnPropertyChanged(nameof(HasNoHistory));
    }

    public bool IsTodaySelected => SelectedPeriod.Equals("Today", StringComparison.OrdinalIgnoreCase);
    public bool IsWeekSelected => SelectedPeriod.Equals("Week", StringComparison.OrdinalIgnoreCase);
    public bool IsMonthSelected => SelectedPeriod.Equals("Month", StringComparison.OrdinalIgnoreCase);

    public string SmartSaverStatus => IsSmartSaverEnabled ? "ENABLED" : "DISABLED";
    public string SmartSaverAction => IsSmartSaverEnabled ? "Disable Smart Battery Saver" : "Enable Smart Battery Saver";
    public string AdvancedOptionsArrow => IsAdvancedOptionsExpanded ? "\uE70E" : "\uE70D";

    partial void OnIsAdvancedOptionsExpandedChanged(bool value)
    {
        OnPropertyChanged(nameof(AdvancedOptionsArrow));
    }

    partial void OnSelectedPeriodChanged(string value)
    {
        OnPropertyChanged(nameof(IsTodaySelected));
        OnPropertyChanged(nameof(IsWeekSelected));
        OnPropertyChanged(nameof(IsMonthSelected));
        UpdateBatteryHistory();
    }

    [RelayCommand]
    private void SelectPeriod(string period)
    {
        if (!string.IsNullOrWhiteSpace(period))
        {
            SelectedPeriod = period;
        }
    }

    [RelayCommand]
    private void ToggleAdvancedOptions()
    {
        IsAdvancedOptionsExpanded = !IsAdvancedOptionsExpanded;
    }

    [RelayCommand]
    private void ToggleSmartSaver()
    {
        EnergySaverErrorMessage = "";
        HasEnergySaverError = false;

        if (IsSmartSaverEnabled)
        {
            _controlService.DisableSmartSaver();
        }
        else
        {
            bool success = _controlService.ApplyBatteryOptimizationProfile(ActiveProfile, out string err);
            if (!success && !string.IsNullOrEmpty(err))
            {
                EnergySaverErrorMessage = err;
                HasEnergySaverError = true;
            }
        }
    }

    [RelayCommand]
    private void ApplyCustomProfile()
    {
        EnergySaverErrorMessage = "";
        HasEnergySaverError = false;

        bool success = _controlService.ApplyBatteryOptimizationProfile(ActiveProfile, out string err);
        if (!success && !string.IsNullOrEmpty(err))
        {
            EnergySaverErrorMessage = err;
            HasEnergySaverError = true;
        }
    }

    [RelayCommand]
    private void SelectAllOptions()
    {
        ActiveProfile.SetAll(true);
    }

    [RelayCommand]
    private void DeselectAllOptions()
    {
        ActiveProfile.SetAll(false);
    }

    private void OnSmartSaverChanged(object? sender, bool enabled)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            IsSmartSaverEnabled = enabled;
            OnPropertyChanged(nameof(SmartSaverStatus));
            OnPropertyChanged(nameof(SmartSaverAction));
        });
    }

    private void OnMetricsUpdated(object? sender, SystemMetrics m)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            UpdateProperties(m);
            UpdateBatteryHistory();
        });
    }

    private void UpdateProperties(SystemMetrics m)
    {
        BatteryPercentageText = $"{m.BatteryPercentage}%";
        RemainingTimeText = m.RemainingTimeText.Replace(" remaining", "");
        StatusText = m.StatusText;
        PowerSourceText = m.PowerSourceText;
        TimeUntilFullText = m.TimeUntilFullText;
        BatteryHealthText = $"{m.BatteryHealthPercent}%";
        HealthStatusText = m.HealthStatusText;
        WearLevelText = $"{m.WearLevelPercent}%";
        CycleCountText = m.CycleCount.ToString();
        ManufacturerText = !string.IsNullOrWhiteSpace(m.Manufacturer) ? m.Manufacturer : "LG";
        ChemistryText = !string.IsNullOrWhiteSpace(m.Chemistry) ? m.Chemistry : "Li-ion";
        SerialNumberText = !string.IsNullOrWhiteSpace(m.SerialNumber) ? m.SerialNumber : "Unavailable";
        VoltageText = $"{m.Voltage:F1} V";
        PowerDrawText = $"{m.PowerDrawWatts:F1} W";
        BatteryTempText = $"{m.BatteryTemperature:F0}°C";
        ChargeRateText = $"{m.ChargeRateWatts:F1} W";
        CapacityUsedText = $"{m.CapacityUsedWh:F1} Wh";
        DesignCapacityText = $"{m.DesignCapacityWh:F1} Wh";
        FullCapacityText = $"{m.FullCapacityWh:F1} Wh";
        AdapterText = m.AdapterText;
    }

    private void UpdateBatteryHistory()
    {
        if (_monitorService is SystemMonitorService realService)
        {
            var samples = realService.HistoryService.GetHistory(SelectedPeriod);

            if (samples == null || samples.Count < 2)
            {
                HasHistory = false;
                ChartPathData = "";
                ChartFillData = "";
                SetupTimeLabels(SelectedPeriod);
                return;
            }

            HasHistory = true;
            SetupTimeLabels(SelectedPeriod);

            double width = 1000.0;
            double height = 115.0;
            double padding = 10.0;
            double usableHeight = height - (padding * 2);

            DateTime minTime = samples.First().Timestamp;
            DateTime maxTime = samples.Last().Timestamp;
            double totalDuration = (maxTime - minTime).TotalSeconds;
            if (totalDuration <= 0) totalDuration = 1;

            var points = new List<Point>();
            foreach (var sample in samples)
            {
                double xRatio = (sample.Timestamp - minTime).TotalSeconds / totalDuration;
                double x = xRatio * width;
                
                double yRatio = (100.0 - sample.BatteryPercentage) / 100.0;
                double y = padding + (yRatio * usableHeight);

                points.Add(new Point(x, y));
            }

            string linePath = $"M {points[0].X:F1},{points[0].Y:F1}";
            for (int i = 1; i < points.Count; i++)
            {
                linePath += $" L {points[i].X:F1},{points[i].Y:F1}";
            }

            ChartPathData = linePath;
            ChartFillData = $"{linePath} L {width:F1},{height:F1} L 0,{height:F1} Z";
        }
        else
        {
            HasHistory = false;
            ChartPathData = "";
            ChartFillData = "";
            SetupTimeLabels(SelectedPeriod);
        }
    }

    private void SetupTimeLabels(string period)
    {
        var labels = new List<string>();
        if (period.Equals("Today", StringComparison.OrdinalIgnoreCase))
        {
            labels = new List<string> { "12am", "3am", "6am", "9am", "12pm", "3pm", "6pm", "9pm", "12am" };
        }
        else if (period.Equals("Week", StringComparison.OrdinalIgnoreCase))
        {
            DateTime now = DateTime.Now;
            for (int i = 6; i >= 0; i--)
            {
                labels.Add(now.AddDays(-i).ToString("ddd"));
            }
        }
        else
        {
            DateTime now = DateTime.Now;
            for (int i = 28; i >= 0; i -= 4)
            {
                labels.Add(now.AddDays(-i).ToString("MMM d"));
            }
        }
        HistoryTimeLabels = labels;
    }
}
