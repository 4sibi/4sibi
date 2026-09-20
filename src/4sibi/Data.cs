using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Sibi;

public record MacroPreset(int Aps, string Action, int ActionKey, int Trigger, int Emergency, bool Hold, bool Randomize, bool RobloxOnly, bool PauseOnFocusLoss)
{
    public static MacroPreset From(Settings s) => new(s.Aps, s.Action, s.ActionKey, s.Trigger, s.Emergency, s.Hold, s.Randomize, s.RobloxOnly, s.PauseOnFocusLoss);
    public Settings Apply(Settings s) => s with { Aps = Aps, Action = Action, ActionKey = ActionKey, Trigger = Trigger, Emergency = Emergency, Hold = Hold, Randomize = Randomize, RobloxOnly = RobloxOnly, PauseOnFocusLoss = PauseOnFocusLoss };
    public void Validate()
    {
        var s = Apply(new());
        if (Aps < 1 || Aps > 1000 || !new[] { "left", "right", "key" }.Contains(Action) || !Bindings.Valid(Trigger) || !Bindings.Valid(Emergency) || ActionKey <= 6 || !Bindings.Valid(ActionKey) || Bindings.Conflict(s))
            throw new InvalidDataException("Invalid macro settings / Ungültige Makro-Einstellungen.");
    }
}
public record SavedProfile(string Id, string Name, MacroPreset Macro, DateTime UpdatedUtc, string? ImportSourceId = null);

public sealed class ProfileStore
{
    public string Root { get; }
    public string PathName => Path.Combine(Root, "profiles.json");
    public List<SavedProfile> Items { get; private set; } = new();
    public string Error { get; private set; } = "";
    public ProfileStore(string root)
    {
        Root = root;
        try { if (File.Exists(PathName)) { var data = JsonSerializer.Deserialize<List<SavedProfile>>(ReadLimited(PathName)) ?? throw new InvalidDataException("Empty profile file."); Validate(data); Items = data; } }
        catch (Exception ex) { Error = ex.Message; }
    }
    public static string ReadLimited(string path)
    {
        if (new FileInfo(path).Length > 2_000_000) throw new InvalidDataException("File exceeds 2 MB / Datei größer als 2 MB.");
        return File.ReadAllText(path);
    }
    public static void Validate(List<SavedProfile> items)
    {
        if (items.Count > 500) throw new InvalidDataException("Too many profiles / Zu viele Profile.");
        var ids = new HashSet<string>();
        foreach (var item in items) {
            if (item == null || !Guid.TryParse(item.Id, out _) || !ids.Add(item.Id) || string.IsNullOrWhiteSpace(item.Name) || item.Name.Length > 40 || item.Name.Any(char.IsControl) || item.Macro == null)
                throw new InvalidDataException("Invalid profile / Ungültiges Profil.");
            item.Macro.Validate();
            if (item.ImportSourceId != null && !Guid.TryParse(item.ImportSourceId, out _)) throw new InvalidDataException("Invalid profile source ID.");
        }
    }
    public static string Name(string name)
    {
        name = name.Trim();
        if (name.Length < 1 || name.Length > 40 || name.Any(char.IsControl)) throw new InvalidDataException("Name: 1–40 characters / Zeichen.");
        return name;
    }
    public void Replace(List<SavedProfile> next)
    {
        if (Error != "") throw new IOException(Error);
        Validate(next); Directory.CreateDirectory(Root);
        var temp = PathName + ".tmp"; File.WriteAllText(temp, JsonSerializer.Serialize(next, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, PathName, true); Items = next;
    }
    public SavedProfile Create(string name, Settings config)
    {
        name = Name(name);
        if (Items.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) throw new InvalidDataException("Name already exists / Name bereits vorhanden.");
        var profile = new SavedProfile(Guid.NewGuid().ToString(), name, MacroPreset.From(config), DateTime.UtcNow);
        Replace(Items.Append(profile).ToList()); return profile;
    }
    public void Save(string id, Settings config)
    {
        if (!Items.Any(p => p.Id == id)) throw new InvalidDataException("Profile not found / Profil nicht gefunden.");
        Replace(Items.Select(p => p.Id == id ? p with { Macro = MacroPreset.From(config), UpdatedUtc = DateTime.UtcNow } : p).ToList());
    }
}
public record TransferBundle
{
    public string App { get; init; } = "4sibi";
    public int Version { get; init; } = 1;
    public Settings? Settings { get; init; }
    public List<SavedProfile>? Profiles { get; init; }
}
public static class Transfers
{
    public static void Export(string path, Settings settings, List<SavedProfile> profiles)
    {
        var json = JsonSerializer.Serialize(new TransferBundle { Settings = settings with { }, Profiles = profiles.ToList() }, new JsonSerializerOptions { WriteIndented = true });
        string temp = path + ".tmp"; File.WriteAllText(temp, json); File.Move(temp, path, true);
    }
    public static TransferBundle Read(string path)
    {
        var b = JsonSerializer.Deserialize<TransferBundle>(ProfileStore.ReadLimited(path)) ?? throw new InvalidDataException("Invalid file / Ungültige Datei.");
        if (b.App != "4sibi" || b.Version != 1 || b.Settings == null || b.Profiles == null) throw new InvalidDataException("Not a supported 4sibi export / Kein unterstützter 4sibi-Export.");
        MacroPreset.From(b.Settings).Validate(); ProfileStore.Validate(b.Profiles); b.Settings.Normalize(); return b;
    }
    public static (Settings Settings, int Added, string Backup) Import(string path, Settings current, ProfileStore store)
    {
        var bundle = Read(path); // Validate the entire file before touching local data.
        if (store.Error != "") throw new IOException(store.Error);
        var next = store.Items.ToList(); var ids = new Dictionary<string, string>(); int added = 0;
        foreach (var imported in bundle.Profiles!) {
            string sourceId = imported.ImportSourceId ?? imported.Id;
            var match = next.FirstOrDefault(p => p.Macro == imported.Macro && ((p.Id == imported.Id && p.Name == imported.Name) || (p.ImportSourceId == sourceId)));
            if (match != null) { ids[imported.Id] = match.Id; continue; }
            string id = next.Any(p => p.Id == imported.Id) ? Guid.NewGuid().ToString() : imported.Id;
            string name = imported.Name; int suffix = 2;
            while (next.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) { string tail = " (" + suffix++ + ")"; name = imported.Name[..Math.Min(imported.Name.Length, 40 - tail.Length)] + tail; }
            next.Add(imported with { Id = id, Name = name, ImportSourceId = sourceId }); ids[imported.Id] = id; added++;
        }
        ProfileStore.Validate(next);
        var settings = bundle.Settings! with { ActiveProfileId = ids.GetValueOrDefault(bundle.Settings!.ActiveProfileId, "") };
        Directory.CreateDirectory(store.Root);
        string backup = Path.Combine(store.Root, "backup-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N")[..6] + ".json");
        Export(backup, current, store.Items);
        var old = store.Items.ToList();
        store.Replace(next);
        try { settings.Save(Path.Combine(store.Root, "settings.json")); }
        catch { store.Replace(old); throw; }
        return (settings, added, backup);
    }
}
public record TestTarget(IntPtr Handle, double Left, double Top, double Right, double Bottom)
{
    public bool Allows(IntPtr foreground, double x, double y, bool cursorKnown) => Handle != IntPtr.Zero && foreground == Handle && cursorKnown && x >= Left && x < Right && y >= Top && y < Bottom;
}
public sealed class ClickMeter
{
    public int Count { get; private set; }
    public double Duration { get; private set; }
    public double Average => Duration > 0 ? Count / Duration : 0;
    public readonly List<double> Seconds = new();
    public void Record(double elapsed) { if (elapsed < 0 || elapsed > 10) return; Count++; Seconds.Add(elapsed); Advance(elapsed); }
    public void Advance(double elapsed) => Duration = Math.Clamp(elapsed, 0, 10);
    public double Recent => Seconds.Count(t => t >= Math.Max(0, Duration - 1)) / Math.Min(1, Math.Max(.001, Duration));
}
public record TestResult(DateTime Time, int TargetAps, int Count, double Duration, double Average);
