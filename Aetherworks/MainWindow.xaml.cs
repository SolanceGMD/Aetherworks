using System.IO;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Aetherworks.Models;
using Aetherworks.Services;

namespace Aetherworks;

public partial class MainWindow : Window
{
    private readonly CatalogService _catalog = new();
    private readonly GameInstallService _installer = new();
    private readonly ObservableCollection<LibraryGame> _games = [];
    private readonly LauncherSettingsService _settings = new();
    private readonly GoogleSignInService _google = new();
    private IReadOnlyList<GameManifest> _manifest = [];
    private string _signedInEmail = "";

    public MainWindow()
    {
        InitializeComponent();
        GamesList.ItemsSource = _games;
        Loaded += async (_, _) =>
        {
            var settings = _settings.Load();
            CatalogueSourceBox.Text = settings.CatalogueSource;
            GoogleClientIdBox.Text = settings.GoogleClientId;
            await RefreshCatalogAsync();
            ShowPage("Home");
            Log("Aetherworks console ready. Type: sudo fetch depot 1 = [install]");
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
                _games.Add(new LibraryGame { Manifest = entry, IsInstalled = _installer.IsInstalled(entry), Status = _installer.IsInstalled(entry) ? "Installed" : "Not installed" });
            FooterStatus.Text = $"{_games.Count} games available";
        }
        catch (Exception ex) { FooterStatus.Text = "Catalogue error"; Log("Catalogue error: " + ex.Message); }
    }

    private void ShowPage(string page)
    {
        StorePage.Visibility = page is "Store" or "Library" ? Visibility.Visible : Visibility.Collapsed;
        DownloadsPage.Visibility = page == "Downloads" ? Visibility.Visible : Visibility.Collapsed;
        ConsolePage.Visibility = page == "Console" ? Visibility.Visible : Visibility.Collapsed;
        SettingsPage.Visibility = page == "Settings" ? Visibility.Visible : Visibility.Collapsed;
        HomePage.Visibility = page == "Home" ? Visibility.Visible : Visibility.Collapsed;
        PageTitle.Text = page == "Store" ? "Discover games" : page;
        PageSubtitle.Text = page switch { "Store" => "Explore games available in your catalogue.", "Library" => "Your installed and ready-to-play games.", "Downloads" => "Track installs and depot operations.", "Console" => "A command line for managing your game depots.", "Settings" => "Configure your launcher and account.", _ => "Your games. Your library. All in one place." };
        if (page == "Library") GamesList.ItemsSource = _games.Where(g => g.IsInstalled).ToList();
        else if (page == "Store") GamesList.ItemsSource = _games;
    }

    private void Navigate_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string section }) ShowPage(section);
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshCatalogAsync();

    private void Search_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (GamesList is null || SearchBox is null) return;
        var source = PageTitle.Text == "Library" ? _games.Where(g => g.IsInstalled) : _games;
        var query = SearchBox.Text.Trim();
        GamesList.ItemsSource = string.IsNullOrEmpty(query) ? source.ToList() : source.Where(g => g.Name.Contains(query, StringComparison.OrdinalIgnoreCase) || g.Description.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private async Task InstallGameAsync(LibraryGame game)
    {
        if (string.IsNullOrWhiteSpace(game.Manifest.DownloadUrl)) throw new InvalidOperationException("This depot has no download URL.");
        game.Status = "Downloading…";
        game.Progress = 0;
        DownloadsList.Items.Add($"{game.Name}: download started");
        var progress = new Progress<double>(value => { game.Progress = value; game.Status = value >= 100 ? "Installed" : $"Downloading… {value:0}%"; });
        await _installer.InstallAsync(game.Manifest, progress, CancellationToken.None);
        game.Status = "Installed";
        game.IsInstalled = true;
        DownloadsList.Items.Add($"{game.Name}: installed successfully");
        FooterStatus.Text = $"{game.Name} installed";
    }

    private async void Install_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: LibraryGame game }) return;
        try { await InstallGameAsync(game); }
        catch (Exception ex) { game.Status = "Install failed"; Log("Install failed: " + ex.Message); MessageBox.Show(ex.Message, "Game installation failed", MessageBoxButton.OK, MessageBoxImage.Error); }
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
        if (MessageBox.Show($"Remove {game.Name} and its installed files?", "Uninstall game", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        try { _installer.Uninstall(game.Manifest); game.Status = "Not installed"; game.IsInstalled = false; game.Progress = 0; DownloadsList.Items.Add($"{game.Name}: removed"); }
        catch (Exception ex) { Log("Uninstall failed: " + ex.Message); MessageBox.Show(ex.Message); }
    }

    private void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _settings.Save(new LauncherSettings { CatalogueSource = CatalogueSourceBox.Text.Trim(), GoogleClientId = GoogleClientIdBox.Text.Trim() });
            FooterStatus.Text = "Settings saved";
            Log("Settings saved.");
            _ = RefreshCatalogAsync();
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Settings", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private async void SignIn_Click(object sender, RoutedEventArgs e)
    {
        var clientId = GoogleClientIdBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(clientId))
        {
            ShowPage("Settings");
            MessageBox.Show("Add your Google OAuth Desktop Client ID in Settings first. Create it in Google Cloud Console; Aetherworks does not collect Google passwords.", "Google sign-in setup");
            return;
        }
        try
        {
            SignInButton.IsEnabled = false;
            SignInButton.Content = "Waiting for Google…";
            _signedInEmail = await _google.SignInAsync(clientId);
            AccountLabel.Text = _signedInEmail;
            SignInButton.Content = "Signed in";
            Log("Google account authenticated: " + _signedInEmail);
        }
        catch (Exception ex) { Log("Google sign-in failed: " + ex.Message); MessageBox.Show(ex.Message, "Google sign-in", MessageBoxButton.OK, MessageBoxImage.Warning); }
        finally { SignInButton.IsEnabled = true; if (string.IsNullOrEmpty(_signedInEmail)) SignInButton.Content = "Sign in with Google"; }
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
                if (game.IsInstalled) { Log($"{game.Name} is already installed."); return; }
                await InstallGameAsync(game);
                Log($"Installed depot {parsed.Depot}: {game.Name}");
            }
            else
            {
                if (!game.IsInstalled && !_installer.IsInstalled(game.Manifest)) { Log($"{game.Name} is not installed; nothing to delete."); return; }
                if (MessageBox.Show($"Delete installed files for {game.Name}?", "Confirm depot deletion", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) { Log("Delete cancelled."); return; }
                _installer.Uninstall(game.Manifest);
                game.IsInstalled = false;
                game.Status = "Not installed";
                game.Progress = 0;
                Log($"Deleted depot {parsed.Depot}: {game.Name}");
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