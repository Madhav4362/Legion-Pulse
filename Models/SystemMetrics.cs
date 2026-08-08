namespace LegionPulse.Models;

public class SystemMetrics
{
    // Battery
    public int BatteryPercentage { get; set; } = 82;
    public string StatusText { get; set; } = "Charging";
    public bool IsAcConnected { get; set; } = true;
    public string PowerSourceText => IsAcConnected ? "AC Adapter Connected" : "On Battery";
    public string RemainingTimeText { get; set; } = "4h 37m remaining";
    public string TimeUntilFullText { get; set; } = "38 min";
    public int BatteryHealthPercent { get; set; } = 98;
    public string HealthStatusText => BatteryHealthPercent >= 90 ? "Excellent" : BatteryHealthPercent >= 75 ? "Good" : "Service Needed";
    public int WearLevelPercent { get; set; } = 2;
    public int CycleCount { get; set; } = 128;
    public string Manufacturer { get; set; } = "LG";
    public string Chemistry { get; set; } = "Li-ion";
    public double Voltage { get; set; } = 16.7;
    public double PowerDrawWatts { get; set; } = 15.0;
    public double ChargeRateWatts { get; set; } = 42.0;
    public double FullCapacityWh { get; set; } = 80.0;
    public double CurrentCapacityWh { get; set; } = 65.6;
    public double CapacityUsedWh { get; set; } = 14.4;
    public double BatteryTemperature { get; set; } = 31.0;
    public string AdapterText { get; set; } = "230W";
    public double DesignCapacityWh { get; set; } = 80.0;
    public string SerialNumber { get; set; } = "";

    // CPU
    public string CpuName { get; set; } = "Intel Core i9-14900HX";
    public double CpuUsagePercent { get; set; } = 23;
    public double CpuClockGhz { get; set; } = 3.80;
    public double CpuTemperature { get; set; } = 68;
    public int CpuFanRpm { get; set; } = 1800;
    public double CpuPowerWatts { get; set; } = 45;

    // GPU
    public string GpuName { get; set; } = "NVIDIA RTX 4070";
    public double GpuUsagePercent { get; set; } = 41;
    public double GpuTemperature { get; set; } = 72;
    public double GpuClockMhz { get; set; } = 1850;
    public double GpuVramUsedGb { get; set; } = 5.2;
    public double GpuVramTotalGb { get; set; } = 8.0;
    public double GpuPowerWatts { get; set; } = 80;
    public double TotalCpuGpuPowerWatts => Math.Round(CpuPowerWatts + GpuPowerWatts, 1);

    // System Memory
    public string SystemModelName { get; set; } = "Lenovo Legion";
    public double MemoryUsedGb { get; set; } = 8.4;
    public double MemoryTotalGb { get; set; } = 16.0;
    public double MemoryUsagePercent => MemoryTotalGb > 0 ? (MemoryUsedGb / MemoryTotalGb) * 100 : 0;
    public string ThermalStateText => CpuTemperature > 85 || GpuTemperature > 85 ? "High Temp" : "Normal";
}
