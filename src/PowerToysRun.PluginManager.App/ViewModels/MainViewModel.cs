using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using PowerToysRun.PluginManager.Core;
using PowerToysRun.PluginManager.Core.Models;
using PowerToysRun.PluginManager.Core.Services;

namespace PowerToysRun.PluginManager.App.ViewModels;

public enum CatalogView
{
    Discover,
    Installed,
    Updates,
}

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly AppPaths _paths;
    private readonly CatalogClient _catalogClient;
    private readonly InstalledPluginScanner _scanner = new();
    private List<PluginCardViewModel> _allPlugins = [];
    private string _searchText = string.Empty;
    private CatalogView _view;
    private string _statusText = "Loading catalog...";
    private bool _isBusy;

    public MainViewModel(AppPaths paths, HttpClient httpClient)
    {
        _paths = paths;
        _catalogClient = new CatalogClient(httpClient, paths);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<PluginCardViewModel> VisiblePlugins { get; } = [];

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetField(ref _searchText, value))
            {
                Refresh();
            }
        }
    }

    public string PageTitle => _view switch
    {
        CatalogView.Installed => "Installed plugins",
        CatalogView.Updates => "Plugin updates",
        _ => "Discover plugins",
    };

    public string StatusText
    {
        get => _statusText;
        set => SetField(ref _statusText, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetField(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(BusyVisibility));
            }
        }
    }

    public Visibility BusyVisibility => IsBusy ? Visibility.Visible : Visibility.Collapsed;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        IsBusy = true;
        try
        {
            var bundledCatalog = Path.Combine(AppContext.BaseDirectory, "catalog", "catalog.json");
            var catalogTask = _catalogClient.LoadAsync(bundledCatalog, cancellationToken);
            var installedTask = _scanner.ScanAsync(_paths.PluginDirectory, cancellationToken);
            await Task.WhenAll(catalogTask, installedTask);
            _allPlugins = PluginStateBuilder.Build(catalogTask.Result, installedTask.Result)
                .Select(state => new PluginCardViewModel(state))
                .OrderBy(plugin => plugin.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            Refresh();
        }
        catch (Exception exception)
        {
            StatusText = $"Could not load plugins: {exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void SetView(CatalogView view)
    {
        _view = view;
        OnPropertyChanged(nameof(PageTitle));
        Refresh();
    }

    public async Task RunBusyAsync(string status, Func<Task> action)
    {
        IsBusy = true;
        StatusText = status;
        try
        {
            await action();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Refresh()
    {
        var query = SearchText.Trim();
        var filtered = _allPlugins.Where(plugin =>
            (_view == CatalogView.Discover ||
             (_view == CatalogView.Installed && plugin.IsInstalled) ||
             (_view == CatalogView.Updates && plugin.UpdateAvailable)) &&
            (query.Length == 0 ||
             plugin.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
             plugin.Author.Contains(query, StringComparison.OrdinalIgnoreCase) ||
             plugin.Description.Contains(query, StringComparison.OrdinalIgnoreCase)));

        VisiblePlugins.Clear();
        foreach (var plugin in filtered)
        {
            VisiblePlugins.Add(plugin);
        }

        StatusText = $"{VisiblePlugins.Count} plugin{(VisiblePlugins.Count == 1 ? string.Empty : "s")}";
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed class PluginCardViewModel(PluginState state)
{
    public PluginState State { get; } = state;
    public string Name => State.CatalogEntry.Name;
    public string Description => State.CatalogEntry.Description;
    public string Author => State.CatalogEntry.Author;
    public string AuthorLine => string.IsNullOrWhiteSpace(Author) ? "Unknown author" : $"by {Author}";
    public string RepositoryUrl => State.CatalogEntry.RepositoryUrl;
    public string? IconUrl => State.CatalogEntry.IconUrl;
    public bool IsInstalled => State.IsInstalled;
    public bool UpdateAvailable => State.UpdateAvailable;
    public bool CanInstall => GitHubRepository.TryParse(RepositoryUrl, out _);
    public string PrimaryActionLabel => UpdateAvailable ? "Update" : IsInstalled ? "Reinstall" : "Install";
    public string StatusLabel => UpdateAvailable
        ? $"Update {State.CatalogEntry.LatestVersion}"
        : IsInstalled
            ? $"Installed {State.InstalledPlugin!.Manifest!.Version}"
            : State.CatalogEntry.LatestVersion is null
                ? string.Empty
                : $"Latest {State.CatalogEntry.LatestVersion}";
    public Visibility RemoveVisibility => IsInstalled ? Visibility.Visible : Visibility.Collapsed;
}
