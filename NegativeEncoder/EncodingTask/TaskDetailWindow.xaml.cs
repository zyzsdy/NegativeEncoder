using System.Windows;
using System.Windows.Controls;

namespace NegativeEncoder.EncodingTask;

/// <summary>
///     TaskDetailWindow.xaml 的交互逻辑
/// </summary>
public partial class TaskDetailWindow : Window
{
    public TaskDetailWindow()
    {
        InitializeComponent();
    }

    private void logBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        logBox.ScrollToEnd();
    }

    private void mainButton_Click(object sender, RoutedEventArgs e)
    {
        var source = (EncodingTask)DataContext;
        if (source.IsFinished)
        {
            source.Destroy();
            Close();
        }
        else
        {
            var result1 = MessageBox.Show(this, "中止后当前的进度会丢失，确认吗？", "确认中止？", MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (result1 == MessageBoxResult.Yes)
            {
                var result2 = MessageBox.Show(this, "请再次确认：中止后已经编码的进度会丢失，确认中止吗？", "再次确认", MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                if (result2 == MessageBoxResult.Yes) source.Stop();
            }
        }
    }
}