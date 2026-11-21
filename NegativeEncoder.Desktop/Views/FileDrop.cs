using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;

namespace NegativeEncoder.Desktop.Views;

internal static class FileDrop
{
    public static void AcceptPath(TextBox field) =>
        AcceptFiles(field, paths => field.SetCurrentValue(TextBox.TextProperty, paths[0]));

    public static void AcceptFiles(Control target, Action<string[]> apply)
    {
        DragDrop.SetAllowDrop(target, true);
        target.AddHandler(DragDrop.DragOverEvent, (_, e) =>
        {
            e.DragEffects &= e.DataTransfer.Contains(DataFormat.File) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        });
        target.AddHandler(DragDrop.DropEvent, (_, e) =>
        {
            var paths = e.DataTransfer.TryGetFiles()?.OfType<IStorageFile>()
                .Select(file => file.TryGetLocalPath()).OfType<string>().ToArray() ?? [];
            e.DragEffects &= paths.Length > 0 ? DragDropEffects.Copy : DragDropEffects.None;
            if (e.DragEffects == DragDropEffects.Copy) apply(paths);
            e.Handled = true;
        });
    }
}
