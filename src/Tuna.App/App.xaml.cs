using System.Windows;
using Tuna.App.ViewModels;

namespace Tuna.App;

public partial class App : Application
{
    private void OnStartup(object sender, StartupEventArgs e)
    {
        var controller = BackendLocator.Resolve();
        if (controller is null)
        {
            MessageBox.Show(
                "未检测到受支持的机型(当前仅支持 Lenovo Legion WMI)。",
                "Tuna", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
            return;
        }

        var window = new MainWindow { DataContext = new MainViewModel(controller) };
        window.Show();
    }
}
