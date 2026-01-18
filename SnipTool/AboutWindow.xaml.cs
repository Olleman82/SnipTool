using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Navigation;

namespace SnipTool;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        CloseButton.Click += (_, _) => Close();
        SourceInitialized += (_, _) =>
        {
            if (System.Windows.Application.Current is App app)
            {
                WindowThemeHelper.Apply(this, app.IsDarkMode);
            }
        };
        SetVersionText();
    }

    private void SetVersionText()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
        VersionText.Text = $"Version {version}";
    }

    private void OnLinkNavigate(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch
        {
            // Ignore failures to open the browser.
        }
        e.Handled = true;
    }
}
