using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using NegativeEncoder.Desktop.Views;
using Xunit;

namespace NegativeEncoder.Desktop.Tests;

public sealed class WorkbenchTests
{
    [AvaloniaFact]
    public void PresetMenusTrackSelectionRenameAndDeletionWithoutLosingEdits()
    {
        var window = new MainWindow(false);
        window.Show();
        try
        {
            var model = window.Model;
            var menu = window.FindControl<MenuItem>("PresetMenu")!;
            MenuItem[] PresetItems() => menu.Items.OfType<MenuItem>().Where(item => item.ToggleType == MenuItemToggleType.Radio).ToArray();
            void CheckSelection(string name)
            {
                Assert.Equal(name, Assert.Single(PresetItems(), item => item.IsChecked).Header);
                var nativeMenu = NativeMenu.GetMenu(window)?.Items.OfType<NativeMenuItem>().Single(item => item.Header == "预设").Menu;
                if (OperatingSystem.IsMacOS()) Assert.NotNull(nativeMenu);
                if (nativeMenu is not null)
                    Assert.Equal(name, Assert.Single(nativeMenu.Items.OfType<NativeMenuItem>(), item => item.IsChecked).Header);
            }

            CheckSelection("Default");
            model.CurrentPreset.VbrParam = "8765";
            Assert.True(model.CreatePreset("测试预设", false));
            CheckSelection("测试预设");
            PresetItems()[0].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            CheckSelection("*Default");
            Assert.Equal("8765", model.CurrentPreset.VbrParam);
            Assert.True(model.IsPresetDirty);
            Assert.True(model.RenamePreset("重命名"));
            CheckSelection("*重命名");
            Assert.Contains("*重命名", window.Title);
            model.SavePreset();
            Assert.DoesNotContain("*", window.Title);
            model.DeletePreset();
            CheckSelection("测试预设");
            Assert.Single(PresetItems());
            model.DeletePreset();
            CheckSelection("Default");
            Assert.Single(PresetItems());
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void EncoderReflowsInPlaceAndKeepsValuesAcrossWindowSizes()
    {
        var window = new MainWindow(false) { Width = 1320, Height = 850 };
        window.Show();
        try
        {
            var view = window.FindControl<FunctionTabs>("EditorTabs")!;
            var fields = view.FindControl<ResponsiveFormPanel>("RateControlFields")!;
            var value = (TextBox)((Grid)fields.Children[0]).Children[1];
            value.Text = "7654";
            foreach (var width in new[] { 1320, 1000, 1600, 1000, 1320 })
            {
                window.Width = width;
                window.UpdateLayout();
                Dispatcher.UIThread.RunJobs();
                var first = fields.Children[0].Bounds;
                var second = fields.Children[1].Bounds;
                if (width == 1000)
                {
                    Assert.Equal(first.X, second.X);
                    Assert.True(second.Y >= first.Bottom);
                }
                else
                {
                    Assert.Equal(first.Y, second.Y);
                    Assert.True(second.X >= first.Right);
                }
                Assert.All(fields.Children, child => Assert.True(child.Bounds.Right <= fields.Bounds.Width + 1));
                Assert.Equal("7654", value.Text);
                Assert.Equal("7654", window.Model.CurrentPreset.VbrParam);
            }
            Assert.Contains("6.0.0-alpha.1", window.Title);
            Assert.NotNull(window.Icon);
        }
        finally { window.Close(); }
    }
}
