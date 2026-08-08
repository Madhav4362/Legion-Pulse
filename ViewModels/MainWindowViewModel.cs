using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LegionPulse.Models;
using LegionPulse.Services;

namespace LegionPulse.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase
{
    private readonly INavigationService _navigationService;

    public MainWindowViewModel(INavigationService navigationService)
    {
        _navigationService = navigationService;
        _navigationService.PropertyChanged += OnNavigationChanged;

        NavigationItems = new ObservableCollection<NavigationItem>
        {
            new(AppPage.Dashboard, "Dashboard", "\uE80F"),
            new(AppPage.Battery, "Battery", "\uE850"),
            new(AppPage.Performance, "Performance", "\uE945"),
            new(AppPage.Settings, "Settings", "\uE713")
        };
    }

    public ObservableCollection<NavigationItem> NavigationItems { get; }

    [ObservableProperty]
    private ViewModelBase? currentViewModel;

    [RelayCommand]
    private void Navigate(NavigationItem? navigationItem)
    {
        if (navigationItem is not null)
        {
            _navigationService.NavigateTo(navigationItem.Page);
        }
    }

    private void OnNavigationChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(INavigationService.CurrentViewModel))
        {
            CurrentViewModel = _navigationService.CurrentViewModel;
        }

        if (e.PropertyName == nameof(INavigationService.CurrentPage))
        {
            foreach (var item in NavigationItems)
            {
                item.IsSelected = item.Page == _navigationService.CurrentPage;
            }
        }
    }
}
