using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
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
    private readonly ManagerUpdateService _updateService;
    private readonly InstalledPluginScanner _scanner = new();
    private readonly PluginChangeQueue _queue = new();
    private List<PluginCardViewModel> _allPlugins = [];
    private string _searchText = string.Empty;
    private CatalogView _view;
    private string _statusText = "Loading catalog...";
    private bool _isBusy;
    private ManagerUpdate? _availableUpdate;
    private PluginCardViewModel? _selectedPlugin;

    public MainViewModel(AppPaths paths, HttpClient httpClient)
    {
        _paths = paths;
        _catalogClient = new CatalogClient(httpClient, paths);
        _updateService = new ManagerUpdateService(
            httpClient,
            new GitHubReleaseClient(httpClient),
            paths);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<PluginCardViewModel> VisiblePlugins { get; } = [];

    public PluginCardViewModel? SelectedPlugin => _selectedPlugin;

    public Visibility CatalogVisibility =>
        _selectedPlugin is null ? Visibility.Visible : Visibility.Collapsed;

    public Visibility DetailVisibility =>
        _selectedPlugin is null ? Visibility.Collapsed : Visibility.Visible;

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

    public ManagerUpdate? AvailableUpdate => _availableUpdate;

    public string UpdateMessage => _availableUpdate is null
        ? string.Empty
        : $"Plugin Manager {_availableUpdate.Version} is available.";

    public Visibility UpdateVisibility =>
        _availableUpdate is null ? Visibility.Collapsed : Visibility.Visible;

    public string QueueSummary => _queue.Count == 1
        ? "1 change queued"
        : $"{_queue.Count} changes queued";

    public bool HasQueuedChanges => _queue.Count > 0;

    public string ApplyQueueLabel => _queue.Count == 1
        ? "Apply change"
        : $"Apply {_queue.Count} changes";

    public Visibility QueueVisibility =>
        _queue.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

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

    public async Task CheckForUpdateAsync(
        string currentVersion,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _availableUpdate = await _updateService.CheckAsync(currentVersion, cancellationToken);
            OnPropertyChanged(nameof(AvailableUpdate));
            OnPropertyChanged(nameof(UpdateMessage));
            OnPropertyChanged(nameof(UpdateVisibility));
        }
        catch (Exception exception) when (
            exception is HttpRequestException or IOException or InvalidOperationException)
        {
            // Update checks must not block normal catalog use, including offline use.
        }
    }

    public Task<string> DownloadUpdateAsync(CancellationToken cancellationToken = default) =>
        _availableUpdate is null
            ? throw new InvalidOperationException("No manager update is available.")
            : _updateService.DownloadAsync(_availableUpdate, cancellationToken);

    public void DismissUpdate()
    {
        _availableUpdate = null;
        OnPropertyChanged(nameof(AvailableUpdate));
        OnPropertyChanged(nameof(UpdateMessage));
        OnPropertyChanged(nameof(UpdateVisibility));
    }

    public void SetView(CatalogView view)
    {
        CloseDetails();
        _view = view;
        OnPropertyChanged(nameof(PageTitle));
        Refresh();
    }

    public void ShowDetails(PluginCardViewModel plugin)
    {
        _selectedPlugin = plugin;
        OnPropertyChanged(nameof(SelectedPlugin));
        OnPropertyChanged(nameof(CatalogVisibility));
        OnPropertyChanged(nameof(DetailVisibility));
        StatusText = plugin.StatusLabel.Length > 0 ? plugin.StatusLabel : plugin.AuthorLine;
    }

    public void CloseDetails()
    {
        if (_selectedPlugin is null)
        {
            return;
        }

        _selectedPlugin = null;
        OnPropertyChanged(nameof(SelectedPlugin));
        OnPropertyChanged(nameof(CatalogVisibility));
        OnPropertyChanged(nameof(DetailVisibility));
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

    public bool UnqueueInstall(PluginCardViewModel plugin)
    {
        if (plugin.QueuedKind is not (PluginTransactionKind.Install or PluginTransactionKind.Update))
        {
            return false;
        }

        RemoveQueuedChange(plugin);
        return true;
    }

    public void QueueInstall(PluginCardViewModel plugin, StagedPluginPackage package)
    {
        RemoveQueuedChange(plugin, notify: false);
        var targetDirectory = plugin.State.InstalledPlugin?.DirectoryPath ??
            Path.Combine(_paths.PluginDirectory, SafeName(package.Manifest.Name));
        var kind = plugin.IsInstalled ? PluginTransactionKind.Update : PluginTransactionKind.Install;
        _queue.Set(new PendingPluginChange
        {
            Kind = kind,
            PluginId = package.Manifest.Id,
            PluginName = package.Manifest.Name,
            SourceDirectory = package.PluginDirectory,
            TargetDirectory = targetDirectory,
        });
        plugin.SetQueued(kind, targetDirectory);
        NotifyQueueChanged();
        StatusText = $"{plugin.Name} is staged. Add more plugins or apply the queue.";
    }

    public void ToggleRemove(PluginCardViewModel plugin)
    {
        if (plugin.State.InstalledPlugin is null)
        {
            return;
        }

        if (plugin.QueuedKind == PluginTransactionKind.Uninstall)
        {
            RemoveQueuedChange(plugin);
            return;
        }

        RemoveQueuedChange(plugin, notify: false);
        var targetDirectory = plugin.State.InstalledPlugin.DirectoryPath;
        _queue.Set(new PendingPluginChange
        {
            Kind = PluginTransactionKind.Uninstall,
            PluginId = plugin.State.CatalogEntry.Id,
            PluginName = plugin.Name,
            TargetDirectory = targetDirectory,
        });
        plugin.SetQueued(PluginTransactionKind.Uninstall, targetDirectory);
        NotifyQueueChanged();
        StatusText = $"Removal of {plugin.Name} is queued.";
    }

    public TransactionPlan CreateQueuePlan(string? powerToysExecutablePath) =>
        _queue.CreatePlan(_paths, powerToysExecutablePath);

    public string DescribeQueue()
    {
        var descriptions = _queue.Changes.Select(change =>
            $"• {change.Kind switch
            {
                PluginTransactionKind.Install => "Install",
                PluginTransactionKind.Update => "Update",
                _ => "Remove",
            }} {change.PluginName}");
        return string.Join(Environment.NewLine, descriptions);
    }

    public void ClearQueue()
    {
        _queue.Clear();
        foreach (var plugin in _allPlugins)
        {
            plugin.SetQueued(null, null);
        }

        NotifyQueueChanged();
        StatusText = "Queue cleared.";
    }

    private void RemoveQueuedChange(PluginCardViewModel plugin, bool notify = true)
    {
        if (plugin.QueuedTargetDirectory is not null)
        {
            _queue.Remove(plugin.QueuedTargetDirectory);
        }

        plugin.SetQueued(null, null);
        if (notify)
        {
            NotifyQueueChanged();
            StatusText = $"{plugin.Name} was removed from the queue.";
        }
    }

    private void NotifyQueueChanged()
    {
        OnPropertyChanged(nameof(HasQueuedChanges));
        OnPropertyChanged(nameof(QueueSummary));
        OnPropertyChanged(nameof(ApplyQueueLabel));
        OnPropertyChanged(nameof(QueueVisibility));
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
             plugin.Description.Contains(query, StringComparison.OrdinalIgnoreCase) ||
             plugin.TagsLine.Contains(query, StringComparison.OrdinalIgnoreCase)));

        VisiblePlugins.Clear();
        foreach (var plugin in filtered)
        {
            VisiblePlugins.Add(plugin);
        }

        StatusText = $"{VisiblePlugins.Count} plugin{(VisiblePlugins.Count == 1 ? string.Empty : "s")}";
    }

    private static string SafeName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var result = new string(value.Select(character => invalid.Contains(character) ? '_' : character).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(result) ? "Plugin" : result;
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

public sealed class PluginCardViewModel(PluginState state) : INotifyPropertyChanged
{
    private PluginTransactionKind? _queuedKind;
    private string? _queuedTargetDirectory;

    public event PropertyChangedEventHandler? PropertyChanged;

    public PluginState State { get; } = state;
    public string Name => State.CatalogEntry.Name;
    public string Description => State.CatalogEntry.Description;
    public string Author => State.CatalogEntry.Author;
    public string AuthorLine => string.IsNullOrWhiteSpace(Author) ? "Unknown author" : $"by {Author}";
    public string RepositoryUrl => State.CatalogEntry.RepositoryUrl;
    public string? DisplayIconUrl => ResolveInstalledIconPath() ?? State.CatalogEntry.IconUrl;
    public bool HasDisplayIcon => !string.IsNullOrWhiteSpace(DisplayIconUrl);
    public Visibility FallbackIconVisibility => HasDisplayIcon ? Visibility.Collapsed : Visibility.Visible;
    public Visibility ImageIconVisibility => HasDisplayIcon ? Visibility.Visible : Visibility.Collapsed;
    public string DetailsDescription => string.IsNullOrWhiteSpace(State.CatalogEntry.LongDescription)
        ? Description
        : State.CatalogEntry.LongDescription;
    public IReadOnlyList<string> ScreenshotUrls => State.CatalogEntry.ScreenshotUrls;
    public string TagsLine => string.Join("  •  ", State.CatalogEntry.Tags);
    public bool IsInstalled => State.IsInstalled;
    public bool UpdateAvailable => State.UpdateAvailable;
    public bool CanInstall => GitHubRepository.TryParse(RepositoryUrl, out _);
    public bool CanUsePrimaryAction => CanInstall || IsInstallQueued;
    public PluginTransactionKind? QueuedKind => _queuedKind;
    public string? QueuedTargetDirectory => _queuedTargetDirectory;
    public bool IsInstallQueued => _queuedKind is PluginTransactionKind.Install or PluginTransactionKind.Update;
    public bool IsRemoveQueued => _queuedKind == PluginTransactionKind.Uninstall;
    public string PrimaryActionLabel => IsInstallQueued
        ? "Undo"
        : UpdateAvailable
            ? "Update"
            : IsInstalled
                ? "Reinstall"
                : "Install";
    public string RemoveActionLabel => IsRemoveQueued ? "Keep" : "Remove";
    public string StatusLabel => _queuedKind switch
    {
        PluginTransactionKind.Install => "Install queued",
        PluginTransactionKind.Update => "Update queued",
        PluginTransactionKind.Uninstall => "Removal queued",
        _ when UpdateAvailable => $"Update {State.CatalogEntry.LatestVersion}",
        _ when IsInstalled => $"Installed {State.InstalledPlugin!.Manifest!.Version}",
        _ when State.CatalogEntry.LatestVersion is not null => $"Latest {State.CatalogEntry.LatestVersion}",
        _ => string.Empty,
    };
    public Visibility StatusVisibility =>
        StatusLabel.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
    public Visibility ScreenshotsVisibility =>
        ScreenshotUrls.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    public Visibility TagsVisibility =>
        State.CatalogEntry.Tags.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    public Visibility RemoveVisibility => IsInstalled ? Visibility.Visible : Visibility.Collapsed;

    public void SetQueued(PluginTransactionKind? kind, string? targetDirectory)
    {
        _queuedKind = kind;
        _queuedTargetDirectory = targetDirectory;
        OnPropertyChanged(nameof(QueuedKind));
        OnPropertyChanged(nameof(QueuedTargetDirectory));
        OnPropertyChanged(nameof(IsInstallQueued));
        OnPropertyChanged(nameof(IsRemoveQueued));
        OnPropertyChanged(nameof(CanUsePrimaryAction));
        OnPropertyChanged(nameof(PrimaryActionLabel));
        OnPropertyChanged(nameof(RemoveActionLabel));
        OnPropertyChanged(nameof(StatusLabel));
        OnPropertyChanged(nameof(StatusVisibility));
    }

    private string? ResolveInstalledIconPath()
    {
        var installed = State.InstalledPlugin;
        if (installed?.Manifest is null)
        {
            return null;
        }

        foreach (var relativePath in new[]
        {
            installed.Manifest.IconPathDark,
            installed.Manifest.IconPathLight,
        })
        {
            if (!InstalledPluginScanner.IsSafeRelativePath(relativePath))
            {
                continue;
            }

            var fullPath = Path.GetFullPath(Path.Combine(installed.DirectoryPath, relativePath!));
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        return null;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
