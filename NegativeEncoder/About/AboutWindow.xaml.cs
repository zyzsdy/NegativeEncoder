using System.Windows;
using NegativeEncoder.Utils;

namespace NegativeEncoder.About;

/// <summary>
///     AboutWindow.xaml 的交互逻辑
/// </summary>
public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();

        AboutVersionBlock.Text = $"Version v{AppContext.Version.CurrentVersion}";
    }

    private void OpenWebsiteButton_Click(object sender, RoutedEventArgs e)
    {
        OpenBrowserViewLink.OpenUrl("https://github.com/zyzsdy/NegativeEncoder");
    }

    private void CloseAboutWindowButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}