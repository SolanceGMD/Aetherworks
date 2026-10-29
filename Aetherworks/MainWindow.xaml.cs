using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Aetherworks.Models;
using Aetherworks.Services;

namespace Aetherworks;

public partial class MainWindow : Window
{
    private readonly CatalogService _catalog = new();
    private readonly GameInstallService _installer = new();
    private readonly ObservableCollection<LibraryGame> _games = [];
    private CancellationTokenSource? _installCts;

    public MainWindow()
    {
        InitializeComponent();
        GamesList.ItemsSource = _games;
        Loaded += async (_, _) => await RefreshCatalogAsync();
    }

    private async Task RefreshCatalogAsync()
    {
        try
        {
            FooterStatus.Text = "Loading catalogue…";
            var path = Path.Combine(AppContext.BaseDirectory, "Data", "games.json");
            var entries = await _catalog.LoadAsync(path);
            _games.Clear();
            foreach (var entry in entries)
                _games.Add(new LibraryGame { Manifest = entry, IsInstalled = _installer.IsInstalled(entry), Status = _installer.IsInstalled(entry) ? "Installed" : "Not installed" });
            FooterStatus.Text = $"{_games.Count} games in catalogue";
        }
        catch (Exception ex) { FooterStatus.Text = "Catalogue error"; MessageBox.Show(ex.Message, "Aetherworks catalogue", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshCatalogAsync();
    private void Navigate_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string section }) PageTitle.Text = section;
    }
    private void Search_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (GamesList is null) return;
        var query = SearchBox.Text.Trim();
        GamesList.ItemsSource = string.IsNullOrEmpty(query) ? _games : _games.Where(g => g.Name.Contains(query, StringComparison.OrdinalIgnoreCase) || g.Description.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private async void Install_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: LibraryGame game }) return;
        if (string.IsNullOrWhiteSpace(game.Manifest.DownloadUrl)) return;
        _installCts?.Cancel();
        _installCts = new CancellationTokenSource();
        try
        {
            game.Status = "Downloading and installing…";
            game.Progress = 0;
            var progress = new Progress<double>(value => game.Progress = value);
            await _installer.InstallAsync(game.Manifest, progress, _installCts.Token);
            game.Status = "Installed";
            game.IsInstalled = true;
            FooterStatus.Text = $"{game.Name} installed";
        }
        catch (OperationCanceledException) { game.Status = "Install cancelled"; }
        catch (Exception ex) { game.Status = "Install failed"; MessageBox.Show(ex.Message, "Game installation failed", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void Play_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: LibraryGame game }) return;
        var executable = Path.Combine(_installer.RootPath, game.Manifest.InstallFolder, game.Manifest.Executable);
        if (!File.Exists(executable)) { game.Status = "Not installed"; MessageBox.Show("Install this game before launching it."); return; }
        try { Process.Start(new ProcessStartInfo(executable) { WorkingDirectory = Path.GetDirectoryName(executable), UseShellExecute = true }); }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Unable to launch game", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void Uninstall_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: LibraryGame game }) return;
        if (MessageBox.Show($"Remove {game.Name} and its installed files? Your other games will not be affected.", "Uninstall game", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        try { _installer.Uninstall(game.Manifest); game.Status = "Not installed"; game.IsInstalled = false; game.Progress = 0; }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Uninstall failed", MessageBoxButton.OK, MessageBoxImage.Error); }
    }
}