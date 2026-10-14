using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NegativeEncoder.SystemOptions;
using Xunit;

namespace NegativeEncoder.Tests;

public class SystemOptionTests
{
    [Fact]
    public async Task ReadOption_WhenJsonIsInvalid_ReturnsDefaultAndBacksUp()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var configDir = Path.Combine(appDataPath, "NegativeEncoder", "Config");
        var configPath = Path.Combine(configDir, $"{nameof(BrokenConfigOption)}.json");

        Directory.CreateDirectory(configDir);

        foreach (var oldBackup in Directory.GetFiles(configDir, $"{nameof(BrokenConfigOption)}.json.bad.*"))
            File.Delete(oldBackup);

        await File.WriteAllTextAsync(configPath, "{invalid-json");

        var result = await SystemOption.ReadOption<BrokenConfigOption>();

        Assert.NotNull(result);
        Assert.Equal("default-name", result.Name);
        Assert.True(Directory.GetFiles(configDir, $"{nameof(BrokenConfigOption)}.json.bad.*").Any());

        File.Delete(configPath);
        foreach (var oldBackup in Directory.GetFiles(configDir, $"{nameof(BrokenConfigOption)}.json.bad.*"))
            File.Delete(oldBackup);
    }

    public class BrokenConfigOption
    {
        public string Name { get; set; } = "default-name";
    }
}
