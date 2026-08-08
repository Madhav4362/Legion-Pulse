using System.ComponentModel;
using LegionPulse.Models;
using LegionPulse.ViewModels;

namespace LegionPulse.Services;

public interface INavigationService : INotifyPropertyChanged
{
    ViewModelBase? CurrentViewModel { get; }

    AppPage CurrentPage { get; }

    void NavigateTo(AppPage page);
}
