using System.IO;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using Aetherworks.Models;
using Aetherworks.Services;

namespace Aetherworks;

public partial class MainWindow : Window
{
    private readonly CatalogService _catalog = new();
    private readonly GameInstallService _installer = new();
    private readonly ObservableCollection<LibraryGame> _games = [];
    private readonly LauncherSettingsService _settings = new();
    private IReadOnlyList<GameManifest> _manifest = [];
    private LibraryGame? _selectedGame;

    public MainWindow()
    {
        InitializeComponent();
        GamesList.ItemsSource = _games;
        Loaded += async (_, _) =>
        {
            var settings = _settings.Load();
            CatalogueSourceBox.Text = settings.CatalogueSource;
            await RefreshCatalogAsync();
            ShowPage("Home");
            Log("Aetherworks console ready. Wobble Works source depot: sudo fetch depot 2 = [install]");
        };
    }

    private async Task RefreshCatalogAsync()
    {
        try
        {
            FooterStatus.Text = "Loading catalogue…";
            var source = string.IsNullOrWhiteSpace(CatalogueSourceBox.Text)
                ? Path.Combine(AppContext.BaseDirectory, "Data", "games.json")
                : CatalogueSourceBox.Text.Trim();
            _manifest = await _catalog.LoadAsync(source);
            _games.Clear();
            foreach (var entry in _manifest)
            {
                var installed = _installer.IsInstalled(entry);
                _games.Add(new LibraryGame { Manifest = entry, IsInstalled = installed, Status = installed ? "In Library" : "Not downloaded" });
            }
            FooterStatus.Text = $"{_games.Count} games available";
        }
        catch (Exception ex) { FooterStatus.Text = "Catalogue error"; Log("Catalogue error: " + ex.Message); }
    }

    private void ShowPage(string page)
    {
        GameDetailsPage.Visibility = Visibility.Collapsed;
        StorePage.Visibility = page is "Store" or "Library" ? Visibility.Visible : Visibility.Collapsed;
        DownloadsPage.Visibility = page == "Downloads" ? Visibility.Visible : Visibility.Collapsed;
        ConsolePage.Visibility = page == "Console" ? Visibility.Visible : Visibility.Collapsed;
        SettingsPage.Visibility = page == "Settings" ? Visibility.Visible : Visibility.Collapsed;
        HomePage.Visibility = page == "Home" ? Visibility.Visible : Visibility.Collapsed;
        PageTitle.Text = page == "Store" ? "Discover games" : page;
        PageSubtitle.Text = page switch
        {
            "Store" => "Explore games available in your catalogue.",
            "Library" => "Your installed games and downloaded source projects.",
            "Downloads" => "Track downloads and depot operations.",
            "Console" => "A command line for managing your game depots.",
            "Settings" => "Configure your launcher.",
            _ => "Your games. Your library. All in one place."
        };
        if (page == "Library")
        {
            SearchBox.Text = "";
            GamesList.ItemsSource = _games.Where(g => g.IsInstalled).ToList();
        }
        else if (page == "Store")
        {
            SearchBox.Text = "";
            GamesList.ItemsSource = _games;
        }
    }

    private void Navigate_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string section }) ShowPage(section);
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshCatalogAsync();

    private void Search_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (GamesList is null || SearchBox is null || PageTitle.Text is not ("Library" or "Store")) return;
        IEnumerable<LibraryGame> source = PageTitle.Text == "Library" ? _games.Where(g => g.IsInstalled) : _games;
        var query = SearchBox.Text.Trim();
        GamesList.ItemsSource = string.IsNullOrEmpty(query) ? source.ToList() : source.Where(g =>
            g.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            g.Description.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private void Details_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: LibraryGame game }) return;
        OpenGameDetails(game);
    }

    private void OpenGameDetails(LibraryGame game)
    {
        _selectedGame = game;
        StorePage.Visibility = Visibility.Collapsed;
        DownloadsPage.Visibility = Visibility.Collapsed;
        ConsolePage.Visibility = Visibility.Collapsed;
        SettingsPage.Visibility = Visibility.Collapsed;
        HomePage.Visibility = Visibility.Collapsed;
        GameDetailsPage.Visibility = Visibility.Visible;
        PageTitle.Text = game.Name;
        PageSubtitle.Text = "Game details and library management";
        DetailName.Text = game.Name;
        DetailDescription.Text = game.Description;
        DetailReleaseLabel.Text = game.ReleaseLabel;
        DetailStatus.Text = game.IsInstalled ? "Installed in your Library" : "Not installed";
        DetailVersion.Text = $"Version: {game.Version}  •  Depot: {game.Manifest.Depot}";
        DetailInstallPath.Text = "Install folder: " + _installer.GetGamePath(game.Manifest);
        DetailRuntime.Text = game.SourceOnly
            ? "Runtime: Godot source project. Play requires Godot and a project.godot file in the downloaded files."
            : "Runtime: Installed Windows game";
        DetailPlayButton.Visibility = game.IsInstalled ? Visibility.Visible : Visibility.Collapsed;
        DetailInstallButton.Visibility = game.IsInstalled ? Visibility.Collapsed : Visibility.Visible;
        DetailInstallButton.Content = game.SourceOnly ? "Download Source" : "Install Game";
    }

    private void BackToLibrary_Click(object sender, RoutedEventArgs e) => ShowPage("Library");
    private async void DetailInstall_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedGame is not null) await TryInstallSelectedAsync();
    }
    private void DetailPlay_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedGame is not null) LaunchGame(_selectedGame);
    }
    private void DetailUninstall_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedGame is not null) RemoveGame(_selectedGame);
    }

    private async Task InstallGameAsync(LibraryGame game)
    {
        if (string.IsNullOrWhiteSpace(game.Manifest.DownloadUrl)) throw new InvalidOperationException("This depot has no download URL.");
        game.Status = game.SourceOnly ? "Downloading source…" : "Downloading…";
        game.Progress = 0;
        DownloadsList.Items.Add($"{game.Name}: download started");
        var progress = new Progress<double>(value => { game.Progress = value; game.Status = value >= 100 ? "In Library" : $"Downloading… {value:0}%"; });
        await _installer.InstallAsync(game.Manifest, progress, CancellationToken.None);
        game.Status = "In Library";
        game.IsInstalled = true;
        DownloadsList.Items.Add(game.SourceOnly
            ? $"{game.Name}: source downloaded to {_installer.GetGamePath(game.Manifest)}"
            : $"{game.Name}: installed successfully");
        FooterStatus.Text = game.SourceOnly ? $"{game.Name} source added to Library" : $"{game.Name} installed";
        if (PageTitle.Text == "Library") GamesList.ItemsSource = _games.Where(g => g.IsInstalled).ToList();
        if (_selectedGame == game) OpenGameDetails(game);
    }

    private async Task TryInstallSelectedAsync()
    {
        if (_selectedGame is null) return;
        try { await InstallGameAsync(_selectedGame); }
        catch (Exception ex) { _selectedGame.Status = "Download failed"; Log("Download failed: " + ex.Message); MessageBox.Show(ex.Message, "Download failed", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private async void Install_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: LibraryGame game }) return;
        try { await InstallGameAsync(game); }
        catch (Exception ex) { game.Status = "Download failed"; Log("Download failed: " + ex.Message); MessageBox.Show(ex.Message, "Download failed", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void Play_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: LibraryGame game }) LaunchGame(game);
    }

    private void LaunchGame(LibraryGame game)
    {
        if (!game.IsInstalled)
        {
            MessageBox.Show("Install this game before launching it.");
            return;
        }

        var installPath = _installer.GetGamePath(game.Manifest);
        if (game.SourceOnly)
        {
            var projectFile = FindGodotProject(installPath);
            if (projectFile is null)
            {
                MessageBox.Show(
                    $"{game.Name}'s downloaded depot currently contains data files but no Godot project.godot file. Godot cannot run it yet. The depot needs to include a complete Godot project before Play can launch it.",
                    "Source is not playable yet", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var godot = FindGodotExecutable();
            if (godot is null)
            {
                var dialog = new OpenFileDialog { Title = "Locate Godot", Filter = "Godot executable (Godot*.exe)|Godot*.exe|Executable files (*.exe)|*.exe" };
                if (dialog.ShowDialog(this) != true) return;
                godot = dialog.FileName;
            }

            try
            {
                var projectDirectory = Path.GetDirectoryName(projectFile)!;
                var start = new ProcessStartInfo(godot) { WorkingDirectory = projectDirectory, UseShellExecute = true };
                start.ArgumentList.Add("--path");
                start.ArgumentList.Add(projectDirectory);
                Process.Start(start);
                FooterStatus.Text = $"Launching {game.Name} with Godot";
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Unable to launch Godot project", MessageBoxButton.OK, MessageBoxImage.Error); }
            return;
        }

        var executable = Path.Combine(installPath, game.Manifest.Executable);
        if (!File.Exists(executable)) { game.Status = "Not installed"; MessageBox.Show("The game executable is missing. Reinstall this game."); return; }
        try { Process.Start(new ProcessStartInfo(executable) { WorkingDirectory = Path.GetDirectoryName(executable)!, UseShellExecute = true }); }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Unable to launch game", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private static string? FindGodotProject(string installPath)
    {
        if (!Directory.Exists(installPath)) return null;
        try { return Directory.GetFiles(installPath, "project.godot", SearchOption.AllDirectories).FirstOrDefault(); }
        catch (UnauthorizedAccessException) { return null; }
        catch (IOException) { return null; }
    }

    private static string? FindGodotExecutable()
    {
        var candidates = new List<string>();
        var pathEntries = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        candidates.AddRange(pathEntries);
        candidates.Add(@"C:\Program Files\Godot");
        candidates.Add(@"C:\Godot");
        var localPrograms = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Godot");
        candidates.Add(localPrograms);

        foreach (var directory in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                if (!Directory.Exists(directory)) continue;
                var exact = Path.Combine(directory, "godot.exe");
                if (File.Exists(exact)) return exact;
                var windows = Path.Combine(directory, "Godot.exe");
                if (File.Exists(windows)) return windows;
                var versioned = Directory.GetFiles(directory, "Godot*.exe").FirstOrDefault();
                if (versioned is not null) return versioned;
            }
            catch (UnauthorizedAccessException) { }
            catch (IOException) { }
        }
        return null;
    }

    private void Uninstall_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: LibraryGame game }) RemoveGame(game);
    }

    private void RemoveGame(LibraryGame game)
    {
        if (MessageBox.Show($"Remove {game.Name} and its downloaded files?", "Remove from library", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        try
        {
            _installer.Uninstall(game.Manifest);
            game.Status = "Not downloaded";
            game.IsInstalled = false;
            game.Progress = 0;
            DownloadsList.Items.Add($"{game.Name}: removed");
            if (_selectedGame == game) ShowPage("Library");
            else if (PageTitle.Text == "Library") GamesList.ItemsSource = _games.Where(g => g.IsInstalled).ToList();
        }
        catch (Exception ex) { Log("Remove failed: " + ex.Message); MessageBox.Show(ex.Message); }
    }

    private void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _settings.Save(new LauncherSettings { CatalogueSource = CatalogueSourceBox.Text.Trim() });
            FooterStatus.Text = "Settings saved";
            Log("Settings saved.");
            _ = RefreshCatalogAsync();
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Settings", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private async void ConsoleInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        var command = ConsoleInput.Text.Trim();
        ConsoleInput.Clear();
        Log("> " + command);
        try
        {
            var parsed = DepotCommand.Parse(command);
            var game = _games.FirstOrDefault(g => g.Manifest.Depot == parsed.Depot);
            if (game is null) throw new InvalidOperationException($"Depot {parsed.Depot} is not in the loaded catalogue.");
            if (parsed.Action == DepotAction.Install)
            {
                if (game.IsInstalled) { Log($"{game.Name} is already in your Library."); return; }
                await InstallGameAsync(game);
                Log($"Downloaded depot {parsed.Depot}: {game.Name}");
            }
            else
            {
                if (!game.IsInstalled && !_installer.IsInstalled(game.Manifest)) { Log($"{game.Name} is not in your Library; nothing to delete."); return; }
                if (MessageBox.Show($"Remove downloaded files for {game.Name}?", "Confirm depot deletion", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) { Log("Delete cancelled."); return; }
                _installer.Uninstall(game.Manifest);
                game.IsInstalled = false;
                game.Status = "Not downloaded";
                game.Progress = 0;
                if (PageTitle.Text == "Library") GamesList.ItemsSource = _games.Where(g => g.IsInstalled).ToList();
                if (_selectedGame == game) ShowPage("Library");
                Log($"Removed depot {parsed.Depot}: {game.Name}");
            }
        }
        catch (Exception ex) { Log("ERROR: " + ex.Message); }
    }

    private void Log(string message)
    {
        if (ConsoleOutput is null) return;
        ConsoleOutput.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        ConsoleOutput.ScrollToEnd();
        FooterStatus.Text = message;
    }
}
