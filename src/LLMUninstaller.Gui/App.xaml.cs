using System.Windows;
using LLMUninstaller.Gui.Localization;

namespace LLMUninstaller.Gui;

public partial class App : Application
{
    public static LocalizationService Localization { get; } = new();

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        Localization.Load();
        var mainWindow = new MainWindow();
        mainWindow.Show();
    }
}
