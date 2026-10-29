using System.Windows;

namespace Aetherworks;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(args.Exception.Message, "Aetherworks error", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
    }
}