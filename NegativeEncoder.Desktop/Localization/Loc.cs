using System.Globalization;
using System.Text.Json;
using Avalonia.Markup.Xaml;

namespace NegativeEncoder.Desktop;

public static class Loc
{
    private static readonly Dictionary<string, Dictionary<string, string>> Packs = new()
    {
        ["zh"] = Load("zh-CN"),
        ["en"] = Load("en-US"),
        ["ja"] = Load("ja-JP")
    };

    private static Dictionary<string, string> Load(string language)
    {
        var resource = $"NegativeEncoder.Desktop.Localization.{language}.json";
        using var stream = typeof(Loc).Assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidDataException($"Missing language pack: {resource}");
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)
            ?? throw new InvalidDataException($"Empty language pack: {resource}");
    }

    private static CultureInfo EffectiveCulture
    {
        get
        {
            var culture = CultureInfo.CurrentUICulture;
            return Packs.ContainsKey(culture.TwoLetterISOLanguageName) ? culture : CultureInfo.GetCultureInfo("zh-CN");
        }
    }

    public static string Text(string id) => Packs[EffectiveCulture.TwoLetterISOLanguageName][id];

    public static string Format(string id, params object?[] arguments) =>
        string.Format(EffectiveCulture, Text(id), arguments);
}

public sealed class TrExtension(string id) : MarkupExtension
{
    public override object ProvideValue(IServiceProvider serviceProvider) => Loc.Text(id);
}
