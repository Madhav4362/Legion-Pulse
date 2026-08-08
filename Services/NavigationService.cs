using CommunityToolkit.Mvvm.ComponentModel;
using LegionPulse.Models;
using LegionPulse.ViewModels;

namespace LegionPulse.Services;

public sealed partial class NavigationService : ObservableObject, INavigationService
{
    private readonly Dictionary<AppPage, Func<ViewModelBase>> _pageFactories = new();

    [ObservableProperty]
    private ViewModelBase? currentViewModel;

    [ObservableProperty]
    private AppPage currentPage = (AppPage)(-1);

    public void Register(AppPage page, Func<ViewModelBase> pageFactory)
    {
        _pageFactories[page] = pageFactory;
    }

    public void NavigateTo(AppPage page)
    {
        if (!_pageFactories.TryGetValue(page, out var pageFactory))
        {
            throw new InvalidOperationException($"No view model is registered for {page}.");
        }

        CurrentPage = page;
        CurrentViewModel = pageFactory();
    }
}
