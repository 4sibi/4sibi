using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Sibi;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        bool testing = args.Contains("--self-test");
        using var single = new Mutex(true, "Local\\4sibi.desktop.v1", out bool created);
        if (!testing && !created) { MessageBox.Show("4sibi läuft bereits. Öffne es über das Symbol im Infobereich.\n4sibi is already running. Open it from the system tray.", "4sibi"); return 0; }
        var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
        if (testing) {
            string directory = args.Length > 1 ? Path.GetFullPath(args[1]) : Path.Combine(Environment.CurrentDirectory, "test-results");
            Directory.CreateDirectory(directory);
            try {
                SelfTests.Run();
                SelfTests.Data(directory);
                var win = new MainWindow(true, Path.Combine(directory, "ui-data-" + Guid.NewGuid().ToString("N"))); app.MainWindow = win;
                win.Loaded += (_, _) => win.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => {
                    try {
                        SelfTests.Ui(win, directory);
                        File.WriteAllText(Path.Combine(directory, "result.txt"), "PASS\n" + SelfTests.Checks + " checks passed.\nNo OS input was emitted.\n");
                        app.Shutdown(0);
                    } catch (Exception ex) { File.WriteAllText(Path.Combine(directory, "result.txt"), ex.ToString()); app.Shutdown(1); }
                }));
                return app.Run(win);
            } catch (Exception ex) { File.WriteAllText(Path.Combine(directory, "result.txt"), ex.ToString()); return 1; }
        }
        app.DispatcherUnhandledException += (_, e) => {
            try { Directory.CreateDirectory(Settings.DirectoryPath); File.AppendAllText(Path.Combine(Settings.DirectoryPath, "error.log"), DateTime.Now + "\n" + e.Exception + "\n"); } catch { }
            MessageBox.Show("4sibi wurde nach einem Fehler beendet.\n4sibi stopped after an error.", "4sibi");
            e.Handled = true; app.MainWindow?.Close();
        };
        var window = new MainWindow(); app.MainWindow = window;
        if (args.Contains("--tray")) window.Loaded += (_, _) => window.Hide();
        return app.Run(window);
    }
}

public static class SelfTests
{
    public static int Checks;
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
    static int HitTest(Window win, Point point)
    {
        var screen = win.PointToScreen(point);
        int packed = unchecked(((int)(short)screen.Y << 16) | (ushort)(short)screen.X);
        return (int)SendMessage(new System.Windows.Interop.WindowInteropHelper(win).Handle, 0x0084, IntPtr.Zero, new IntPtr(packed));
    }
    static void Check(bool condition, string message) { Checks++; if (!condition) throw new Exception("FAIL: " + message); }
    public static void Run()
    {
        var s = new Settings(); var m = new MacroState();
        Check(!m.Tick(0, false, false, true, false, s, .5), "idle emits nothing");
        Check(m.Tick(1, true, false, true, false, s, .5), "toggle key starts");
        Check(!m.Tick(2, true, false, true, false, s, .5), "held toggle does not toggle repeatedly");
        Check(!m.Tick(50, false, false, true, false, s, .5), "20 APS interval enforced");
        Check(m.Tick(51, false, false, true, false, s, .5), "next action at due time");
        Check(!m.Tick(52, true, false, true, false, s, .5) && !m.Armed, "toggle stops");
        m = new();
        Check(!m.Tick(0, true, false, false, false, s, .5) && m.Armed && !m.Running, "wait for allowed foreground");
        Check(m.Tick(1, false, false, true, false, s, .5), "start in allowed foreground");
        Check(!m.Tick(2, false, false, false, false, s, .5) && !m.Armed, "focus loss clears activation");
        Check(!m.Tick(100, false, false, true, false, s, .5), "no automatic restart after focus loss");
        m = new(); var hold = s with { Hold = true };
        Check(m.Tick(0, true, false, true, false, hold, .5), "hold starts");
        Check(!m.Tick(50, false, false, true, false, hold, .5) && !m.Armed, "release stops immediately");
        Check(m.Tick(60, true, false, true, false, hold, .5), "hold can restart");
        Check(!m.Tick(70, true, true, true, false, hold, .5), "emergency takes priority");
        Check(!m.Tick(200, true, false, true, false, hold, .5), "held trigger cannot undo emergency");
        m.Tick(201, false, false, true, false, hold, .5);
        Check(m.Tick(202, true, false, true, false, hold, .5), "fresh hold after emergency works");
        Check(!m.Tick(203, true, false, false, false, hold, .5), "hold mode focus loss stops");
        Check(!m.Tick(300, true, false, true, false, hold, .5), "hold mode requires release after focus loss");
        m = new();
        Check(!m.Tick(0, true, false, true, true, s, .5), "capture/edit suspension blocks trigger");
        Check(!m.Tick(100, true, false, true, false, s, .5), "leaving capture cannot activate a held key");
        m = new(); m.Toggle();
        Check(m.Tick(1000, false, false, true, false, s, .5), "UI arm starts");
        Check(!m.Tick(1000, false, false, true, false, s, .5), "no catch-up burst after stall");
        m = new(); m.Toggle(); m.Tick(0, false, false, true, false, s with { Randomize = true }, 0);
        Check(Math.Abs(m.NextDue - 42.5) < .001, "jitter lower bound");
        m = new(); m.Toggle(); m.Tick(0, false, false, true, false, s with { Randomize = true }, 1);
        Check(Math.Abs(m.NextDue - 57.5) < .001, "jitter upper bound");
        Check(Bindings.Conflict(s with { Trigger = 1 }), "mouse output conflict rejected");
        Check(Bindings.Conflict(s with { Action = "key", ActionKey = s.Trigger }), "keyboard output conflict rejected");
        Check(Bindings.Conflict(s with { Emergency = s.Trigger }), "stop/trigger conflict rejected");
        Check(!Bindings.Conflict(s with { Trigger = 5 }), "side mouse trigger supported");
        var invalid = new Settings { Aps = 2000, Glow = double.NaN, Trigger = -1, Language = "unknown", Accent = "bad" }; invalid.Normalize();
        Check(invalid.Aps == 1000 && invalid.Language == "de" && invalid.Glow == .45 && !Bindings.Conflict(invalid), "loaded settings sanitized");
        var roundtrip = JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(s with { Language = "en", Accent = "#AD64FF", Trigger = 5 }));
        Check(roundtrip?.Language == "en" && roundtrip.Trigger == 5 && roundtrip.Accent == "#AD64FF", "settings round-trip");
        Check(Marshal.SizeOf<Native.INPUT>() == (IntPtr.Size == 8 ? 40 : 28), "native input ABI size");
        var mouse = Native.Inputs(s);
        Check(mouse.Length == 2 && mouse[0].u.mouse.flags == 2 && mouse[1].u.mouse.flags == 4, "balanced left click");
        var right = Native.Inputs(s with { Action = "right" });
        Check(right[0].u.mouse.flags == 8 && right[1].u.mouse.flags == 16, "balanced right click");
        var key = Native.Inputs(s with { Action = "key" });
        Check(key[0].type == 1 && (key[1].u.key.flags & 2) != 0, "balanced keyboard press");
        Check(Native.IsRoblox("RobloxPlayerBeta") && !Native.IsRoblox("chrome") && !Native.IsRoblox("RobloxStudioBeta"), "client process identity");
        using (var engine = new Engine(s, suspended: true)) {
            Thread.Sleep(80);
            Check(engine.Status == "paused" && engine.Count == 0, "native scheduler starts safely while suspended");
        }
    }
    static System.Collections.Generic.IEnumerable<DependencyObject> Walk(DependencyObject node)
    {
        yield return node;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++) foreach (var child in Walk(VisualTreeHelper.GetChild(node, i))) yield return child;
    }
    public static void Data(string directory)
    {
        var root = Path.Combine(directory, "data-" + Guid.NewGuid().ToString("N"));
        var store = new ProfileStore(root); var config = new Settings { Aps = 55, Language = "en", Accent = "#AD64FF" };
        var created = store.Create("Arena", config);
        Check(new ProfileStore(root).Items.Single().Macro.Aps == 55, "profiles persist across reload");
        store.Save(created.Id, config with { Aps = 80 });
        Check(new ProfileStore(root).Items.Single().Macro.Aps == 80, "profile save replaces selected preset");
        var applied = store.Items.Single().Macro.Apply(new Settings { Language = "de", Accent = "#24D989" });
        Check(applied.Aps == 80 && applied.Language == "de" && applied.Accent == "#24D989", "profile load preserves interface preferences");
        bool duplicateRejected = false; try { store.Create("arena", config); } catch (InvalidDataException) { duplicateRejected = true; }
        Check(duplicateRejected && store.Items.Count == 1, "duplicate names do not overwrite profiles");
        string exported = Path.Combine(root, "export.json");
        var settings = config with { ActiveProfileId = created.Id, OverlayScale = 1.25, OverlayLocked = true, OverlayShowProfile = true };
        Transfers.Export(exported, settings, store.Items);
        Check(Transfers.Read(exported).Settings!.OverlayScale == 1.25, "overlay preferences exported");
        var imported = Transfers.Import(exported, new Settings(), store);
        Check(imported.Added == 0 && store.Items.Count == 1, "identical imports do not duplicate profiles");
        Check(File.Exists(imported.Backup) && Transfers.Read(imported.Backup).Settings!.Language == "de", "pre-import backup preserves old settings");
        Check(imported.Settings.Language == "en" && imported.Settings.OverlayLocked, "language and overlay restored");
        store.Save(created.Id, config with { Aps = 10 });
        var conflict = Transfers.Import(exported, config, store);
        Check(conflict.Added == 1 && store.Items.Any(p => p.Macro.Aps == 10) && store.Items.Any(p => p.Macro.Aps == 80), "conflicting imports preserve both presets");
        Check(store.Items.Select(p => p.Name).Distinct().Count() == 2 && conflict.Settings.ActiveProfileId != created.Id, "conflicting import remaps active profile and name");
        var repeatConflict = Transfers.Import(exported, config, store);
        Check(repeatConflict.Added == 0 && store.Items.Count == 2 && repeatConflict.Settings.ActiveProfileId == conflict.Settings.ActiveProfileId, "reimport after a conflict reuses the imported copy");
        string before = File.ReadAllText(store.PathName), bad = Path.Combine(root, "invalid.json"); File.WriteAllText(bad, "{\"App\":\"other\",\"Version\":1}");
        bool invalidRejected = false; try { Transfers.Import(bad, config, store); } catch (InvalidDataException) { invalidRejected = true; }
        Check(invalidRejected && File.ReadAllText(store.PathName) == before, "invalid import leaves profiles untouched");
        var corruptDir = Path.Combine(root, "corrupt"); Directory.CreateDirectory(corruptDir); File.WriteAllText(Path.Combine(corruptDir, "profiles.json"), "bad JSON");
        var corrupt = new ProfileStore(corruptDir); bool writeBlocked = false; try { corrupt.Create("new", config); } catch (IOException) { writeBlocked = true; }
        Check(writeBlocked && File.ReadAllText(corrupt.PathName) == "bad JSON", "corrupt profile file is not silently overwritten");
        var rollback = new ProfileStore(Path.Combine(root, "rollback")); rollback.Create("Original", config); var original = File.ReadAllText(rollback.PathName);
        Directory.CreateDirectory(Path.Combine(rollback.Root, "settings.json")); bool failed = false;
        try { Transfers.Import(exported, config, rollback); } catch (IOException) { failed = true; } catch (UnauthorizedAccessException) { failed = true; }
        Check(failed && File.ReadAllText(rollback.PathName) == original, "failed settings write rolls back profile import");
        var target = new TestTarget(new IntPtr(42), 100, 100, 400, 400);
        Check(target.Allows(new IntPtr(42), 200, 200, true), "test input allowed inside designated pad");
        Check(!target.Allows(new IntPtr(43), 200, 200, true), "test cannot target another window");
        Check(!target.Allows(new IntPtr(42), 401, 200, true) && !target.Allows(new IntPtr(42), 200, 200, false), "test stops outside pad or on unknown cursor");
        Check(!new TestTarget(IntPtr.Zero, 0, 0, 100, 100).Allows(IntPtr.Zero, 50, 50, true), "finished test cannot emit input");
        var meter = new ClickMeter(); for (int i = 0; i < 200; i++) meter.Record(i / 20.0); meter.Advance(10);
        Check(meter.Count == 200 && Math.Abs(meter.Average - 20) < .001, "measured APS uses actual received events");
        meter.Record(10.01); Check(meter.Count == 200, "late events excluded from test measurement");
    }
    static void Click(MainWindow win, string label)
    {
        var button = Walk(win).OfType<Button>().FirstOrDefault(b => b.Content is string s && s == label);
        Check(button != null, "button exists: " + label);
        button!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); win.UpdateLayout();
    }
    public static void Ui(MainWindow win, string directory)
    {
        win.ShowPage(0); win.UpdateLayout();
        Check(HitTest(win, new Point(win.ActualWidth / 2, 40)) == 2, "blank title bar is a native drag area");
        Check(HitTest(win, new Point(60, 40)) == 2, "logo is a native drag area");
        Check(HitTest(win, new Point(350, 160)) == 1, "page content remains interactive");
        foreach (string caption in new[] { "−", "×" }) {
            var control = Walk(win).OfType<Button>().First(b => b.Content as string == caption);
            var center = control.TransformToAncestor(win).Transform(new Point(control.ActualWidth / 2, control.ActualHeight / 2));
            Check(HitTest(win, center) == 1, "title button remains clickable: " + caption);
        }
        var number = Walk(win).OfType<TextBox>().First(); number.Text = "75"; number.RaiseEvent(new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice, 0, number, win) { RoutedEvent = UIElement.LostKeyboardFocusEvent });
        Check(win.Config.Aps == 75, "APS numeric edit reaches config");
        Click(win, "Tastatur"); Check(win.Config.Action == "key", "keyboard action UI binding");
        Click(win, "Gedrückt halten"); Check(win.Config.Hold, "hold mode UI binding");
        Click(win, "Umschalten"); Check(!win.Config.Hold, "toggle mode UI binding");
        Click(win, "Linksklick"); win.Update(s => s.Aps = 20, true);
        win.UpdateLayout();
        Check(win.ActualWidth <= 820 && win.ActualHeight <= 610, "default window stays compact");
        var macroScroll = Walk(win).OfType<ScrollViewer>().First(s => s.Content is StackPanel panel && Walk(panel).OfType<TextBox>().Any());
        Check(macroScroll.ScrollableHeight < 1, "default macro controls fit without scrolling");
        Snapshot(win, Path.Combine(directory, "01-makro.png"));
        Check(win.Icon != null && Branding.TrayIcon.Width == 32, "embedded window and tray icons load");
        Check(win.Icon is BitmapSource taskbarSource && taskbarSource.PixelWidth >= 256, "taskbar receives high-resolution source instead of first small ICO frame");
        using (var trayMenu = win.CreateTrayMenu()) {
            trayMenu.Show(100, 100);
            Check(trayMenu.Renderer is DarkTrayRenderer, "tray uses dark renderer");
            using var bitmap = new System.Drawing.Bitmap(trayMenu.Width, trayMenu.Height);
            trayMenu.DrawToBitmap(bitmap, new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height));
            bitmap.Save(Path.Combine(directory, "14-tray.png"));
            trayMenu.Close();
            trayMenu.Items[2].PerformClick();
            win.UpdateLayout();
            Check(Walk(win).OfType<TextBlock>().Any(t => t.Text == "Application Settings"), "tray settings command opens settings");
            win.ShowPage(0);
        }
        var overlay = new OverlayWindow(win); overlay.Show(); overlay.UpdateStatus("paused"); overlay.UpdateLayout();
        Check(overlay.Width == 300 && overlay.Height == 44, "default overlay is a small pill");
        Check(!Walk(overlay).OfType<Button>().Any(), "pill has no persistent window buttons");
        var pillMenu = Walk(overlay).OfType<Border>().First(b => b.ContextMenu != null).ContextMenu;
        Check(pillMenu.Items.OfType<MenuItem>().Count() == 5, "pill context menu is populated before opening");
        var lockItem = pillMenu.Items.OfType<MenuItem>().First(m => m.Header as string == "Position sperren");
        lockItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); Check(win.Config.OverlayLocked, "pill menu locks position");
        lockItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); Check(!win.Config.OverlayLocked, "pill menu unlocks position");
        Snapshot(overlay, Path.Combine(directory, "06-overlay.png")); overlay.Close();
        win.ShowPage(1); win.UpdateLayout();
        Check(Walk(win).OfType<TextBlock>().Any(t => t.Text == "Kein Roblox-Client geöffnet"), "honest empty client state");
        Snapshot(win, Path.Combine(directory, "02-spielmodus.png"));
        win.ShowPage(2); win.UpdateLayout();
        Click(win, "English"); Check(win.Config.Language == "en", "live English switch");
        win.ShowPage(10); win.UpdateLayout();
        Check(Walk(win).OfType<TextBlock>().Any(t => t.Text == "Appearance"), "visible labels translated");
        var purple = Walk(win).OfType<Button>().First(b => b.ToolTip as string == "Purple"); purple.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(win.Config.Accent == "#AD64FF", "theme swatch binding");
        win.UpdateLayout(); Snapshot(win, Path.Combine(directory, "04-settings-en-purple.png"));
        win.ShowPage(2); win.UpdateLayout(); Click(win, "Deutsch"); win.Update(s => s.Accent = "#FF1838", true);
        Snapshot(win, Path.Combine(directory, "03-einstellungen.png"));
        win.ShowPage(0); win.Width = 800; win.Height = 580; win.UpdateLayout();
        Snapshot(win, Path.Combine(directory, "05-small-window.png"));
        Check(win.ActualWidth == 800, "compact minimum size supported");
        var start = Walk(win).OfType<Button>().First(b => b.Content as string == "Starten");
        var location = start.TransformToAncestor(win).Transform(new Point(0, 0));
        Check(location.Y >= 0 && location.Y + start.ActualHeight < win.ActualHeight, "start/stop stays visible in a small window");
        win.Width = 820; win.Height = 610;
        win.ShowPage(4); win.UpdateLayout();
        var profileName = Walk(win).OfType<TextBox>().Single(); profileName.Text = "Blade Ball 20";
        Click(win, "Erstellen"); Check(win.Profiles.Items.Count == 1, "create profile form writes preset");
        string profileId = win.Profiles.Items.Single().Id;
        win.Update(s => s.Aps = 75); win.ShowPage(5); win.UpdateLayout(); Click(win, "Speichern");
        Check(win.Profiles.Items.Single().Macro.Aps == 75, "save profile page stores current macro");
        win.Update(s => s.Aps = 20); win.ShowPage(3); win.UpdateLayout(); Click(win, "Laden");
        Check(win.Config.Aps == 75 && win.Config.ActiveProfileId == profileId, "load profile page applies preset");
        Snapshot(win, Path.Combine(directory, "07-profile.png"));
        Check(!Walk(win).OfType<Expander>().Any(), "navigation has no dropdowns");
        var icons = Walk(win).OfType<Button>().Where(b => b.Content is TextBlock text && text.FontFamily.Source == "Segoe MDL2 Assets").ToList();
        Check(icons.Count == 5 && icons.All(b => b.ToolTip is string), "five icon destinations with tooltips");
        Check(Walk(win).OfType<Button>().Any(b => b.Content as string == "Laden") && Walk(win).OfType<Button>().Any(b => b.Content as string == "Speichern"), "load and save share the profile page");
        win.ShowPage(11); win.UpdateLayout();
        var lockToggle = Walk(win).OfType<CheckBox>().First(c => System.Windows.Automation.AutomationProperties.GetName(c) == "Position sperren"); lockToggle.IsChecked = true;
        Check(win.Config.OverlayLocked, "overlay lock connected to UI");
        win.ShowPage(8); win.UpdateLayout();
        var overlaySize = Walk(win).OfType<Slider>().First(c => System.Windows.Automation.AutomationProperties.GetName(c) == "Overlay-Größe"); overlaySize.Value = 1.25;
        Check(Math.Abs(win.Config.OverlayScale - 1.25) < .001, "overlay size connected to UI");
        Snapshot(win, Path.Combine(directory, "08-overlay-settings.png"));
        win.ShowPage(6); Snapshot(win, Path.Combine(directory, "09-test-area.png"));
        var bench = new TestBench(win); bench.Show(); bench.UpdateLayout(); Snapshot(bench, Path.Combine(directory, "12-test-window.png")); bench.Close();
        win.Update(s => s.OverlayShowProfile = true);
        var customOverlay = new OverlayWindow(win); customOverlay.Show(); customOverlay.UpdateStatus("paused"); customOverlay.UpdateLayout();
        Check(Math.Abs(customOverlay.Width - 525) < .1 && customOverlay.Cursor == Cursors.Arrow, "custom overlay applies size and locked position");
        Check(Walk(customOverlay).OfType<TextBlock>().Any(t => t.Text.Contains("Blade Ball 20")), "custom overlay displays active profile");
        Snapshot(customOverlay, Path.Combine(directory, "13-custom-overlay.png")); customOverlay.Close();
        win.FinishTest(new TestResult(DateTime.UtcNow, 75, 700, 10, 70)); win.ShowPage(7);
        Snapshot(win, Path.Combine(directory, "10-test-results.png"));
        string transfer = Path.Combine(directory, "ui-export.json"); win.ExportTo(transfer); win.Update(s => s.Aps = 11); win.ImportFrom(transfer);
        Check(win.Config.Aps == 75 && win.Profiles.Items.Count == 1, "UI export/import restores settings without duplicate profiles");
        Snapshot(win, Path.Combine(directory, "11-transfer.png"));
        win.ShowPage(0);
    }
    static void Snapshot(Window win, string path)
    {
        win.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)win.ActualWidth, (int)win.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(win); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path); encoder.Save(stream);
    }
}
