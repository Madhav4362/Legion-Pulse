using System.Windows;
using LegionPulse.Models;
using LegionPulse.Services;
using LegionPulse.ViewModels;

namespace LegionPulse;

public partial class App : Application
{
    private SystemMonitorService? _monitorService;
    private SystemControlService? _controlService;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _monitorService = new SystemMonitorService();
        _monitorService.Start();

        _controlService = new SystemControlService();

        var navigationService = new NavigationService();
        navigationService.Register(AppPage.Dashboard, () => new DashboardViewModel(navigationService, _monitorService, _controlService));
        navigationService.Register(AppPage.Battery, () => new BatteryViewModel(_controlService, _monitorService));
        navigationService.Register(AppPage.Performance, () => new PerformanceViewModel(_controlService, _monitorService));
        navigationService.Register(AppPage.Settings, () => new SettingsViewModel(_controlService));

        var mainWindow = new MainWindow
        {
            DataContext = new MainWindowViewModel(navigationService)
        };

        mainWindow.Show();
        navigationService.NavigateTo(AppPage.Dashboard);
        SystemMonitorService.OptimizeProcessMemory();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _monitorService?.Stop();
        _monitorService?.Dispose();
        base.OnExit(e);
    }
}
