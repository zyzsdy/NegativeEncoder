using System.Linq;
using System.Windows;

namespace NegativeEncoder.Presets;

/// <summary>
///     PresetReName.xaml 的交互逻辑
/// </summary>
public partial class PresetReName : Window
{
    public PresetReName(string oldName)
    {
        InitializeComponent();

        NameBox.Text = oldName;
    }

    private void Button_Click(object sender, RoutedEventArgs e)
    {
        var newName = NameBox.Text;

        if (AppContext.PresetContext.PresetList.Count(p => p.PresetName == newName) > 0)
            if (MessageBox.Show(this, $"{newName} 已存在，是否覆盖？", "名称冲突", MessageBoxButton.YesNo,
                    MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                DialogResult = false;
                return;
            }

        DialogResult = true;
    }
}