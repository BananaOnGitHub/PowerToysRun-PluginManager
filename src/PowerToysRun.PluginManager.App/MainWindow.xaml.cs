using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PowerToysRun.PluginManager.App.ViewModels;
using PowerToysRun.PluginManager.Core;
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
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";
        await _viewModel.CheckForUpdateAsync(version);
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
        if (_viewModel.IsBusy || sender is not Button { Tag: PluginCardViewModel plugin })
        {
            return;
        }

        if (_viewModel.UnqueueInstall(plugin))
        {
            return;
        }

        try
        {
            await _viewModel.RunBusyAsync($"Downloading and validating {plugin.Name}...", async () =>
            {
                var releaseClient = new GitHubReleaseClient(_httpClient);
                var staging = new PackageStagingService(_httpClient, releaseClient, _paths);
                var package = await staging.StageLatestAsync(plugin.State.CatalogEntry);
                _viewModel.QueueInstall(plugin, package);
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

    private void Remove_Click(object sender, RoutedEventArgs eventArgs)
    {
        if (!_viewModel.IsBusy && sender is Button { Tag: PluginCardViewModel plugin })
        {
            _viewModel.ToggleRemove(plugin);
        }
    }

    private void ClearQueue_Click(object sender, RoutedEventArgs eventArgs) =>
        ClearQueueIfIdle();

    private void ClearQueueIfIdle()
    {
        if (!_viewModel.IsBusy)
        {
            _viewModel.ClearQueue();
        }
    }

    private async void ApplyQueue_Click(object sender, RoutedEventArgs eventArgs)
    {
        if (_viewModel.IsBusy || !_viewModel.HasQueuedChanges)
        {
            return;
        }

        var result = MessageBox.Show(
            $"Apply these plugin changes? PowerToys will close and restart once.\n\n{_viewModel.DescribeQueue()}",
            "Apply queued changes",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Information);
        if (result != MessageBoxResult.OK)
        {
            return;
        }

        try
        {
            var plan = _viewModel.CreateQueuePlan(FindPowerToysExecutable());
            var planPath = await new TransactionPlanStore(_paths).WriteAsync(plan);
            StartUpdater(planPath);
            Close();
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, "Could not apply queue", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void InstallManagerUpdate_Click(object sender, RoutedEventArgs eventArgs)
    {
        if (_viewModel.IsBusy)
        {
            return;
        }

        if (_viewModel.HasQueuedChanges)
        {
            MessageBox.Show(
                "Apply or clear the queued plugin changes before updating the manager.",
                "Plugin changes are queued",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        try
        {
            await _viewModel.RunBusyAsync("Downloading manager update...", async () =>
            {
                var installerPath = await _viewModel.DownloadUpdateAsync();
                var startInfo = new ProcessStartInfo(installerPath) { UseShellExecute = true };
                startInfo.ArgumentList.Add("/SP-");
                startInfo.ArgumentList.Add("/SILENT");
                startInfo.ArgumentList.Add("/CURRENTUSER");
                startInfo.ArgumentList.Add("/CLOSEAPPLICATIONS");
                startInfo.ArgumentList.Add("/NORESTARTAPPLICATIONS");
                Process.Start(startInfo);
                Close();
            });
        }
        catch (Exception exception)
        {
            _viewModel.StatusText = $"Could not start the manager update: {exception.Message}";
            MessageBox.Show(_viewModel.StatusText, "Update failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ViewManagerRelease_Click(object sender, RoutedEventArgs eventArgs)
    {
        var url = _viewModel.AvailableUpdate?.ReleaseUrl;
        if (Uri.TryCreate(url, UriKind.Absolute, out _))
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
    }

    private void DismissManagerUpdate_Click(object sender, RoutedEventArgs eventArgs) =>
        _viewModel.DismissUpdate();

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
