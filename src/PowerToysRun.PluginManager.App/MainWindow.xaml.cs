using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PowerToysRun.PluginManager.App.ViewModels;
using PowerToysRun.PluginManager.Core;
using PowerToysRun.PluginManager.Core.Models;
using PowerToysRun.PluginManager.Core.Services;

namespace PowerToysRun.PluginManager.App;

public partial class MainWindow : Window
{
    private readonly AppPaths _paths = new();
    private readonly HttpClient _httpClient = new();
    private readonly MainViewModel _viewModel;
    private ScrollViewer? _pluginScrollViewer;

    public MainWindow()
    {
        _viewModel = new MainViewModel(_paths, _httpClient);
        InitializeComponent();
        DataContext = _viewModel;
    }

    protected override void OnClosed(EventArgs eventArgs)
    {
        _httpClient.Dispose();
        base.OnClosed(eventArgs);
    }

    private async void Window_Loaded(object sender, RoutedEventArgs eventArgs)
    {
        ApplyArguments(Environment.GetCommandLineArgs().Skip(1).ToArray());
        await _viewModel.LoadAsync();
    }

    private void Navigation_Checked(object sender, RoutedEventArgs eventArgs)
    {
        if (sender is RadioButton { Tag: string tag } &&
            Enum.TryParse<CatalogView>(tag, true, out var view))
        {
            _viewModel.SetView(view);
        }
    }

    private void PluginList_PreviewMouseWheel(object sender, MouseWheelEventArgs eventArgs)
    {
        _pluginScrollViewer ??= FindVisualChild<ScrollViewer>(PluginList);
        if (_pluginScrollViewer is null || eventArgs.Delta == 0)
        {
            return;
        }

        const double pixelsPerNotch = 52;
        var notches = eventArgs.Delta / 120d;
        var targetOffset = Math.Clamp(
            _pluginScrollViewer.VerticalOffset - (notches * pixelsPerNotch),
            0,
            _pluginScrollViewer.ScrollableHeight);
        _pluginScrollViewer.ScrollToVerticalOffset(targetOffset);
        eventArgs.Handled = true;
    }

    private static T? FindVisualChild<T>(DependencyObject parent)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match)
            {
                return match;
            }

            var nested = FindVisualChild<T>(child);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }

    private async void PrimaryAction_Click(object sender, RoutedEventArgs eventArgs)
    {
        if (sender is not Button { Tag: PluginCardViewModel plugin })
        {
            return;
        }

        var result = MessageBox.Show(
            $"Download and validate {plugin.Name}, then close and restart PowerToys to apply it?",
            "Apply plugin change",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Information);
        if (result != MessageBoxResult.OK)
        {
            return;
        }

        try
        {
            await _viewModel.RunBusyAsync($"Staging {plugin.Name}...", async () =>
            {
                var releaseClient = new GitHubReleaseClient(_httpClient);
                var staging = new PackageStagingService(_httpClient, releaseClient, _paths);
                var package = await staging.StageLatestAsync(plugin.State.CatalogEntry);
                var transaction = BuildInstallTransaction(plugin, package);
                var planPath = await new TransactionPlanStore(_paths).WriteAsync(transaction);
                StartUpdater(planPath);
                Close();
            });
        }
        catch (Exception exception)
        {
            _viewModel.StatusText = $"Could not stage {plugin.Name}: {exception.Message}";
            MessageBox.Show(_viewModel.StatusText, "Plugin install failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Repository_Click(object sender, RoutedEventArgs eventArgs)
    {
        if (sender is Button { Tag: string url } && Uri.TryCreate(url, UriKind.Absolute, out _))
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
    }

    private async void Remove_Click(object sender, RoutedEventArgs eventArgs)
    {
        if (sender is not Button { Tag: PluginCardViewModel plugin } ||
            plugin.State.InstalledPlugin is null)
        {
            return;
        }

        var result = MessageBox.Show(
            $"Remove {plugin.Name}? A backup will be retained by the plugin manager.",
            "Remove plugin",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning);
        if (result != MessageBoxResult.OK)
        {
            return;
        }

        var transactionId = Guid.NewGuid();
        var plan = new TransactionPlan
        {
            Id = transactionId,
            PowerToysExecutablePath = FindPowerToysExecutable(),
            Operations =
            [
                new PluginTransactionOperation
                {
                    Kind = PluginTransactionKind.Uninstall,
                    PluginId = plugin.State.CatalogEntry.Id,
                    PluginName = plugin.Name,
                    TargetDirectory = plugin.State.InstalledPlugin.DirectoryPath,
                    BackupDirectory = Path.Combine(_paths.BackupDirectory, transactionId.ToString("N"), SafeName(plugin.Name)),
                },
            ],
        };

        try
        {
            var planPath = await new TransactionPlanStore(_paths).WriteAsync(plan);
            StartUpdater(planPath);
            Close();
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, "Could not prepare removal", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private TransactionPlan BuildInstallTransaction(PluginCardViewModel plugin, StagedPluginPackage package)
    {
        var transactionId = Guid.NewGuid();
        var targetDirectory = plugin.State.InstalledPlugin?.DirectoryPath ??
            Path.Combine(_paths.PluginDirectory, SafeName(package.Manifest.Name));
        return new TransactionPlan
        {
            Id = transactionId,
            PowerToysExecutablePath = FindPowerToysExecutable(),
            Operations =
            [
                new PluginTransactionOperation
                {
                    Kind = plugin.IsInstalled ? PluginTransactionKind.Update : PluginTransactionKind.Install,
                    PluginId = package.Manifest.Id,
                    PluginName = package.Manifest.Name,
                    SourceDirectory = package.PluginDirectory,
                    TargetDirectory = targetDirectory,
                    BackupDirectory = Path.Combine(
                        _paths.BackupDirectory,
                        transactionId.ToString("N"),
                        SafeName(package.Manifest.Name)),
                },
            ],
        };
    }

    private static string SafeName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var result = new string(value.Select(character => invalid.Contains(character) ? '_' : character).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(result) ? "Plugin" : result;
    }

    private void StartUpdater(string planPath)
    {
        var updaterPath = Path.Combine(AppContext.BaseDirectory, "PowerToysRun.PluginManager.Updater.exe");
        if (!File.Exists(updaterPath))
        {
            throw new FileNotFoundException("The transaction updater is missing from the app installation.", updaterPath);
        }

        var startInfo = new ProcessStartInfo(updaterPath) { UseShellExecute = true };
        startInfo.ArgumentList.Add(planPath);
        Process.Start(startInfo);
    }

    private static string? FindPowerToysExecutable()
    {
        try
        {
            using var process = Process.GetProcessesByName("PowerToys").FirstOrDefault();
            var path = process?.MainModule?.FileName;
            if (File.Exists(path))
            {
                return path;
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
        }

        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PowerToys", "PowerToys.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PowerToys", "PowerToys.exe"),
        };
        return candidates.FirstOrDefault(File.Exists);
    }

    private void ApplyArguments(string[] arguments)
    {
        for (var index = 0; index < arguments.Length; index++)
        {
            if (arguments[index] == "--search" && index + 1 < arguments.Length)
            {
                _viewModel.SearchText = arguments[++index];
            }
            else if (arguments[index] == "--view" && index + 1 < arguments.Length &&
                     Enum.TryParse<CatalogView>(arguments[++index], true, out var view))
            {
                _viewModel.SetView(view);
                DiscoverNavigation.IsChecked = view == CatalogView.Discover;
                InstalledNavigation.IsChecked = view == CatalogView.Installed;
                UpdatesNavigation.IsChecked = view == CatalogView.Updates;
            }
        }
    }
}
