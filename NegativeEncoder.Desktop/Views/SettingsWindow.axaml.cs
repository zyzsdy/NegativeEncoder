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
    private bool _discardChanges;
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
        KeepTemporaryFilesOnFailureField.IsChecked = settings.KeepTemporaryFilesOnFailure;
        ProxyAddressField.Text = settings.ProxyAddress;
        if (OperatingSystem.IsWindows())
        {
            var available = ToolkitTerminal.FindPwsh() is not null;
            PwshOption.IsEnabled = available;
            PwshOption.Content = available ? Loc.Text("SettingsWindow.PwshDefault") : Loc.Text("SettingsWindow.PwshMissing");
            CmdOption.Content = available ? "cmd" : Loc.Text("SettingsWindow.CmdDefault");
            ShellField.SelectedItem = ShellField.Items.OfType<ComboBoxItem>()
                .Single(item => Equals(item.Tag, settings.EffectiveWindowsShell(available)));
        }
        Opened += (_, _) => LanguageField.Focus();
        Closing += OnSettingsClosing;
    }

    private void OnSettingsClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_discardChanges || e.CloseReason is not (WindowCloseReason.WindowClosing or WindowCloseReason.Undefined)) return;

        var language = (string)((ComboBoxItem)LanguageField.SelectedItem!).Tag!;
        var shell = _settings.WindowsShell;
        if (ShellField.SelectedItem is ComboBoxItem { Tag: string selected } &&
            selected != _settings.EffectiveWindowsShell(PwshOption.IsEnabled))
            shell = selected;

        var proxy = ProxyAddressField.Text ?? "";
        var keepTemporaryFiles = KeepTemporaryFilesOnFailureField.IsChecked == true;
        if (keepTemporaryFiles == _settings.KeepTemporaryFilesOnFailure &&
            language == _settings.Language && shell == _settings.WindowsShell &&
            ConcurrencyField.Text == _settings.MaxConcurrentEncodings.ToString() && proxy == _settings.ProxyAddress)
            return;

        e.Cancel = !TrySave(language, shell, proxy, keepTemporaryFiles);
    }

    private bool TrySave(string language, string? shell, string proxy, bool keepTemporaryFiles)
    {
        AutomationProperties.SetHelpText(ConcurrencyField, "");
        AutomationProperties.SetHelpText(ProxyAddressField, "");
        if (!int.TryParse(ConcurrencyField.Text, out var limit) || limit < 1)
        {
            ShowError(ConcurrencyField, Loc.Text("SettingsWindow.InvalidConcurrency"));
            return false;
        }
        try
        {
            _settings.Save(language, limit, proxy, shell, keepTemporaryFiles);
        }
        catch (ArgumentException ex) { ShowError(ProxyAddressField, ex.Message); return false; }
        catch (Exception ex)
        {
            SaveStatus.Text = Loc.Format("SettingsWindow.SaveFailed", ex.Message);
            SaveStatus.IsVisible = true;
            return false;
        }
        Saved?.Invoke();
        return true;
    }

    private void ShowError(TextBox field, string message)
    {
        SaveStatus.Text = message;
        SaveStatus.IsVisible = true;
        AutomationProperties.SetHelpText(field, message);
        field.Focus();
    }

    private async void Check_Click(object? sender, RoutedEventArgs e) => await _updates.CheckAsync(_lifetime);
    private void Save_Click(object? sender, RoutedEventArgs e) => Close();
    private void Cancel_Click(object? sender, RoutedEventArgs e) => Dismiss();

    protected override void Dismiss()
    {
        _discardChanges = true;
        Close();
    }
}
