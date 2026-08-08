using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace LegionPulse.Services;

public class BatteryHistoryPoint
{
    public DateTime Timestamp { get; set; }
    public int BatteryPercentage { get; set; }
    public bool IsAcConnected { get; set; }
}

public interface IBatteryHistoryService
{
    void RecordSample(int percentage, bool isAcConnected);
    List<BatteryHistoryPoint> GetHistory(string period);
}

public class BatteryHistoryService : IBatteryHistoryService
{
    private readonly string _filePath;
    private readonly List<BatteryHistoryPoint> _samples = new();
    private readonly object _lock = new();
    private DateTime _lastRecordedTime = DateTime.MinValue;

    public BatteryHistoryService()
    {
        string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LegionPulse");
        Directory.CreateDirectory(folder);
        _filePath = Path.Combine(folder, "battery_history.json");
        LoadHistory();
    }

    private void LoadHistory()
    {
        lock (_lock)
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    string json = File.ReadAllText(_filePath);
                    var loaded = JsonSerializer.Deserialize<List<BatteryHistoryPoint>>(json);
                    if (loaded != null)
                    {
                        // Filter points older than 30 days
                        DateTime cutoff = DateTime.Now.AddDays(-30);
                        _samples.AddRange(loaded.Where(p => p.Timestamp >= cutoff));
                    }
                }
            }
            catch
            {
                // Soft fail if corrupt file
            }
        }
    }

    private void SaveHistory()
    {
        lock (_lock)
        {
            try
            {
                string json = JsonSerializer.Serialize(_samples, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_filePath, json);
            }
            catch { }
        }
    }

    public void RecordSample(int percentage, bool isAcConnected)
    {
        // Don't record faster than once per 30 seconds
        if ((DateTime.Now - _lastRecordedTime).TotalSeconds < 30) return;

        _lastRecordedTime = DateTime.Now;

        lock (_lock)
        {
            _samples.Add(new BatteryHistoryPoint
            {
                Timestamp = DateTime.Now,
                BatteryPercentage = Math.Clamp(percentage, 0, 100),
                IsAcConnected = isAcConnected
            });

            // Keep max 30 days
            DateTime cutoff = DateTime.Now.AddDays(-30);
            _samples.RemoveAll(p => p.Timestamp < cutoff);

            SaveHistory();
        }
    }

    public List<BatteryHistoryPoint> GetHistory(string period)
    {
        lock (_lock)
        {
            DateTime now = DateTime.Now;
            DateTime startTime = period.ToLowerInvariant() switch
            {
                "today" => now.Date,
                "week" => now.AddDays(-7),
                "month" => now.AddDays(-30),
                _ => now.Date
            };

            return _samples.Where(p => p.Timestamp >= startTime).OrderBy(p => p.Timestamp).ToList();
        }
    }
}
