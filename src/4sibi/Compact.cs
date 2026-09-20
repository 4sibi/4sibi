using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Sibi;

public sealed partial class MainWindow
{
    string selectedProfile = "";
    bool creatingProfile;
    void CompactNavigation(StackPanel panel)
    {
        foreach (var item in new[] { (0, "\uE962", T("Makro", "Macro")), (3, "\uE8B7", T("Profile", "Profiles")), (6, "\uE9D9", T("Testbereich", "Test area")), (8, "\uE737", "Overlay"), (2, "\uE713", T("Einstellungen", "Settings")) }) {
            var icon = Text(item.Item2, 22); icon.FontFamily = new FontFamily("Segoe MDL2 Assets");
            var button = Btn("", () => { engine?.Stop(); featureNotice = ""; ShowPage(item.Item1); });
            button.Content = icon; button.Width = 46; button.Height = 48; button.Padding = new Thickness(0); button.Margin = new Thickness(0, 0, 0, 14); button.BorderThickness = new Thickness(0); button.ToolTip = item.Item3;
            Accessible(button, item.Item3); nav[item.Item1] = button; panel.Children.Add(button);
        }
    }
    void CompactTabs(StackPanel panel, int current)
    {
        (int, string)[] tabs = current switch {
            1 or 2 or 9 or 10 or 12 => new[] { (2, T("Allgemein", "General")), (10, "Design"), (1, "Roblox"), (12, "Hotkeys"), (9, T("Daten", "Data")) },
            6 or 7 => new[] { (6, T("Testbereich", "Test area")), (7, T("Auswertung", "Results")) },
            8 or 11 => new[] { (8, T("Anzeige", "Display")), (11, T("Position", "Position")) },
            _ => Array.Empty<(int, string)>()
        };
        if (tabs.Length == 0) return;
        var row = new WrapPanel { Margin = new Thickness(0, 0, 0, 17) };
        foreach (var (id, title) in tabs) {
            var button = Btn(title, () => { engine?.Stop(); ShowPage(id); }); button.MinHeight = 31; button.Padding = new Thickness(12, 6, 12, 6); button.Margin = new Thickness(0, 0, 6, 0);
            button.Background = current == id ? (Brush)Resources["AccentTint"] : Brushes.Transparent;
            button.BorderBrush = current == id ? (Brush)Resources["Accent"] : Brushes.Transparent;
            row.Children.Add(button);
        }
        panel.Children.Add(row);
    }
    (StackPanel Left, StackPanel Right) Columns(Panel parent, double leftWeight = 1, double rightWeight = 1)
    {
        var grid = new Grid(); grid.ColumnDefinitions.Add(new() { Width = new GridLength(leftWeight, GridUnitType.Star) }); grid.ColumnDefinitions.Add(new() { Width = new GridLength(14) }); grid.ColumnDefinitions.Add(new() { Width = new GridLength(rightWeight, GridUnitType.Star) });
        var left = new StackPanel(); var right = new StackPanel(); grid.Children.Add(left); Grid.SetColumn(right, 2); grid.Children.Add(right); parent.Children.Add(grid); return (left, right);
    }
    Button SmallChoice(string label, Action action, bool active)
    {
        var button = Btn(label, action, active); button.Padding = new Thickness(10, 7, 10, 7); button.MinHeight = 34; button.Margin = new Thickness(0, 0, 5, 0); return button;
    }
    void CompactMacro(StackPanel p)
    {
        var chip = Btn(ActiveProfileName, () => ShowPage(3)); chip.MaxWidth = 190; chip.ToolTip = ActiveProfileName;
        var heading = Row(p, Text(T("Makro", "Macro"), 27, bold: true), chip, 0); heading.Margin = new Thickness(0, 0, 0, 18);
        var cadence = Card(p, "");
        var description = new StackPanel(); description.Children.Add(Text(T("Geschwindigkeit", "Cadence"), 16, bold: true)); var label = Text("Actions per second", 12, true); label.Margin = new Thickness(0, 5, 0, 0); description.Children.Add(label);
        var value = new StackPanel { Orientation = Orientation.Horizontal };
        var number = new TextBox { Text = Config.Aps.ToString(), Width = 85, MaxLength = 4, TextAlignment = TextAlignment.Right, FontSize = 28, FontWeight = FontWeights.SemiBold, BorderThickness = new Thickness(0), Background = Brushes.Transparent, Padding = new Thickness(4), ToolTip = "1–1000 APS" }; Accessible(number, "Actions per second"); value.Children.Add(number);
        var unit = Text("APS", 12, true); unit.Margin = new Thickness(8, 0, 0, 0); value.Children.Add(unit); Row(cadence, description, value, 0);
        var slider = new Slider { Minimum = 1, Maximum = 1000, Value = Config.Aps, TickFrequency = 1, IsSnapToTickEnabled = true, IsMoveToPointEnabled = true, Margin = new Thickness(0, 12, 0, 0) }; Accessible(slider, "Actions per second"); cadence.Children.Add(slider);
        var interval = Text("", 11, true); cadence.Children.Add(interval);
        void UpdateInterval() => interval.Text = T($"{1000.0 / Config.Aps:0.##} ms pro Aktion", $"{1000.0 / Config.Aps:0.##} ms per action"); UpdateInterval();
        slider.ValueChanged += (_, _) => { Update(s => s.Aps = (int)slider.Value); number.Text = Config.Aps.ToString(); UpdateInterval(); };
        void Commit() { if (int.TryParse(number.Text, out int n)) slider.Value = Math.Clamp(n, 1, 1000); number.Text = Config.Aps.ToString(); }
        number.LostKeyboardFocus += (_, _) => Commit(); number.KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Enter) { Commit(); System.Windows.Input.Keyboard.ClearFocus(); } };
        var columns = Columns(p);
        var action = Card(columns.Left, T("Aktion", "Action")); var options = new WrapPanel { Margin = new Thickness(0, 16, 0, 0) };
        foreach (var (id, name) in new[] { ("left", T("Linksklick", "Left click")), ("right", T("Rechtsklick", "Right click")), ("key", T("Tastatur", "Keyboard")) }) options.Children.Add(SmallChoice(name, () => Update(s => s.Action = id, true), Config.Action == id));
        action.Children.Add(options);
        if (Config.Action == "key") Row(action, Text(T("Taste", "Key"), 12, true), Btn(KeyLabel(Config.ActionKey), () => Capture((s, v) => s.ActionKey = v, true)), 10);
        var hotkey = Card(columns.Right, "Hotkey");
        var keycap = Btn(KeyLabel(Config.Trigger), () => Capture((s, v) => s.Trigger = v, false)); keycap.Width = 72; keycap.HorizontalAlignment = HorizontalAlignment.Left;
        var change = Btn(T("Ändern", "Change"), () => Capture((s, v) => s.Trigger = v, false)); change.Background = Brushes.Transparent; change.BorderThickness = new Thickness(0);
        Row(hotkey, keycap, change, 16);
        var activation = Card(p, ""); var modes = new StackPanel { Orientation = Orientation.Horizontal };
        modes.Children.Add(SmallChoice(T("Umschalten", "Toggle"), () => Update(s => s.Hold = false, true), !Config.Hold));
        modes.Children.Add(SmallChoice(T("Gedrückt halten", "Hold to activate"), () => Update(s => s.Hold = true, true), Config.Hold));
        Row(activation, Text(T("Aktivierung", "Activation"), 14, bold: true), modes, 0);
        var footer = new StackPanel(); var status = new StackPanel(); stateLabel = Text("", 12, true); status.Children.Add(stateLabel); rateLabel = Text("", 10, true); rateLabel.Margin = new Thickness(0, 4, 0, 0); status.Children.Add(rateLabel);
        startButton = Btn("", () => { engine?.Toggle(); Refresh(); }, true); startButton.MinWidth = 105;
        Row(footer, status, startButton, 0); footerHost!.Child = footer; footerHost.BorderBrush = Brush("#292D35"); footerHost.BorderThickness = new Thickness(0, 1, 0, 0); footerHost.Padding = new Thickness(0, 12, 0, 0); footerHost.Margin = new Thickness(0, 5, 0, 0);
    }
    void CompactProfiles(StackPanel p)
    {
        var top = Row(p, Text(T("Profile", "Profiles"), 25, bold: true), Btn(T("+ Neu", "+ New"), () => { creatingProfile = true; draftProfileName = ""; featureNotice = ""; ShowPage(3); }), 0); top.Margin = new Thickness(0, 0, 0, 18);
        Notice(p);
        if (profiles.Error != "") { p.Children.Add(Text(profiles.Error)); return; }
        if (profiles.Items.Count == 0) creatingProfile = true;
        if (!profiles.Items.Any(x => x.Id == selectedProfile)) selectedProfile = profiles.Items.FirstOrDefault(x => x.Id == Config.ActiveProfileId)?.Id ?? profiles.Items.FirstOrDefault()?.Id ?? "";
        var cols = Columns(p, .85, 1.7);
        var list = new StackPanel();
        foreach (var profile in profiles.Items) {
            var b = Btn(profile.Name, () => { selectedProfile = profile.Id; creatingProfile = false; featureNotice = ""; ShowPage(3); });
            var name = Text(profile.Name, 12, bold: selectedProfile == profile.Id && !creatingProfile); name.TextTrimming = TextTrimming.CharacterEllipsis; name.TextWrapping = TextWrapping.NoWrap; b.Content = name; b.ToolTip = profile.Name;
            b.Background = profile.Id == selectedProfile && !creatingProfile ? (Brush)Resources["AccentTint"] : Brush("#111318"); b.Padding = new Thickness(11, 12, 11, 12); b.Margin = new Thickness(0, 0, 0, 8); list.Children.Add(b);
        }
        if (profiles.Items.Count == 0) list.Children.Add(Text(T("Noch keine Profile", "No profiles yet"), 12, true));
        cols.Left.Children.Add(new ScrollViewer { Content = list, MaxHeight = 300, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
        if (creatingProfile) {
            var create = Card(cols.Right, T("Neues Profil", "New profile"));
            var note = Text(T("Speichert deine aktuellen Makro- und Roblox-Einstellungen.", "Saves your current macro and Roblox settings."), 12, true); note.Margin = new Thickness(0, 10, 0, 14); create.Children.Add(note);
            var name = new TextBox { Text = draftProfileName, MaxLength = 40, FontSize = 14 }; Accessible(name, T("Profilname", "Profile name")); create.Children.Add(name); name.TextChanged += (_, _) => draftProfileName = name.Text;
            Row(create, Text(T("1–40 Zeichen", "1–40 characters"), 11, true), Btn(T("Erstellen", "Create"), () => Attempt(() => CreateProfile(name.Text)), true), 15);
        } else {
            var profile = profiles.Items.First(x => x.Id == selectedProfile); var details = Card(cols.Right, profile.Name);
            Row(details, Text(T("Geschwindigkeit", "Cadence"), 12, true), Text(profile.Macro.Aps + " APS", 17, bold: true));
            Row(details, Text(T("Aktion", "Action"), 12, true), Text(profile.Macro.Action == "key" ? Bindings.Name(profile.Macro.ActionKey) : profile.Macro.Action == "left" ? T("Linksklick", "Left click") : T("Rechtsklick", "Right click"), 13));
            Row(details, Text("Hotkey", 12, true), Text(Bindings.Name(profile.Macro.Trigger), 13));
            Row(details, Text(T("Modus", "Mode"), 12, true), Text(profile.Macro.Hold ? T("Halten", "Hold") : T("Umschalten", "Toggle"), 13));
            var buttons = new WrapPanel { Margin = new Thickness(0, 22, 0, 0) };
            var load = Btn(T("Laden", "Load"), () => Attempt(() => LoadProfile(profile.Id))); load.Margin = new Thickness(0, 0, 8, 0); buttons.Children.Add(load);
            buttons.Children.Add(Btn(T("Speichern", "Save"), () => Attempt(() => SaveProfile(profile.Id)), true)); details.Children.Add(buttons);
            var hint = Text(T("Speichern übernimmt deine aktuellen Makro-Werte.", "Save uses your current macro settings."), 11, true); hint.Margin = new Thickness(0, 12, 0, 0); details.Children.Add(hint);
        }
        var current = Text(T("Aktuell: ", "Current: ") + PresetSummary(MacroPreset.From(Config)), 11, true); current.Margin = new Thickness(0, 8, 0, 0); p.Children.Add(current);
    }
    void CompactSettings(StackPanel p, int section)
    {
        var title = Text(section == 10 ? T("Design", "Appearance") : section == 12 ? T("Hotkeys & Verhalten", "Hotkeys & behavior") : T("Einstellungen", "Settings"), 25, bold: true); title.Margin = new Thickness(0, 0, 0, 18); p.Children.Add(title);
        if (section == 2) {
            var language = Card(p, ""); var langs = new StackPanel { Orientation = Orientation.Horizontal };
            langs.Children.Add(SmallChoice("Deutsch", () => Update(s => s.Language = "de", true), Config.Language == "de")); langs.Children.Add(SmallChoice("English", () => Update(s => s.Language = "en", true), Config.Language == "en"));
            Row(language, Text(T("Sprache", "Language"), 14, bold: true), langs, 0);
            var app = Card(p, "Application Settings");
            Switch(app, T("Mit Windows starten", "Run on Windows startup"), StartupEnabled(), SetStartup);
            Switch(app, T("In den Infobereich minimieren", "Minimize to system tray"), Config.Tray, v => Update(s => s.Tray = v));
            Switch(app, T("Signalton beim Umschalten", "Play beep on toggle"), Config.Beep, v => Update(s => s.Beep = v));
            Switch(app, T("Immer im Vordergrund", "Always on top"), Config.Topmost, v => Update(s => s.Topmost = v));
        } else if (section == 10) {
            var appearance = Card(p, T("Akzentfarbe", "Accent color")); var colors = new WrapPanel { Margin = new Thickness(0, 18, 0, 0) }; appearance.Children.Add(colors);
            string[] hex = { "#FF1838", "#24D989", "#358BFF", "#AD64FF", "#FF9F32", "#EEEEF4" };
            string[] names = { T("Rot", "Red"), T("Grün", "Green"), T("Blau", "Blue"), T("Lila", "Purple"), "Orange", T("Weiß", "White") };
            for (int i = 0; i < hex.Length; i++) { string h = hex[i]; var b = Btn(h == Config.Accent ? "✓" : "", () => Update(s => s.Accent = h, true)); b.Width = 38; b.Height = 38; b.Padding = new Thickness(0); b.Background = Brush(h); b.Foreground = Brush("#080A0D"); b.FontSize = 19; b.ToolTip = names[i]; Accessible(b, names[i]); b.Margin = new Thickness(0, 0, 12, 0); colors.Children.Add(b); }
            Percent(appearance, T("Dezentes Status-Leuchten", "Subtle status glow"), Config.Glow, 0, v => Update(s => s.Glow = v));
            var hint = Text(T("Die Akzentfarbe gilt für aktive Schalter und den Statuspunkt im Overlay.", "The accent color applies to active controls and the overlay status dot."), 12, true); p.Children.Add(hint);
        } else {
            var keys = Card(p, T("Tasten & Timing", "Keys & timing"));
            Bind(keys, T("Start / Stopp", "Start / stop"), Config.Trigger, (s, v) => s.Trigger = v);
            Bind(keys, T("Not-Aus", "Emergency stop"), Config.Emergency, (s, v) => s.Emergency = v);
            Switch(keys, T("Klickintervalle variieren (±15 %)", "Randomize click intervals (±15%)"), Config.Randomize, v => Update(s => s.Randomize = v));
        }
        saveLabel = Text(T("Automatisch gespeichert · 4sibi 1.2", "Saved automatically · 4sibi 1.2"), 10, true); p.Children.Add(saveLabel);
    }
    void CompactOverlay(StackPanel p, int section)
    {
        Heading(p, "Overlay", section == 8 ? T("Minimal Pill · nur die Informationen, die du brauchst.", "Minimal Pill · just the information you need.") : T("Platzieren und gegen versehentliches Verschieben sperren.", "Position and lock your overlay."));
        if (section == 11) {
            var pos = Card(p, T("Position", "Position"));
            Switch(pos, T("Position sperren", "Lock position"), Config.OverlayLocked, v => Update(s => s.OverlayLocked = v));
            var choices = new WrapPanel { Margin = new Thickness(0, 20, 0, 0) }; pos.Children.Add(choices);
            foreach (var (id, label) in new[] { ("tl", T("Oben links", "Top left")), ("tr", T("Oben rechts", "Top right")), ("bl", T("Unten links", "Bottom left")), ("br", T("Unten rechts", "Bottom right")) }) { var b = Btn(label, () => { if (!Config.Overlay) Update(s => s.Overlay = true); overlay?.Place(id); }); b.Margin = new Thickness(0, 0, 8, 8); choices.Children.Add(b); }
            var hint = Text(T("Ziehe die Pille mit der Maus. Per Rechtsklick erreichst du Sperre, Einstellungen und Ausblenden.", "Drag the pill to move it. Right-click for locking, settings and hiding."), 12, true); p.Children.Add(hint); return;
        }
        var cols = Columns(p); var display = Card(cols.Left, T("Anzeige", "Display"));
        Switch(display, T("Overlay anzeigen", "Show overlay"), Config.Overlay, v => Update(s => s.Overlay = v));
        var value = Text($"{Config.OverlayScale:P0}", 12, true); Row(display, Text(T("Größe", "Size"), 13), value);
        var slider = new Slider { Minimum = .75, Maximum = 1.5, Value = Config.OverlayScale, TickFrequency = .05, IsSnapToTickEnabled = true, Margin = new Thickness(0, 7, 0, 0) }; Accessible(slider, T("Overlay-Größe", "Overlay size")); display.Children.Add(slider);
        slider.ValueChanged += (_, _) => { value.Text = $"{slider.Value:P0}"; Update(s => s.OverlayScale = slider.Value); };
        Percent(display, T("Deckkraft", "Opacity"), Config.OverlayOpacity, .3, v => Update(s => s.OverlayOpacity = v));
        var info = Card(cols.Right, T("Informationen", "Information"));
        Switch(info, "APS", Config.OverlayShowAps, v => Update(s => s.OverlayShowAps = v));
        Switch(info, "Hotkey", Config.OverlayShowHotkey, v => Update(s => s.OverlayShowHotkey = v));
        Switch(info, T("Aktives Profil", "Active profile"), Config.OverlayShowProfile, v => Update(s => s.OverlayShowProfile = v));
        var hint2 = Text(T("Der Status bleibt immer sichtbar. Weitere Infos verbreitern die Pille automatisch.", "Status always stays visible. Extra information widens the pill automatically."), 12, true); p.Children.Add(hint2);
    }
}
