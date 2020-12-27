using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace NegativeEncoder
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public string BaseDir { get; set; }
        public MainWindow()
        {
            string[] pargs = Environment.GetCommandLineArgs();
            BaseDir = System.IO.Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);

            if (pargs.Length > 1)
            {
                if (pargs[1] != "--baseDir")
                {
                    MessageBox.Show("不支持的启动参数！", "初始化警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else
                {
                    if (pargs.Length > 2 && string.IsNullOrEmpty(pargs[2]))
                    {
                        BaseDir = pargs[2];
                    }
                    else
                    {
                        MessageBox.Show("--baseDir后指定的参数无效。", "初始化警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }

            //初始化（阶段1）
            var asm = Assembly.GetEntryAssembly();
            var asmVersion = (AssemblyInformationalVersionAttribute)Attribute.GetCustomAttribute(asm, typeof(AssemblyInformationalVersionAttribute));
            AppContext.Version = asmVersion?.InformationalVersion ?? "";

            AppContext.FileSelector = new FileSelector.FileSelector();

            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            //初始化（阶段2）
            Title = "消极压制 v" + AppContext.Version;
            StatusBar.DataContext = AppContext.Status;

            AppContext.Status.MainStatus = "载入系统配置...";
            AppContext.Config = SystemOptions.SystemOption.ReadOption<SystemOptions.Config>().GetAwaiter().GetResult();

            AutoCheckUpdateAfterStartupMenuItem.IsChecked = AppContext.Config.AutoCheckUpdate;

            AppContext.Status.MainStatus = "就绪";
        }

        private void ImportVideoMenuItem_Click(object sender, RoutedEventArgs e)
        {
            MainFileList.ImportVideoAction(sender, e);
        }

        private void ClearFilesMenuItem_Click(object sender, RoutedEventArgs e)
        {
            MainFileList.ClearFileList(sender, e);
        }

        private void ExitAppMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void AutoCheckUpdateAfterStartupMenuItem_Click(object sender, RoutedEventArgs e)
        {
            AppContext.Config.AutoCheckUpdate = AutoCheckUpdateAfterStartupMenuItem.IsChecked;
            SystemOptions.SystemOption.SaveOption(AppContext.Config).GetAwaiter().GetResult();
        }
    }
}
