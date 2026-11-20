using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using NegativeEncoder.Desktop.Models;
using NegativeEncoder.Desktop.ToolLibrary;
using NegativeEncoder.Desktop.Views;
using Xunit;
using Xunit.Sdk;

[assembly: NegativeEncoder.Desktop.Tests.ChineseTestCulture]

namespace NegativeEncoder.Desktop.Tests;

// Existing UI and diagnostic assertions describe the Simplified Chinese interface.
public sealed class ChineseTestCultureAttribute : BeforeAfterTestAttribute
{
    public override void Before(MethodInfo methodUnderTest) =>
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("zh-CN");
}

[CollectionDefinition("Application language", DisableParallelization = true)]
public sealed class ApplicationLanguageCollection;

[Collection("Application language")]
public sealed class LocalizationTests
{
    [Fact]
    public void AllEmbeddedPacksHaveTheSameIdsAndFormatArguments()
    {
        var source = ReadPack("zh-CN");
        foreach (var language in new[] { "en-US", "ja-JP" })
        {
            var translated = ReadPack(language);
            Assert.Equal(source.Keys.Order(), translated.Keys.Order());
            foreach (var (id, template) in source)
            {
                var value = translated[id];
                Assert.False(string.IsNullOrEmpty(value), $"{language}: {id}");
                Assert.Equal(CompositeFormat.Parse(template).MinimumArgumentCount, CompositeFormat.Parse(value).MinimumArgumentCount);
                Assert.Equal(Placeholders(template), Placeholders(value));
            }
        }

        static string[] Placeholders(string value) =>
            Regex.Matches(value, @"\{\d+(?::[^}]+)?\}").Select(m => m.Value).Distinct().Order().ToArray();

        static Dictionary<string, string> ReadPack(string language)
        {
            using var stream = typeof(Loc).Assembly.GetManifestResourceStream($"NegativeEncoder.Desktop.Localization.{language}.json");
            Assert.NotNull(stream);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)!;
        }
    }

    [Theory]
    [InlineData("en-GB", "Ready")]
    [InlineData("ja", "準備完了")]
    [InlineData("zh-TW", "就绪")]
    [InlineData("fr-FR", "就绪")]
    public void SystemLanguageMatchesLanguageFamily(string systemLanguage, string ready)
    {
        var original = CultureInfo.CurrentUICulture;
        var originalDefault = CultureInfo.DefaultThreadCurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(systemLanguage);
            new AppSettings(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json")).ApplyUiCulture();
            Assert.Equal(ready, Loc.Text("MainWindow.Ready"));
        }
        finally
        {
            CultureInfo.CurrentUICulture = original;
            CultureInfo.DefaultThreadCurrentUICulture = originalDefault;
        }
    }

    [AvaloniaTheory]
    [InlineData("zh-CN", "编码器", "视频输出路径", "设置", "视频编码", "等待执行")]
    [InlineData("en-US", "Encoder", "Video output path", "Settings", "Video encoding", "Waiting")]
    [InlineData("ja-JP", "エンコーダー", "動画の出力先", "設定", "動画エンコード", "待機中")]
    public void SavedLanguageLoadsBeforeWindowOptionsAndQueue(string language, string encoder, string outputPath,
        string settingsTitle, string operation, string waiting)
    {
        var original = CultureInfo.CurrentUICulture;
        var originalDefault = CultureInfo.DefaultThreadCurrentUICulture;
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var path = Path.Combine(root.FullName, "network.json");
            new AppSettings(path).Save(language, 3, "");
            using var client = new DistributionClient(settings: new AppSettings(path));
            var window = new MainWindow(false, distributionClient: client) { Width = 1000, Height = 620 };
            window.Show();
            try
            {
                Assert.Equal(outputPath, AutomationProperties.GetName(window.FindControl<TextBox>("OutputPath")!));
                var editor = window.FindControl<FunctionTabs>("EditorTabs")!;
                var tabs = editor.FindControl<TabControl>("Tabs")!;
                Assert.Equal(encoder, ((TabItem)tabs.Items[0]!).Header);
                Assert.Equal(settingsTitle, new SettingsWindow().Title);
                var queueButton = editor.GetVisualDescendants().OfType<Button>().Single(b => Equals(b.Tag, TaskOperation.Video));
                window.Model.AddFiles([Path.Combine(root.FullName, "素材.mp4")]);
                Assert.True(window.Model.AddTask(Assert.IsType<TaskOperation>(queueButton.Tag)));
                var task = Assert.Single(window.Model.Tasks);
                Assert.Equal(operation, task.OperationLabel);
                Assert.Equal(waiting, task.Status);
                Assert.Contains(task.Name, task.Details);
                Assert.Contains(operation, task.Details);
                foreach (var type in Enum.GetValues<TaskOperation>())
                    Assert.False(string.IsNullOrWhiteSpace(Loc.Text($"Operation.{type}")));
            }
            finally { window.Close(); }
        }
        finally
        {
            CultureInfo.CurrentUICulture = original;
            CultureInfo.DefaultThreadCurrentUICulture = originalDefault;
            root.Delete(true);
        }
    }
}
