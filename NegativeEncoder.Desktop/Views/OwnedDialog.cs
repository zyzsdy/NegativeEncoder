using Avalonia.Controls;
using Avalonia.Input;

namespace NegativeEncoder.Desktop.Views;

public class OwnedDialog : Window
{
    public OwnedDialog()
    {
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        KeyDown += (_, e) => { if (e.Key == Key.Escape) { Dismiss(); e.Handled = true; } };
    }

    protected virtual void Dismiss() => Close();
}
