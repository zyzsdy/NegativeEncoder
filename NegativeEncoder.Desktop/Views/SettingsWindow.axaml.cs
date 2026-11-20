using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using NegativeEncoder.Desktop.ToolLibrary;
using NegativeEncoder.Desktop.ViewModels;

namespace NegativeEncoder.Desktop.Views;

public partial class SettingsWindow : OwnedDialog
{
    private readonly AppSettings _settings = null!;
    private readonly AppUpdateViewModel _updates = null!;
    private readonly CancellationToken _lifetime;
    public event Action? Saved;

    public SettingsWindow() => InitializeComponent();

    public SettingsWindow(AppSettings settings, AppUpdateViewModel updates, CancellationToken lifetime) : this()
    {
        _settings = settings;
        _updates = updates;
        _lifetime = lifetime;
        DataContext = updates;
        LanguageField.SelectedItem = LanguageField.Items.OfType<ComboBoxItem>().Single(item => Equals(item.Tag, settings.Language));
        ConcurrencyField.Text = settings.MaxConcurrentEncodings.ToString();
        ProxyAddressField.Text = settings.ProxyAddress;
        if (OperatingSystem.IsWindows())
        {
            var available = ToolkitTerminal.FindPwsh() is not null;
            PwshOption.IsEnabled = available;
            PwshOption.Content = available ? "pwsh（默认值）" : "pwsh（未检测到）";
            CmdOption.Content = available ? "cmd" : "cmd（默认值）";
            ShellField.SelectedItem = ShellField.Items.OfType<ComboBoxItem>()
                .Single(item => Equals(item.Tag, settings.EffectiveWindowsShell(available)));
        }
        Opened += (_, _) => LanguageField.Focus();
    }

    private void Save_Click(object? sender, RoutedEventArgs e)
    {
        AutomationProperties.SetHelpText(ConcurrencyField, "");
        AutomationProperties.SetHelpText(ProxyAddressField, "");
        if (!int.TryParse(ConcurrencyField.Text, out var limit) || limit < 1)
        {
            ShowError(ConcurrencyField, "并发数量必须是大于零的整数。");
            return;
        }
        try
        {
            var language = (string)((ComboBoxItem)LanguageField.SelectedItem!).Tag!;
            var shell = _settings.WindowsShell;
            if (ShellField.SelectedItem is ComboBoxItem { Tag: string selected } &&
                selected != _settings.EffectiveWindowsShell(PwshOption.IsEnabled))
                shell = selected;
            _settings.Save(language, limit, ProxyAddressField.Text ?? "", shell);
        }
        catch (ArgumentException ex) { ShowError(ProxyAddressField, ex.Message); return; }
        catch (Exception ex) { SaveStatus.Text = "设置未保存：" + ex.Message; return; }
        ProxyAddressField.Text = _settings.ProxyAddress;
        SaveStatus.Text = "设置已保存。";
        Saved?.Invoke();
    }

    private void ShowError(TextBox field, string message)
    {
        SaveStatus.Text = message;
        AutomationProperties.SetHelpText(field, message);
        field.Focus();
    }

    private async void Check_Click(object? sender, RoutedEventArgs e) => await _updates.CheckAsync(_lifetime);
    private void Close_Click(object? sender, RoutedEventArgs e) => Close();
}
