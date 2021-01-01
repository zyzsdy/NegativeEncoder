using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace NegativeEncoder.About
{
    /// <summary>
    /// AboutWindow.xaml 的交互逻辑
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
            Utils.OpenBrowserViewLink.OpenUrl("https://github.com/zyzsdy/NegativeEncoder");
        }

        private void CloseAboutWindowButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
