using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;
using LLMUninstaller.Core;
using LLMUninstaller.Gui.Localization;

namespace LLMUninstaller.Gui;

public partial class AboutWindow : Window
{
    private readonly LocalizationService _loc;

    public AboutWindow(LocalizationService localization)
    {
        _loc = localization;
        InitializeComponent();
        ApplyLocalization();
        _loc.LanguageChanged += ApplyLocalization;
    }

    private void ApplyLocalization()
    {
        Title = _loc.Get("AboutTitle");
        VersionText.Text = Strings.Format(_loc.Current, "AboutVersion", AppInfo.Version);
        SourceLabel.Text = _loc.Get("AboutSourceCode") + ":";
        DonateLabel.Text = _loc.Get("AboutDonate") + ":";
        CryptoLabel.Text = _loc.Get("AboutDonateCrypto") + ":";
        CloseButton.Content = _loc.Get("AboutClose");
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    protected override void OnClosed(EventArgs e)
    {
        _loc.LanguageChanged -= ApplyLocalization;
        base.OnClosed(e);
    }
}
