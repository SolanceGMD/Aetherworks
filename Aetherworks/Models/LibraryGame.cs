using System.ComponentModel;
using System.Runtime.CompilerServices;
using Aetherworks.Models;

namespace Aetherworks.Models;

public sealed class LibraryGame : INotifyPropertyChanged
{
    private string _status = "Not installed";
    private double _progress;
    private bool _isInstalled;
    public required GameManifest Manifest { get; init; }
    public string Name => Manifest.Name;
    public string Description => Manifest.Description;
    public string Version => Manifest.Version;
    public bool SourceOnly => Manifest.SourceOnly;
    public string ReleaseLabel => SourceOnly ? "IN DEVELOPMENT  •  SOURCE CODE ONLY" : Version;
    public string Status { get => _status; set { _status = value; OnPropertyChanged(); } }
    public double Progress { get => _progress; set { _progress = value; OnPropertyChanged(); } }
    public bool IsInstalled { get => _isInstalled; set { _isInstalled = value; OnPropertyChanged(); } }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}
