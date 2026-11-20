using System.Text.Json;
using NegativeEncoder.Desktop.ToolLibrary;

namespace NegativeEncoder.Desktop.Models;

internal sealed record SavedPreset(Guid Id, JsonElement Settings);
internal sealed record PresetDrafts(Guid Selected, Dictionary<Guid, JsonElement> Changes);

internal sealed class PresetStore(string directory)
{
    private readonly string _catalog = Path.Combine(directory, "presets.json");
    private readonly string _drafts = Path.Combine(directory, "preset-draft.json");

    public SavedPreset[]? LoadCatalog()
    {
        var presets = ReadIfPresent<SavedPreset[]>(_catalog);
        if (presets is not null && (presets.Length == 0 ||
            presets.Any(p => p is null || p.Id == Guid.Empty || p.Settings.ValueKind != JsonValueKind.Object) ||
            presets.DistinctBy(p => p.Id).Count() != presets.Length))
            throw new InvalidDataException(Loc.Format("PresetStore.InvalidList", _catalog));
        return presets;
    }

    public PresetDrafts? LoadDrafts()
    {
        var drafts = ReadIfPresent<PresetDrafts>(_drafts);
        if (drafts is not null && (drafts.Selected == Guid.Empty || drafts.Changes is null ||
            drafts.Changes.Values.Any(snapshot => snapshot.ValueKind != JsonValueKind.Object)))
            throw new InvalidDataException(Loc.Format("PresetStore.InvalidDraft", _drafts));
        return drafts;
    }

    public void WriteCatalog(IEnumerable<SavedPreset> presets) => LibraryJson.WriteAtomic(_catalog, presets);
    public void WriteDrafts(PresetDrafts drafts) => LibraryJson.WriteAtomic(_drafts, drafts);

    private static T? ReadIfPresent<T>(string path) where T : class
    {
        try { return LibraryJson.Read<T>(path); }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
        catch (JsonException error) { throw new InvalidDataException(Loc.Format("PresetStore.ReadFailed", path), error); }
    }
}
