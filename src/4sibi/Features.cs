using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace Sibi;

public sealed partial class MainWindow
{
    readonly ProfileStore profiles;


    string draftProfileName = "", featureNotice = "";
    public TestResult? LastTest;
    public string ActiveProfileName => profiles.Items.FirstOrDefault(p => p.Id == Config.ActiveProfileId)?.Name ?? T("Ohne Profil", "No profile");
    public ProfileStore Profiles => profiles;
    public string EngineStatus => engine?.Status ?? "paused";
    public void SetTestTarget(TestTarget? target) => engine?.SetTestTarget(target);
    public void FinishTest(TestResult result) { LastTest = result; if (page == 7) ShowPage(7); }

    void FeaturePage(StackPanel p, int index)
    {

        if (index is 6 or 7) TestPage(p, index);

        else if (index == 9) TransferPage(p);
    }
    void Notice(Panel p)
    {
        if (featureNotice == "") return;
        var text = Text(featureNotice, 13); text.Margin = new Thickness(0, 0, 0, 18); text.SetResourceReference(TextBlock.ForegroundProperty, "Accent"); p.Children.Add(text);
    }
    void Attempt(Action action)
    {
        engine?.Stop(); capturing = true; if (engine != null) engine.Suspended = true;
        try { action(); } catch (Exception ex) { ShowMessage(ex.Message); }
        finally { capturing = false; if (engine != null) engine.Suspended = false; }
    }
    public void CreateProfile(string name)
    {
        var profile = profiles.Create(name, Config); Config.ActiveProfileId = profile.Id; selectedProfile = profile.Id; creatingProfile = false; draftProfileName = "";
        featureNotice = T("Profil erstellt: ", "Profile created: ") + profile.Name; SaveSoon(); ShowPage(3);
    }
    public void LoadProfile(string id)
    {
        var profile = profiles.Items.First(p => p.Id == id); profile.Macro.Validate(); selectedProfile = id; creatingProfile = false;
        Config = profile.Macro.Apply(Config) with { ActiveProfileId = profile.Id }; engine?.Configure(Config);
        featureNotice = T("Profil geladen: ", "Profile loaded: ") + profile.Name; SaveSoon(); SyncOverlay(); ShowPage(3);
    }
    public void SaveProfile(string id)
    {
        profiles.Save(id, Config); Config.ActiveProfileId = id; selectedProfile = id; creatingProfile = false; SaveSoon();
        featureNotice = T("Profil gespeichert: ", "Profile saved: ") + ActiveProfileName; ShowPage(3);
    }
    string PresetSummary(MacroPreset preset) => $"{preset.Aps} APS  ·  " + (preset.Action == "key" ? Bindings.Name(preset.ActionKey) : preset.Action == "left" ? T("Linksklick", "Left click") : T("Rechtsklick", "Right click")) + $"  ·  {Bindings.Name(preset.Trigger)}  ·  " + (preset.Hold ? T("Halten", "Hold") : T("Umschalten", "Toggle"));
    public void ExportTo(string path) { if (profiles.Error != "") throw new IOException(profiles.Error); Transfers.Export(path, Config, profiles.Items); }
    public void ImportFrom(string path)
    {
        var result = Transfers.Import(path, Config, profiles); Config = result.Settings; engine?.Configure(Config); ApplyTheme(); BuildShell(); SetupTrayMenu(); SyncOverlay();
        featureNotice = T($"Importiert · {result.Added} Profile hinzugefügt. Sicherung: ", $"Imported · {result.Added} profiles added. Backup: ") + Path.GetFileName(result.Backup); ShowPage(9);
    }
    void TransferPage(StackPanel p)
    {
        Heading(p, T("Export / Import", "Export / import"), T("Einstellungen und Profile als eine Datei sichern oder übertragen.", "Back up or transfer settings and profiles in one file.")); Notice(p);
        var export = Card(p, T("Einstellungen exportieren", "Export settings"), T("Enthält Profile, Tasten, Sprache, Farben und Overlay-Einstellungen.", "Includes profiles, keys, language, colors and overlay preferences."));
        Row(export, Text(T($"{profiles.Items.Count} Profile enthalten", $"{profiles.Items.Count} profiles included"), 12, true), Btn(T("Exportieren …", "Export …"), () => Attempt(() => {
            var dialog = new SaveFileDialog { Filter = "4sibi JSON (*.json)|*.json", FileName = "4sibi-backup-" + DateTime.Now.ToString("yyyy-MM-dd") + ".json", AddExtension = true, DefaultExt = ".json" };
            if (dialog.ShowDialog(this) == true) { ExportTo(dialog.FileName); featureNotice = T("Export gespeichert: ", "Export saved: ") + Path.GetFileName(dialog.FileName); ShowPage(9); }
        }), true));
        var import = Card(p, T("Einstellungen importieren", "Import settings"), T("Übernimmt die Einstellungen. Bestehende Profile bleiben erhalten; Namenskonflikte erhalten einen Zusatz.", "Applies settings. Existing profiles are kept; conflicting names get a suffix."));
        Row(import, Text(T("Vorher wird automatisch eine Sicherung erstellt.", "A backup is created automatically before importing."), 12, true), Btn(T("Importieren …", "Import …"), () => Attempt(() => {
            var dialog = new OpenFileDialog { Filter = "4sibi JSON (*.json)|*.json", CheckFileExists = true };
            if (dialog.ShowDialog(this) == true) ImportFrom(dialog.FileName);
        })));
        var hint = Text(T("Autostart wird nicht übertragen. Ein Import startet niemals das Makro. Sicherungen liegen im lokalen 4sibi-Datenordner.", "Startup registration is not transferred. Import never starts the macro. Backups are stored in the local 4sibi data folder."), 13, true); p.Children.Add(hint);
    }
    void TestPage(StackPanel p, int index)
    {
        Heading(p, index == 6 ? T("Klick- & Tastentest", "Click & key test") : T("Letzte Auswertung", "Latest results"), T("Miss die empfangenen Aktionen in einer eigenen Testfläche.", "Measure received actions in a dedicated test surface."));
        if (index == 6) {
            var card = Card(p, T("10-Sekunden-Test", "10-second test"), PresetSummary(MacroPreset.From(Config)));
            var steps = Text(T("1. Testfenster öffnen.\n2. Maus in die große Testfläche bewegen.\n3. Deinen Start-Hotkey drücken bzw. halten.\n\nDer Test stoppt nach 10 Sekunden. Not-Aus und Fensterwechsel stoppen ebenfalls. Der Roblox-Modus bleibt für normale Makros erhalten.", "1. Open the test window.\n2. Move the pointer into the large test pad.\n3. Press or hold your trigger key.\n\nThe test stops after 10 seconds. Emergency stop and switching windows also stop it. Roblox mode stays enabled for normal macros."), 15, true); steps.Margin = new Thickness(0, 20, 0, 0); card.Children.Add(steps);
            Row(card, Text(T("Misst echte Fenstereingaben, keine berechneten Sollwerte.", "Measures actual window input, not calculated targets."), 12, true), Btn(T("Testfenster öffnen", "Open test window"), () => { engine?.Stop(); var bench = new TestBench(this); bench.ShowDialog(); ShowPage(7); }, true));
        } else {
            if (LastTest == null) { var empty = Card(p, T("Noch kein Testergebnis", "No test result yet")); Row(empty, Text(""), Btn(T("Zum Test", "Go to test"), () => ShowPage(6), true)); return; }
            var result = LastTest;
            var stats = Card(p, T("Letzter Durchlauf", "Latest run"), result.Time.ToLocalTime().ToString("g"));
            Row(stats, Text(T("Empfangene Aktionen", "Received actions")), Text(result.Count.ToString(), 27, bold: true));
            Row(stats, Text(T("Durchschnittliche APS", "Average APS")), Text(result.Average.ToString("0.0"), 27, bold: true));
            Row(stats, Text(T("Zielrate", "Target rate")), Text(result.TargetAps + " APS", 18));
            Row(stats, Text(T("Messdauer", "Duration")), Text(result.Duration.ToString("0.00") + " s", 18));
            Row(stats, Text(T("Das Ergebnis gilt für diese Testfläche, nicht automatisch für Roblox.", "This result applies to the test surface, not automatically to Roblox."), 12, true), Btn(T("Erneut testen", "Test again"), () => ShowPage(6)));
        }
    }
}

public sealed class TestBench : Window
{
    readonly MainWindow main;
    readonly Settings config;
    readonly Border pad;
    readonly TextBlock stats;
    readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(50) };
    readonly Stopwatch clock = new();
    readonly ClickMeter meter = new();
    bool finished;
    public TestBench(MainWindow owner)
    {
        main = owner; config = owner.Config with { }; Owner = owner; Title = "4sibi · " + owner.T("Testbereich", "Test area"); Width = 600; Height = 410; ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = MainWindow.Brush("#0B0C0F"); Foreground = Brushes.White; FontFamily = owner.FontFamily;
        var root = new Grid { Margin = new Thickness(24) }; root.RowDefinitions.Add(new() { Height = GridLength.Auto }); root.RowDefinitions.Add(new()); root.RowDefinitions.Add(new() { Height = GridLength.Auto }); Content = root;
        var heading = owner.Text(owner.T($"Hotkey: {Bindings.Name(config.Trigger)}   ·   Not-Aus: {Bindings.Name(config.Emergency)}", $"Trigger: {Bindings.Name(config.Trigger)}   ·   Stop: {Bindings.Name(config.Emergency)}"), 16, bold: true); heading.Margin = new Thickness(0, 0, 0, 18); root.Children.Add(heading);
        var inside = new StackPanel { VerticalAlignment = VerticalAlignment.Center }; inside.Children.Add(owner.Text(owner.T("MAUS HIERHER BEWEGEN", "MOVE POINTER HERE"), 23, bold: true));
        var instructions = owner.Text(owner.T("Dann den Start-Hotkey betätigen.\nDie Messung beginnt mit der ersten Aktion.", "Then use your trigger key.\nMeasurement starts with the first action."), 14, true); instructions.Margin = new Thickness(0, 14, 0, 0); inside.Children.Add(instructions);
        pad = new Border { Background = MainWindow.Brush("#181B22"), BorderBrush = MainWindow.Brush(config.Accent), BorderThickness = new Thickness(2), CornerRadius = new CornerRadius(14), Padding = new Thickness(28), Child = inside, Focusable = true }; Grid.SetRow(pad, 1); root.Children.Add(pad);
        stats = owner.Text("0 / 10 s   ·   0 " + owner.T("Aktionen", "actions"), 16); stats.Margin = new Thickness(0, 18, 0, 0); Grid.SetRow(stats, 2); root.Children.Add(stats);
        Loaded += (_, _) => { pad.Focus(); ConfigureTarget(); timer.Start(); };
        LocationChanged += (_, _) => { if (IsLoaded) { main.StopMacro(); ConfigureTarget(); } };
        pad.MouseEnter += (_, _) => { if (!finished) pad.Focus(); };
        pad.PreviewMouseDown += (_, e) => { e.Handled = true; if ((config.Action == "left" && e.ChangedButton == MouseButton.Left) || (config.Action == "right" && e.ChangedButton == MouseButton.Right)) Receive(); };
        PreviewKeyDown += (_, e) => { var key = e.Key == Key.System ? e.SystemKey : e.Key; if (!e.IsRepeat && config.Action == "key" && KeyInterop.VirtualKeyFromKey(key) == config.ActionKey) Receive(); e.Handled = true; };
        Deactivated += (_, _) => { if (IsLoaded) Finish(); };
        Closing += (_, _) => Finish();
        timer.Tick += (_, _) => {
            if (clock.IsRunning) {
                meter.Advance(clock.Elapsed.TotalSeconds); stats.Text = $"{meter.Duration:0.0} / 10 s   ·   {meter.Count} " + main.T("Aktionen", "actions") + $"   ·   Ø {meter.Average:0.0} APS";
                if (clock.Elapsed.TotalSeconds >= 10 || main.EngineStatus != "running") Finish();
            }
        };
    }
    void ConfigureTarget()
    {
        if (finished) return;
        pad.UpdateLayout(); var a = pad.PointToScreen(new Point(8, 8)); var b = pad.PointToScreen(new Point(pad.ActualWidth - 8, pad.ActualHeight - 8));
        main.SetTestTarget(new TestTarget(new System.Windows.Interop.WindowInteropHelper(this).Handle, a.X, a.Y, b.X, b.Y));
    }
    void Receive()
    {
        if (finished || main.EngineStatus != "running") return;
        if (!clock.IsRunning) clock.Start(); meter.Record(clock.Elapsed.TotalSeconds);
    }
    void Finish()
    {
        if (finished) return; finished = true; timer.Stop(); main.StopMacro();
        // Keep an impossible test target until this window closes, so a hotkey
        // cannot fall back to a general macro while viewing the test result.
        main.SetTestTarget(new TestTarget(IntPtr.Zero, 0, 0, 0, 0));
        if (clock.IsRunning) { meter.Advance(clock.Elapsed.TotalSeconds); clock.Stop(); main.FinishTest(new(DateTime.UtcNow, config.Aps, meter.Count, meter.Duration, meter.Average)); }
        stats.Text = main.T("Beendet", "Finished") + $"   ·   {meter.Count}   ·   Ø {meter.Average:0.0} APS";
    }
    protected override void OnClosed(EventArgs e) { Finish(); main.SetTestTarget(null); base.OnClosed(e); }
}
