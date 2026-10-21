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

    protected override void OnActivated(System.EventArgs e)
    {
        base.OnActivated(e);
        // An owned window can be restored while its owner is still minimized.
        // Restore through Windows so the owner's previous maximized state is preserved.
        if (Owner?.WindowState == WindowState.Minimized) SystemCommands.RestoreWindow(Owner);
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
