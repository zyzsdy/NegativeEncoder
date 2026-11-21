using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NegativeEncoder.Desktop.Models;
using NegativeEncoder.Desktop.Views;
using Xunit;

namespace NegativeEncoder.Desktop.Tests;

public sealed class WorkbenchTests
{
    [AvaloniaFact]
    public async Task TaskLogOpensAtTheBottomAndFollowsUntilScrolledUp()
    {
        var owner = new Window();
        owner.Show();
        try
        {
            var task = new TaskDraft(TaskOperation.Video, "input.mp4", "output.mp4", new Preset(), "", "", "");
            task.AppendLog(string.Concat(Enumerable.Repeat("existing line\n", 100)));
            var closed = PreviewDialog.TaskAsync(owner, task);
            var dialog = Assert.Single(owner.OwnedWindows);
            void UpdateLayout()
            {
                dialog.UpdateLayout();
                Dispatcher.UIThread.RunJobs();
            }

            UpdateLayout();
            var log = Assert.Single(dialog.GetVisualDescendants().OfType<TextBox>());
            var scroll = log.GetVisualAncestors().OfType<ScrollViewer>().First();
            double Bottom() => scroll.Extent.Height - scroll.Viewport.Height;
            Assert.True(Bottom() > 0);
            Assert.Equal(Bottom(), scroll.Offset.Y);

            var previousBottom = Bottom();
            task.AppendLog("another line\n");
            UpdateLayout();
            Assert.True(Bottom() > previousBottom);
            Assert.Equal(Bottom(), scroll.Offset.Y);

            scroll.Offset = Vector.Zero;
            task.AppendLog("line while reading history\n");
            UpdateLayout();
            Assert.Equal(0, scroll.Offset.Y);

            scroll.ScrollToEnd();
            UpdateLayout();
            task.AppendLog("latest line\n");
            UpdateLayout();
            Assert.Equal(Bottom(), scroll.Offset.Y);
            dialog.Close();
            await closed;
        }
        finally { owner.Close(); }
    }

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
            Assert.True(model.CreatePreset("测试预设", false));
            CheckSelection("测试预设");
            PresetItems()[0].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            CheckSelection("Default");
            model.CurrentPreset.VbrParam = "8765";
            void Switch(string choice)
            {
                PresetItems()[1].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                var dialog = Assert.IsType<PreviewDialog>(Assert.Single(window.OwnedWindows));
                CheckSelection("*Default");
                if (choice == "Escape")
                    dialog.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
                else
                    dialog.GetVisualDescendants().OfType<Button>().Single(b => Equals(b.Content, choice))
                        .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Dispatcher.UIThread.RunJobs();
            }
            Switch("Escape");
            CheckSelection("*Default");
            Switch("取消");
            CheckSelection("*Default");
            Assert.Equal("8765", model.CurrentPreset.VbrParam);
            Switch("保存");
            CheckSelection("测试预设");
            PresetItems()[0].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            CheckSelection("Default");
            Assert.Equal("8765", model.CurrentPreset.VbrParam);
            model.CurrentPreset.VbrParam = "9000";
            Switch("不保存");
            CheckSelection("测试预设");
            PresetItems()[0].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            CheckSelection("Default");
            Assert.Equal("8765", model.CurrentPreset.VbrParam);
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
    public void PictureControlsStayGroupedAcrossEncodersAndWindowSizes()
    {
        var window = new MainWindow(false);
        window.Show();
        try
        {
            var view = window.FindControl<FunctionTabs>("EditorTabs")!;
            var deinterlace = view.FindControl<CheckBox>("DeinterlaceCheckBox")!;
            var resize = view.FindControl<CheckBox>("ResizeCheckBox")!;
            var deinterlaceFields = view.FindControl<Grid>("DeinterlaceFields")!;
            var resizeFields = view.FindControl<Grid>("ResizeFields")!;
            foreach (var encoder in Enum.GetValues<Encoder>())
            {
                window.Model.CurrentPreset.Encoder = encoder;
                foreach (var width in new[] { 1000, 1320 })
                {
                    window.Width = width;
                    deinterlace.IsChecked = width == 1000;
                    resize.IsChecked = width == 1320;
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();
                    Assert.Equal(deinterlace.IsChecked, view.FindControl<ComboBox>("FieldOrderSelect")!.IsEffectivelyEnabled);
                    Assert.Equal(deinterlace.IsChecked, view.FindControl<ComboBox>("DeInterlaceMethodPresetSelect")!.IsEffectivelyEnabled);
                    Assert.Equal(resize.IsChecked, view.FindControl<TextBox>("OutputResWidthField")!.IsEffectivelyEnabled);
                    Assert.Equal(resize.IsChecked, view.FindControl<TextBox>("OUtputResHeightField")!.IsEffectivelyEnabled);
                    Assert.True(deinterlaceFields.Bounds.Bottom <= resizeFields.Bounds.Top);
                    foreach (var group in new[] { deinterlaceFields, resizeFields })
                    {
                        var toggle = group.Children[0].Bounds;
                        var parameters = (Grid)group.Children[1];
                        var first = parameters.Children[0].Bounds;
                        var second = parameters.Children[1].Bounds;
                        Assert.Equal(first.Top, second.Top);
                        Assert.True(second.Left >= first.Right);
                        if (width == 1000)
                        {
                            Assert.True(parameters.Bounds.Top >= toggle.Bottom);
                            Assert.Equal(12, parameters.Bounds.Left);
                        }
                        else
                        {
                            Assert.True(toggle.Top < parameters.Bounds.Bottom && parameters.Bounds.Top < toggle.Bottom);
                            Assert.InRange(parameters.Bounds.Left - Math.Max(deinterlace.Bounds.Width, resize.Bounds.Width), 11, 13);
                        }
                    }
                }
            }
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
            window.Model.CurrentPreset.Encoder = Encoder.QSV;
            var value = (TextBox)((Grid)fields.Children[0]).Children[1];
            value.Text = "7654";
            foreach (var width in new[] { 1320, 1000, 1600, 1000, 1320 })
            {
                window.Width = width;
                window.Height = width == 1000 ? window.MinHeight : 850;
                window.UpdateLayout();
                Dispatcher.UIThread.RunJobs();
                var visible = fields.Children.Where(child => child.IsVisible).ToArray();
                Assert.Equal(6, visible.Length);
                var columns = width == 1000 ? 2 : 3;
                for (var index = 0; index < visible.Length; index++)
                {
                    var bounds = visible[index].Bounds;
                    Assert.True(bounds.Width > 0);
                    Assert.True(bounds.Right <= fields.Bounds.Width + 1);
                    Assert.Equal(visible[index % columns].Bounds.Left, bounds.Left);
                    if (index % columns > 0)
                    {
                        var previous = visible[index - 1].Bounds;
                        Assert.Equal(previous.Top, bounds.Top);
                        Assert.True(previous.Right <= bounds.Left);
                    }
                    if (index >= columns)
                        Assert.True(visible[index - columns].Bounds.Bottom <= bounds.Top);
                }
                Assert.Equal("7654", value.Text);
                Assert.Equal("7654", window.Model.CurrentPreset.VbrParam);
            }
            Assert.Contains(AppVersion.Current, window.Title);
            Assert.NotNull(window.Icon);
        }
        finally { window.Close(); }
    }
}
