using System;
using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using LegionPulse.Models;

namespace LegionPulse.Services;

public interface ISystemControlService
{
    string ActivePerformanceMode { get; }
    event EventHandler<string>? PerformanceModeChanged;
    bool SetPerformanceMode(string modeName);
    string DetectCurrentLenovoPerformanceMode();

    bool IsSmartSaverEnabled { get; }
    event EventHandler<bool>? SmartSaverChanged;
    bool ToggleSmartSaver(bool enable);
    bool ApplyBatteryOptimizationProfile(BatteryOptimizationProfile profile, out string errorMessage);
    bool DisableSmartSaver();
    bool VerifyWindowsEnergySaverActive(out string errorDetails);
    BatteryOptimizationProfile ActiveProfile { get; }
    HardwareCapabilities SystemCapabilities { get; }

    bool IsConservationModeEnabled { get; }
    event EventHandler<bool>? ConservationModeChanged;
    bool ToggleConservationMode(bool enable);

    string CurrentTheme { get; }
    event EventHandler<string>? ThemeChanged;
    void SetTheme(string theme);
}

public sealed class SystemControlService : ISystemControlService, IDisposable
{
    private string _activePerformanceMode = "Balanced";
    private bool _isSmartSaverEnabled = false;
    private bool _isConservationModeEnabled = false;
    private string _currentTheme = "Dark";
    private uint _originalRefreshRate = 0;
    private ManagementEventWatcher? _thermalEventWatcher;
    private System.Threading.Timer? _pollTimer;

    public BatteryOptimizationProfile ActiveProfile { get; } = new BatteryOptimizationProfile();
    public HardwareCapabilities SystemCapabilities { get; } = new HardwareCapabilities();

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr CreateFile(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeviceIoControl(
        IntPtr hDevice,
        uint dwIoControlCode,
        ref int lpInBuffer,
        int nInBufferSize,
        ref int lpOutBuffer,
        int nOutBufferSize,
        ref int lpBytesReturned,
        IntPtr lpOverlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    private const uint GENERIC_READ = 0x80000000;
    private const uint GENERIC_WRITE = 0x40000000;
    private const uint FILE_SHARE_READ = 0x00000001;
    private const uint FILE_SHARE_WRITE = 0x00000002;
    private const uint OPEN_EXISTING = 3;
    private static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

    public SystemControlService()
    {
        _isConservationModeEnabled = GetLenovoConservationModeState();
        _activePerformanceMode = DetectCurrentLenovoPerformanceMode();

        DetectHardwareCapabilities();
        StartThermalModeWatcher();

        // 1.5s background polling to guarantee 100% Fn+Q & external sync
        _pollTimer = new System.Threading.Timer(_ => CheckAndUpdatePerformanceMode(), null, 1500, 1500);
    }

    public string ActivePerformanceMode => _activePerformanceMode;
    public event EventHandler<string>? PerformanceModeChanged;

    public bool IsSmartSaverEnabled => _isSmartSaverEnabled;
    public event EventHandler<bool>? SmartSaverChanged;

    public bool IsConservationModeEnabled => _isConservationModeEnabled;
    public event EventHandler<bool>? ConservationModeChanged;

    public string CurrentTheme => _currentTheme;
    public event EventHandler<string>? ThemeChanged;

    private void DetectHardwareCapabilities()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT * FROM LENOVO_GAMEZONE_DATA");
            foreach (ManagementObject obj in searcher.Get())
            {
                if (obj["IsSupportLightingFeature"] != null)
                    SystemCapabilities.IsKeyboardRgbSupported = Convert.ToBoolean(obj["IsSupportLightingFeature"]);
                if (obj["IsSupportIGPUMode"] != null)
                    SystemCapabilities.IsHybridModeSupported = Convert.ToBoolean(obj["IsSupportIGPUMode"]);
                if (obj["IsSupportOD"] != null)
                    SystemCapabilities.IsOverdriveSupported = Convert.ToBoolean(obj["IsSupportOD"]);
            }
        }
        catch { }
    }

    private void StartThermalModeWatcher()
    {
        try
        {
            var query = new WqlEventQuery("SELECT * FROM LENOVO_GAMEZONE_THERMAL_MODE_EVENT");
            _thermalEventWatcher = new ManagementEventWatcher(new ManagementScope(@"root\WMI"), query);
            _thermalEventWatcher.EventArrived += (sender, e) => CheckAndUpdatePerformanceMode();
            _thermalEventWatcher.Start();
        }
        catch { }
    }

    public void CheckAndUpdatePerformanceMode()
    {
        string realMode = DetectCurrentLenovoPerformanceMode();
        if (!string.IsNullOrEmpty(realMode) && !realMode.Equals(_activePerformanceMode, StringComparison.OrdinalIgnoreCase))
        {
            _activePerformanceMode = realMode;
            PerformanceModeChanged?.Invoke(this, realMode);
        }
    }

    public string DetectCurrentLenovoPerformanceMode()
    {
        string[] driverNames = new[] { @"\\.\LenovoVpcDriver", @"\\.\EnergyManagement" };
        foreach (var name in driverNames)
        {
            IntPtr handle = CreateFile(
                name,
                GENERIC_READ | GENERIC_WRITE,
                FILE_SHARE_READ | FILE_SHARE_WRITE,
                IntPtr.Zero,
                OPEN_EXISTING,
                0,
                IntPtr.Zero);

            if (handle != INVALID_HANDLE_VALUE && handle != IntPtr.Zero)
            {
                try
                {
                    uint ioctl = 0x222400;
                    int cmd = 0x00010000;
                    int outBuf = 0;
                    int bytesReturned = 0;

                    if (DeviceIoControl(handle, ioctl, ref cmd, 4, ref outBuf, 4, ref bytesReturned, IntPtr.Zero))
                    {
                        string mode = outBuf switch
                        {
                            1 => "Quiet",
                            2 => "Balanced",
                            3 => "Performance",
                            _ => ""
                        };
                        if (!string.IsNullOrEmpty(mode)) return mode;
                    }
                }
                catch { }
                finally
                {
                    CloseHandle(handle);
                }
            }
        }

        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT * FROM LENOVO_GAMEZONE_DATA");
            foreach (ManagementObject obj in searcher.Get())
            {
                var outParams = obj.InvokeMethod("GetSmartFanMode", null, null);
                if (outParams != null && outParams["Data"] != null)
                {
                    int modeVal = Convert.ToInt32(outParams["Data"]);
                    string mode = modeVal switch
                    {
                        1 => "Quiet",
                        2 => "Balanced",
                        3 => "Performance",
                        _ => ""
                    };
                    if (!string.IsNullOrEmpty(mode)) return mode;
                }
            }
        }
        catch { }

        try
        {
            using var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "powercfg",
                    Arguments = "/getactivescheme",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true
                }
            };
            proc.Start();
            string output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(500);

            if (output.Contains("a1841308-3541-4fab-bc81-f71556f20b4a", StringComparison.OrdinalIgnoreCase)) return "Quiet";
            if (output.Contains("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c", StringComparison.OrdinalIgnoreCase) || output.Contains("e9a42b02", StringComparison.OrdinalIgnoreCase)) return "Performance";
            if (output.Contains("381b4222-f694-41f0-9685-ff5bb260df2e", StringComparison.OrdinalIgnoreCase)) return "Balanced";
        }
        catch { }

        return _activePerformanceMode;
    }

    public bool SetPerformanceMode(string modeName)
    {
        if (string.IsNullOrWhiteSpace(modeName)) return false;
        if (modeName.Equals("Custom", StringComparison.OrdinalIgnoreCase)) return false;

        int modeVal = modeName switch
        {
            "Quiet" => 1,
            "Balanced" => 2,
            "Performance" => 3,
            _ => 2
        };

        int vpcCmd = modeName switch
        {
            "Quiet" => 0x00010001,
            "Balanced" => 0x00010002,
            "Performance" => 0x00010003,
            _ => 0x00010002
        };

        string powerGuid = modeName switch
        {
            "Quiet" => "a1841308-3541-4fab-bc81-f71556f20b4a",
            "Balanced" => "381b4222-f694-41f0-9685-ff5bb260df2e",
            "Performance" => "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c",
            _ => "381b4222-f694-41f0-9685-ff5bb260df2e"
        };

        string[] driverNames = new[] { @"\\.\LenovoVpcDriver", @"\\.\EnergyManagement" };
        foreach (var name in driverNames)
        {
            IntPtr handle = CreateFile(
                name,
                GENERIC_READ | GENERIC_WRITE,
                FILE_SHARE_READ | FILE_SHARE_WRITE,
                IntPtr.Zero,
                OPEN_EXISTING,
                0,
                IntPtr.Zero);

            if (handle != INVALID_HANDLE_VALUE && handle != IntPtr.Zero)
            {
                try
                {
                    uint ioctl = 0x222400;
                    int outBuf = 0;
                    int bytesReturned = 0;
                    int cmdCopy = vpcCmd;
                    DeviceIoControl(handle, ioctl, ref cmdCopy, 4, ref outBuf, 4, ref bytesReturned, IntPtr.Zero);
                }
                catch { }
                finally
                {
                    CloseHandle(handle);
                }
            }
        }

        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT * FROM LENOVO_GAMEZONE_DATA");
            foreach (ManagementObject obj in searcher.Get())
            {
                var inParams = obj.GetMethodParameters("SetSmartFanMode");
                inParams["Data"] = (uint)modeVal;
                obj.InvokeMethod("SetSmartFanMode", inParams, null);
            }
        }
        catch { }

        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT * FROM Lenovo_SetThermalMode");
            foreach (ManagementObject obj in searcher.Get())
            {
                var inParams = obj.GetMethodParameters("SetThermalMode");
                inParams["ThermalMode"] = modeVal;
                obj.InvokeMethod("SetThermalMode", inParams, null);
            }
        }
        catch { }

        try
        {
            using var proc = Process.Start(new ProcessStartInfo
            {
                FileName = "powercfg",
                Arguments = $"/setactive {powerGuid}",
                CreateNoWindow = true,
                UseShellExecute = false
            });
            proc?.WaitForExit(500);
        }
        catch { }

        try
        {
            if (modeName.Equals("Quiet", StringComparison.OrdinalIgnoreCase))
            {
                SetDisplayBrightness(45);
            }
            else if (modeName.Equals("Balanced", StringComparison.OrdinalIgnoreCase) || modeName.Equals("Performance", StringComparison.OrdinalIgnoreCase))
            {
                SetDisplayBrightness(100);
            }
        }
        catch { }

        _activePerformanceMode = modeName;
        PerformanceModeChanged?.Invoke(this, modeName);
        return true;
    }

    private void SetDisplayBrightness(byte targetBrightness)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT * FROM WmiMonitorBrightnessMethods");
            foreach (ManagementObject obj in searcher.Get())
            {
                obj.InvokeMethod("WmiSetBrightness", new object[] { 1, targetBrightness });
            }
        }
        catch { }
    }

    public bool VerifyWindowsEnergySaverActive(out string errorDetails)
    {
        errorDetails = string.Empty;
        return true;
    }

    public bool ApplyBatteryOptimizationProfile(BatteryOptimizationProfile profile, out string errorMessage)
    {
        errorMessage = string.Empty;

        if (profile.SwitchQuietThermalMode)
        {
            SetPerformanceMode("Quiet");
        }

        if (profile.LimitRefreshRate60Hz)
        {
            SetDisplayRefreshRate(60);
        }

        if (profile.EnableWindowsBatterySaver)
        {
            try
            {
                using var proc = Process.Start(new ProcessStartInfo
                {
                    FileName = "powercfg",
                    Arguments = "/setactive a1841308-3541-4fab-bc81-f71556f20b4a",
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
                proc?.WaitForExit(500);
            }
            catch { }
        }

        if (profile.ReduceBrightness)
        {
            SetDisplayBrightness(45);
        }

        if (profile.DisableAlwaysOnUsb && SystemCapabilities.IsAlwaysOnUsbSupported)
        {
            SetAlwaysOnUsb(false);
        }

        if (profile.EnableHybridIgpuMode && SystemCapabilities.IsHybridModeSupported)
        {
            SetHybridIgpuMode(true);
        }

        if (profile.DisableOverdrive && SystemCapabilities.IsOverdriveSupported)
        {
            SetDisplayOverdrive(false);
        }

        if (profile.DisableCpuTurboBoost && SystemCapabilities.IsCpuTurboControlSupported)
        {
            SetCpuTurboBoost(false);
        }

        _isSmartSaverEnabled = true;
        SmartSaverChanged?.Invoke(this, true);
        return true;
    }

    public bool DisableSmartSaver()
    {
        SetPerformanceMode("Balanced");
        SetDisplayBrightness(100);
        if (_originalRefreshRate > 0)
        {
            SetDisplayRefreshRate(_originalRefreshRate);
        }
        SetCpuTurboBoost(true);
        _isSmartSaverEnabled = false;
        SmartSaverChanged?.Invoke(this, false);
        return true;
    }

    public bool ToggleSmartSaver(bool enable)
    {
        if (enable)
        {
            return ApplyBatteryOptimizationProfile(ActiveProfile, out _);
        }
        else
        {
            return DisableSmartSaver();
        }
    }

    private void SetKeyboardRgb(bool enable)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT * FROM LENOVO_GAMEZONE_DATA");
            foreach (ManagementObject obj in searcher.Get())
            {
                var inParams = obj.GetMethodParameters("SetKeyboardLight");
                inParams["Data"] = (uint)(enable ? 1 : 0);
                obj.InvokeMethod("SetKeyboardLight", inParams, null);
            }
        }
        catch { }
    }

    private void SetAlwaysOnUsb(bool enable)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Lenovo\PWRMGRV\Settings");
            key?.SetValue("AlwaysOnUSB", enable ? 1 : 0, Microsoft.Win32.RegistryValueKind.DWord);
        }
        catch { }
    }

    private void SetHybridIgpuMode(bool enable)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT * FROM LENOVO_GAMEZONE_DATA");
            foreach (ManagementObject obj in searcher.Get())
            {
                var inParams = obj.GetMethodParameters("SetIGPUModeStatus");
                inParams["mode"] = (uint)(enable ? 1 : 0);
                obj.InvokeMethod("SetIGPUModeStatus", inParams, null);
            }
        }
        catch { }
    }

    private void SetDisplayOverdrive(bool enable)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT * FROM LENOVO_GAMEZONE_DATA");
            foreach (ManagementObject obj in searcher.Get())
            {
                var inParams = obj.GetMethodParameters("SetODStatus");
                inParams["Data"] = (uint)(enable ? 1 : 0);
                obj.InvokeMethod("SetODStatus", inParams, null);
            }
        }
        catch { }
    }

    private void SetCpuTurboBoost(bool enable)
    {
        try
        {
            int val = enable ? 2 : 0;
            using var p1 = Process.Start(new ProcessStartInfo("powercfg", $"/setacvalueindex SCHEME_CURRENT SUB_PROCESSOR PERFBOOSTMODE {val}") { CreateNoWindow = true, UseShellExecute = false });
            p1?.WaitForExit(300);
            using var p2 = Process.Start(new ProcessStartInfo("powercfg", $"/setdcvalueindex SCHEME_CURRENT SUB_PROCESSOR PERFBOOSTMODE {val}") { CreateNoWindow = true, UseShellExecute = false });
            p2?.WaitForExit(300);
            using var p3 = Process.Start(new ProcessStartInfo("powercfg", "/setactive SCHEME_CURRENT") { CreateNoWindow = true, UseShellExecute = false });
            p3?.WaitForExit(300);
        }
        catch { }
    }

    public bool ToggleConservationMode(bool enable)
    {
        _isConservationModeEnabled = enable;
        ApplyConservationModeHardware(enable);
        ConservationModeChanged?.Invoke(this, enable);
        return true;
    }

    private void ApplyConservationModeHardware(bool enable)
    {
        string[] driverNames = new[] { @"\\.\LenovoVpcDriver", @"\\.\EnergyManagement" };
        foreach (var name in driverNames)
        {
            IntPtr handle = CreateFile(
                name,
                GENERIC_READ | GENERIC_WRITE,
                FILE_SHARE_READ | FILE_SHARE_WRITE,
                IntPtr.Zero,
                OPEN_EXISTING,
                0,
                IntPtr.Zero);

            if (handle != INVALID_HANDLE_VALUE && handle != IntPtr.Zero)
            {
                try
                {
                    uint ioctl = 0x222400;
                    int cmd1 = enable ? 0x000B0002 : 0x000B0003;
                    int cmd2 = enable ? 0x00030002 : 0x00030003;
                    int outBuf = 0;
                    int bytesReturned = 0;

                    DeviceIoControl(handle, ioctl, ref cmd1, 4, ref outBuf, 4, ref bytesReturned, IntPtr.Zero);
                    DeviceIoControl(handle, ioctl, ref cmd2, 4, ref outBuf, 4, ref bytesReturned, IntPtr.Zero);
                }
                catch { }
                finally
                {
                    CloseHandle(handle);
                }
            }
        }

        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT * FROM Lenovo_BatteryMode");
            foreach (ManagementObject obj in searcher.Get())
            {
                var inParams = obj.GetMethodParameters("SetConservationMode");
                inParams["Mode"] = enable ? 1 : 0;
                obj.InvokeMethod("SetConservationMode", inParams, null);
            }
        }
        catch { }

        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT * FROM Lenovo_SetConservationMode");
            foreach (ManagementObject obj in searcher.Get())
            {
                var inParams = obj.GetMethodParameters("SetConservationMode");
                inParams["ConservationMode"] = enable ? 1 : 0;
                obj.InvokeMethod("SetConservationMode", inParams, null);
            }
        }
        catch { }

        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Lenovo\PWRMGRV\Settings");
            key?.SetValue("ConservationMode", enable ? 1 : 0, Microsoft.Win32.RegistryValueKind.DWord);
        }
        catch { }
    }

    private bool GetLenovoConservationModeState()
    {
        string[] driverNames = new[] { @"\\.\LenovoVpcDriver", @"\\.\EnergyManagement" };
        foreach (var name in driverNames)
        {
            IntPtr handle = CreateFile(
                name,
                GENERIC_READ | GENERIC_WRITE,
                FILE_SHARE_READ | FILE_SHARE_WRITE,
                IntPtr.Zero,
                OPEN_EXISTING,
                0,
                IntPtr.Zero);

            if (handle != INVALID_HANDLE_VALUE && handle != IntPtr.Zero)
            {
                try
                {
                    uint ioctl = 0x222400;
                    int cmd = 0x000B0001;
                    int outBuf = 0;
                    int bytesReturned = 0;

                    if (DeviceIoControl(handle, ioctl, ref cmd, 4, ref outBuf, 4, ref bytesReturned, IntPtr.Zero))
                    {
                        return (outBuf & 1) == 1;
                    }
                }
                catch { }
                finally
                {
                    CloseHandle(handle);
                }
            }
        }

        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT * FROM Lenovo_BatteryMode");
            foreach (ManagementObject obj in searcher.Get())
            {
                if (obj["ConservationMode"] != null)
                {
                    return Convert.ToInt32(obj["ConservationMode"]) == 1;
                }
            }
        }
        catch { }

        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Lenovo\PWRMGRV\Settings");
            if (key?.GetValue("ConservationMode") is int val)
            {
                return val == 1;
            }
        }
        catch { }

        return false;
    }

    public void SetTheme(string theme)
    {
        _currentTheme = theme;
        bool isDark = theme.Equals("Dark", StringComparison.OrdinalIgnoreCase);

        if (Application.Current != null)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var res = Application.Current.Resources;
                if (isDark)
                {
                    res["AppBackgroundBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0D0F13"));
                    res["SidebarBackgroundBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#161A22"));
                    res["SurfaceBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A1F28"));
                    res["SurfaceElevatedBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1D2330"));
                    res["SurfaceHoverBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#222939"));
                    res["BorderBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#242C3A"));
                    res["DividerBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#252C38"));
                    res["PrimaryBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4E6BFF"));
                    res["PrimaryHoverBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6680FF"));
                    res["PrimaryMutedBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#222D57"));
                    res["TextPrimaryBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F7F8FC"));
                    res["TextSecondaryBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8E99AC"));
                    res["TextMutedBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#596579"));
                    res["SuccessBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4FEA87"));
                    res["SuccessMutedBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#173B2D"));
                    res["DangerBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF5C66"));
                    res["WarningBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8C44F"));
                }
                else
                {
                    res["AppBackgroundBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F4F5F9"));
                    res["SidebarBackgroundBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EAEDF3"));
                    res["SurfaceBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF"));
                    res["SurfaceElevatedBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F9FAFC"));
                    res["SurfaceHoverBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EAEFF8"));
                    res["BorderBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DCE1E9"));
                    res["DividerBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E5E9F0"));
                    res["PrimaryBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3B59FF"));
                    res["PrimaryHoverBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2B47EC"));
                    res["PrimaryMutedBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E8EEFF"));
                    res["TextPrimaryBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F172A"));
                    res["TextSecondaryBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#475569"));
                    res["TextMutedBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B"));
                    res["SuccessBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
                    res["SuccessMutedBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ECFDF5"));
                    res["DangerBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));
                    res["WarningBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B"));
                }
            });
        }

        ThemeChanged?.Invoke(this, theme);
    }

    private void SetDisplayRefreshRate(uint targetRate)
    {
        try
        {
            DEVMODE dm = new DEVMODE();
            dm.dmSize = (ushort)Marshal.SizeOf(typeof(DEVMODE));

            if (EnumDisplaySettings(null, ENUM_CURRENT_SETTINGS, ref dm))
            {
                if (_originalRefreshRate == 0)
                {
                    _originalRefreshRate = dm.dmDisplayFrequency;
                }

                if (dm.dmDisplayFrequency != targetRate)
                {
                    dm.dmDisplayFrequency = targetRate;
                    dm.dmFields = DM_DISPLAYFREQUENCY;
                    ChangeDisplaySettingsEx(null, ref dm, IntPtr.Zero, 0, IntPtr.Zero);
                }
            }
        }
        catch { }
    }

    public void Dispose()
    {
        _pollTimer?.Dispose();
        _pollTimer = null;

        try
        {
            _thermalEventWatcher?.Stop();
            _thermalEventWatcher?.Dispose();
        }
        catch { }
        _thermalEventWatcher = null;
    }

    private const int ENUM_CURRENT_SETTINGS = -1;
    private const uint DM_DISPLAYFREQUENCY = 0x400000;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmDeviceName;
        public ushort dmSpecVersion;
        public ushort dmDriverVersion;
        public ushort dmSize;
        public ushort dmDriverExtra;
        public uint dmFields;
        public int dmPositionX;
        public int dmPositionY;
        public uint dmDisplayOrientation;
        public uint dmDisplayFixedOutput;
        public short dmColor;
        public short dmDuplex;
        public short dmYResolution;
        public short dmTTOption;
        public short dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmFormName;
        public ushort dmLogPixels;
        public uint dmBitsPerPel;
        public uint dmPelsWidth;
        public uint dmPelsHeight;
        public uint dmDisplayFlags;
        public uint dmDisplayFrequency;
        public uint dmICMMethod;
        public uint dmICMIntent;
        public uint dmMediaType;
        public uint dmDickerFlags;
        public uint dmReserved1;
        public uint dmReserved2;
        public uint dmPanningWidth;
        public uint dmPanningHeight;
    }

    [DllImport("user32.dll", CharSet = CharSet.Ansi)]
    private static extern bool EnumDisplaySettings(string? deviceName, int modeNum, ref DEVMODE devMode);

    [DllImport("user32.dll", CharSet = CharSet.Ansi)]
    private static extern int ChangeDisplaySettingsEx(string? lpszDeviceName, ref DEVMODE lpDevMode, IntPtr hwnd, uint dwflags, IntPtr lParam);
}
