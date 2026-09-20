using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace Sibi;

public sealed partial class MainWindow : Window
{
    public Settings Config;
    public readonly bool Testing;
    readonly Engine? engine;
    readonly DispatcherTimer refresh = new() { Interval = TimeSpan.FromMilliseconds(150) };
    readonly DispatcherTimer saveTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    Forms.NotifyIcon? tray;
    OverlayWindow? overlay;
    Grid shell = new();
    Border? frameBorder;
    bool capturing;
    Border pageHost = new();
    Border? footerHost;
    readonly Dictionary<int, Button> nav = new();
    TextBlock? stateLabel, rateLabel, clientLabel, focusLabel, saveLabel;
    Button? startButton;
    StackPanel? clientList;
    int page, refreshTicks;
    string previousStatus = "paused";
    public string SaveError = "";
    public string T(string de, string en) => Config.Language == "en" ? en : de;
    public string StatusText(string status) => status switch {
        "running" => T("Aktiv", "Active"), "waiting" => T("Bereit", "Ready"),
        "error" => T("Eingabe blockiert", "Input blocked"), _ => T("Pausiert", "Paused") };
    public string KeyLabel(int vk) => Bindings.Name(vk);

    public MainWindow(bool testing = false, string? dataDirectory = null)
    {
        Testing = testing; Config = testing ? new() : Settings.Load();
        profiles = new ProfileStore(testing ? dataDirectory ?? Path.Combine(Path.GetTempPath(), "4sibi-test-" + Guid.NewGuid()) : Settings.DirectoryPath);
        Title = "4sibi"; Width = 820; Height = 610; MinWidth = 800; MinHeight = 580;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.CanResize;
        Background = Brush("#0B0C0F"); Foreground = Brush("#F1F2F5");
        FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI"); FontSize = 13;
        // Match the top inset plus the header row. Windows handles drag, snap,
        // double-click maximize and restore-from-maximized for this region.
        System.Windows.Shell.WindowChrome.SetWindowChrome(this, new System.Windows.Shell.WindowChrome { CaptionHeight = 54, ResizeBorderThickness = new Thickness(6), CornerRadius = new CornerRadius(12), GlassFrameThickness = new Thickness(0) });
        LoadStyles(); ApplyTheme(); BuildShell();
        if (!testing) {
            engine = new Engine(Config);
            SetupTray();
            refresh.Tick += (_, _) => Refresh(); refresh.Start();
            saveTimer.Tick += (_, _) => { saveTimer.Stop(); SaveNow(); };
            Loaded += (_, _) => { SyncOverlay(); Refresh(); };
        }
        StateChanged += (_, _) => { if (WindowState == WindowState.Minimized && Config.Tray && !Testing) Hide(); };
        Closing += (_, _) => { refresh.Stop(); saveTimer.Stop(); engine?.Stop(); engine?.Dispose(); overlay?.Close(); tray?.Dispose(); if (!Testing) SaveNow(); };
        Topmost = Config.Topmost;
    }
    public static SolidColorBrush Brush(string hex) => new((Color)ColorConverter.ConvertFromString(hex));
    void LoadStyles()
    {
        const string xaml = """
        <ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
          <Style TargetType="TextBlock"><Setter Property="Foreground" Value="#F1F2F5"/><Setter Property="TextWrapping" Value="Wrap"/></Style>
          <Style TargetType="Button">
            <Setter Property="Foreground" Value="#F1F2F5"/><Setter Property="Background" Value="#17191F"/><Setter Property="BorderBrush" Value="#363A44"/><Setter Property="BorderThickness" Value="1"/><Setter Property="Padding" Value="18,11"/><Setter Property="Cursor" Value="Hand"/><Setter Property="FontSize" Value="14"/>
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Button"><Border x:Name="b" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}" CornerRadius="8" Padding="{TemplateBinding Padding}"><ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/></Border><ControlTemplate.Triggers><Trigger Property="IsMouseOver" Value="True"><Setter TargetName="b" Property="BorderBrush" Value="{DynamicResource Accent}"/></Trigger><Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="b" Property="BorderBrush" Value="{DynamicResource Accent}"/><Setter TargetName="b" Property="BorderThickness" Value="2"/></Trigger><Trigger Property="IsPressed" Value="True"><Setter TargetName="b" Property="Opacity" Value="0.7"/></Trigger><Trigger Property="IsEnabled" Value="False"><Setter TargetName="b" Property="Opacity" Value="0.38"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
          </Style>
          <Style TargetType="CheckBox">
            <Setter Property="Cursor" Value="Hand"/><Setter Property="Focusable" Value="True"/>
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="CheckBox"><Border x:Name="track" Width="46" Height="26" CornerRadius="13" Background="#353943" BorderBrush="#50545E" BorderThickness="1"><Ellipse x:Name="knob" Width="18" Height="18" Fill="#F5F5FA" HorizontalAlignment="Left" Margin="3"/></Border><ControlTemplate.Triggers><Trigger Property="IsChecked" Value="True"><Setter TargetName="track" Property="Background" Value="{DynamicResource Accent}"/><Setter TargetName="track" Property="BorderBrush" Value="{DynamicResource Accent}"/><Setter TargetName="knob" Property="HorizontalAlignment" Value="Right"/></Trigger><Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="track" Property="BorderBrush" Value="White"/><Setter TargetName="track" Property="BorderThickness" Value="2"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
          </Style>
          <Style TargetType="TextBox"><Setter Property="Background" Value="#0E1015"/><Setter Property="Foreground" Value="#F1F2F5"/><Setter Property="CaretBrush" Value="#F1F2F5"/><Setter Property="BorderBrush" Value="#414650"/><Setter Property="Padding" Value="12,9"/><Setter Property="FontSize" Value="18"/><Setter Property="VerticalContentAlignment" Value="Center"/></Style>
          <Style TargetType="ScrollBar">
            <Setter Property="Width" Value="8"/><Setter Property="Background" Value="Transparent"/>
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ScrollBar"><Grid Background="Transparent"><Track x:Name="PART_Track" Orientation="Vertical" IsDirectionReversed="True" Minimum="{TemplateBinding Minimum}" Maximum="{TemplateBinding Maximum}" Value="{TemplateBinding Value}" ViewportSize="{TemplateBinding ViewportSize}"><Track.DecreaseRepeatButton><RepeatButton Command="ScrollBar.PageUpCommand" Opacity="0" Focusable="False"/></Track.DecreaseRepeatButton><Track.Thumb><Thumb><Thumb.Template><ControlTemplate TargetType="Thumb"><Border Background="#404550" CornerRadius="4" Margin="1,0"/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb><Track.IncreaseRepeatButton><RepeatButton Command="ScrollBar.PageDownCommand" Opacity="0" Focusable="False"/></Track.IncreaseRepeatButton></Track></Grid></ControlTemplate></Setter.Value></Setter>
          </Style>
          <Style TargetType="Slider">
            <Setter Property="Height" Value="30"/><Setter Property="Minimum" Value="0"/><Setter Property="Maximum" Value="100"/>
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Slider"><Grid VerticalAlignment="Center"><Border Height="5" CornerRadius="2" Background="#3B3F4A"/><Track x:Name="PART_Track"><Track.DecreaseRepeatButton><RepeatButton Command="Slider.DecreaseLarge" Focusable="False"><RepeatButton.Template><ControlTemplate TargetType="RepeatButton"><Border Height="5" Background="{DynamicResource Accent}" CornerRadius="2"/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.DecreaseRepeatButton><Track.Thumb><Thumb Width="20" Height="20"><Thumb.Template><ControlTemplate TargetType="Thumb"><Ellipse Fill="{DynamicResource Accent}" Stroke="#F4F5F8" StrokeThickness="2"/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb><Track.IncreaseRepeatButton><RepeatButton Command="Slider.IncreaseLarge" Focusable="False"><RepeatButton.Template><ControlTemplate TargetType="RepeatButton"><Border Background="Transparent" Height="20"/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.IncreaseRepeatButton></Track></Grid></ControlTemplate></Setter.Value></Setter>
          </Style>
        </ResourceDictionary>
        """;
        Resources.MergedDictionaries.Add((ResourceDictionary)System.Windows.Markup.XamlReader.Parse(xaml));
    }
    public void ApplyTheme()
    {
        Resources["Accent"] = Brush(Config.Accent);
        Resources["AccentTint"] = new SolidColorBrush(Color.FromArgb(24, Brush(Config.Accent).Color.R, Brush(Config.Accent).Color.G, Brush(Config.Accent).Color.B));
        Topmost = Config.Topmost;
        if (frameBorder != null) {
            frameBorder.BorderBrush = Brush("#30343D");
            frameBorder.Effect = null;
        }
        overlay?.ApplyTheme();
    }
    public TextBlock Text(string text, double size = 14, bool muted = false, bool bold = false)
        => new() { Text = text, FontSize = size, Foreground = Brush(muted ? "#A4AAB8" : "#F1F2F5"), FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal, VerticalAlignment = VerticalAlignment.Center };
    public Button Btn(string text, Action action, bool accent = false)
    {
        var b = new Button { Content = text, MinHeight = 36, Padding = new Thickness(13, 8, 13, 8), FontSize = 12 };
        if (accent) { b.SetResourceReference(BackgroundProperty, "Accent"); var c = Brush(Config.Accent).Color; b.Foreground = .2126*c.R + .7152*c.G + .0722*c.B > 160 ? Brush("#080A0D") : Brushes.White; b.FontWeight = FontWeights.SemiBold; b.BorderThickness = new Thickness(0); }
        b.Click += (_, _) => action(); return b;
    }
    static void Accessible(DependencyObject element, string name) => System.Windows.Automation.AutomationProperties.SetName(element, name);
    public void BuildShell()
    {
        shell = new Grid(); shell.RowDefinitions.Add(new() { Height = new GridLength(48) }); shell.RowDefinitions.Add(new());
        var outside = new Border { BorderBrush = Brush("#343843"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Background = Background, Child = shell, Margin = new Thickness(5) };
        frameBorder = outside; ApplyTheme();
        Content = outside;
        var top = new Grid { Margin = new Thickness(18, 0, 8, 0), Background = Brushes.Transparent };
        top.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); top.ColumnDefinitions.Add(new()); top.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        Icon = Branding.Source("taskbar.png");
        var brand = new StackPanel { Orientation = Orientation.Horizontal };
        brand.Children.Add(Branding.Logo(28));
        var logo = Text("4sibi", 19, bold: true); logo.Margin = new Thickness(9, 0, 0, 0); brand.Children.Add(logo); top.Children.Add(brand);
        var controls = new StackPanel { Orientation = Orientation.Horizontal };
        var min = Btn("−", () => WindowState = WindowState.Minimized); min.Background = Brushes.Transparent; min.BorderThickness = new Thickness(0); Accessible(min, T("Minimieren", "Minimize"));
        var close = Btn("×", Close); close.Background = Brushes.Transparent; close.BorderThickness = new Thickness(0); close.FontSize = 23; Accessible(close, T("Beenden", "Quit"));
        System.Windows.Shell.WindowChrome.SetIsHitTestVisibleInChrome(min, true);
        System.Windows.Shell.WindowChrome.SetIsHitTestVisibleInChrome(close, true);
        controls.Children.Add(min); controls.Children.Add(close); Grid.SetColumn(controls, 2); top.Children.Add(controls);
        shell.Children.Add(top);
        var body = new Grid(); body.ColumnDefinitions.Add(new() { Width = new GridLength(62) }); body.ColumnDefinitions.Add(new()); Grid.SetRow(body, 1); shell.Children.Add(body);
        var watermark = new Image { Source = Branding.Source("4sibi-hintergrund.png"), Opacity = .09, Stretch = Stretch.UniformToFill, IsHitTestVisible = false }; Grid.SetColumn(watermark, 1); body.Children.Add(watermark); body.ClipToBounds = true;
        var side = new DockPanel { Background = Brush("#101115"), LastChildFill = true };
        var links = new StackPanel { Margin = new Thickness(7, 16, 7, 0) }; nav.Clear();
        CompactNavigation(links);
        side.Children.Add(new ScrollViewer { Content = links, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }); body.Children.Add(side);
        pageHost = new Border { BorderBrush = Brush("#252831"), BorderThickness = new Thickness(1, 1, 0, 0), Padding = new Thickness(22, 20, 18, 16) }; Grid.SetColumn(pageHost, 1); body.Children.Add(pageHost);
        ShowPage(page);
    }
    public void ShowPage(int index)
    {
        if (index == 4) { creatingProfile = true; index = 3; }
        if (index == 5) { creatingProfile = false; selectedProfile = Config.ActiveProfileId; index = 3; }
        page = index; stateLabel = rateLabel = clientLabel = focusLabel = saveLabel = null; clientList = null; startButton = null;
        int rootPage = index is 1 or 9 or 10 or 12 ? 2 : index == 7 ? 6 : index == 11 ? 8 : index;
        foreach (var entry in nav) { bool active = entry.Key == rootPage; entry.Value.Background = active ? (Brush)Resources["AccentTint"] : Brushes.Transparent; entry.Value.BorderThickness = new Thickness(0); if (entry.Value.Content is TextBlock icon) icon.Foreground = active ? (Brush)Resources["Accent"] : Brush("#B3B8C4"); }
        var content = new StackPanel();
        var layout = new Grid(); layout.RowDefinitions.Add(new()); layout.RowDefinitions.Add(new() { Height = GridLength.Auto });
        var scroller = new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Padding = new Thickness(0, 0, 8, 0) };
        layout.Children.Add(scroller);
        footerHost = new Border(); Grid.SetRow(footerHost, 1); layout.Children.Add(footerHost); pageHost.Child = layout;
        CompactTabs(content, index);
        if (index == 0) CompactMacro(content);
        else if (index == 1) GamePage(content);
        else if (index is 2 or 10 or 12) CompactSettings(content, index);
        else if (index == 3) CompactProfiles(content);
        else if (index is 8 or 11) CompactOverlay(content, index);
        else FeaturePage(content, index);
        Refresh();
    }
    void Heading(StackPanel p, string title, string subtitle)
    {
        p.Children.Add(Text(title, 25, bold: true)); var s = Text(subtitle, 12, true); s.Margin = new Thickness(0, 5, 0, 18); p.Children.Add(s);
    }
    StackPanel Card(Panel parent, string title, string? hint = null)
    {
        var stack = new StackPanel(); var card = new Border { Background = Brush("#111318"), BorderBrush = Brush("#292D35"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(9), Padding = new Thickness(17), Margin = new Thickness(0, 0, 0, 14), Child = stack };
        parent.Children.Add(card);
        if (title != "") stack.Children.Add(Text(title, 16, bold: true));
        if (hint != null) { var t = Text(hint, 12, true); t.Margin = new Thickness(0, 5, 0, 0); stack.Children.Add(t); }
        return stack;
    }
    Grid Row(Panel parent, UIElement left, UIElement right, double top = 18)
    {
        var g = new Grid { Margin = new Thickness(0, top, 0, 0) };
        g.ColumnDefinitions.Add(new()); g.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        if (left is FrameworkElement l) l.Margin = new Thickness(0, 0, 16, 0);
        g.Children.Add(left); Grid.SetColumn(right, 1); g.Children.Add(right); parent.Children.Add(g); return g;
    }
    void Line(Panel p) => p.Children.Add(new Border { Height = 1, Background = Brush("#2B2F38"), Margin = new Thickness(0, 19, 0, 0) });
    CheckBox Switch(Panel p, string label, bool value, Action<bool> set, string? hint = null)
    {
        var description = new StackPanel(); description.Children.Add(Text(label));
        if (hint != null) { var h = Text(hint, 12, true); h.Margin = new Thickness(0, 4, 0, 0); description.Children.Add(h); }
        var c = new CheckBox { IsChecked = value, VerticalAlignment = VerticalAlignment.Center, ToolTip = label }; Accessible(c, label);
        c.Checked += (_, _) => set(true); c.Unchecked += (_, _) => set(false); Row(p, description, c); return c;
    }
    public void Update(Action<Settings> change, bool rebuild = false)
    {
        var next = Config with { }; change(next);
        if (Bindings.Conflict(next)) { ShowMessage(T("Start/Stopp, Aktion und Not-Aus müssen verschiedene Tasten verwenden.", "Trigger, action and emergency stop must use different keys.")); return; }
        Config = next; engine?.Configure(Config); ApplyTheme();
        if (rebuild) { BuildShell(); SetupTrayMenu(); }
        SyncOverlay(); SaveSoon();
    }
    void SaveSoon() { if (!Testing) { saveTimer.Stop(); saveTimer.Start(); } }
    void SaveNow()
    {
        try { Config.Save(); SaveError = ""; }
        catch (Exception ex) { SaveError = ex.Message; }
        if (saveLabel != null) saveLabel.Text = SaveError == "" ? T("Änderungen automatisch gespeichert", "Changes saved automatically") : T("Speichern fehlgeschlagen: ", "Could not save: ") + SaveError;
    }
    void ShowMessage(string text) { if (!Testing) MessageBox.Show(this, text, "4sibi", MessageBoxButton.OK, MessageBoxImage.Information); }
    void Bind(Panel p, string title, int key, Action<Settings, int> setter, bool keyboardOnly = false)
    {
        var right = new StackPanel { Orientation = Orientation.Horizontal };
        var cap = Btn(KeyLabel(key), () => Capture(setter, keyboardOnly)); cap.MinWidth = 88; Accessible(cap, title); right.Children.Add(cap);
        var edit = Btn(T("Ändern", "Change"), () => Capture(setter, keyboardOnly)); edit.Margin = new Thickness(9, 0, 0, 0); right.Children.Add(edit);
        Row(p, Text(title), right);
    }
    void Capture(Action<Settings, int> setter, bool keyboardOnly)
    {
        engine?.Stop(); capturing = true; if (engine != null) engine.Suspended = true;
        try { var dialog = new CaptureWindow(this, keyboardOnly); if (dialog.ShowDialog() == true) Update(s => setter(s, dialog.CapturedKey), true); }
        finally { capturing = false; if (engine != null) engine.Suspended = false; }
    }
    void GamePage(StackPanel p)
    {
        Heading(p, T("Spielmodus", "Game mode"), T("Roblox-Client erkennen und Eingaben an das aktive Fenster binden.", "Detect the Roblox client and target the active window."));
        var clients = Card(p, T("Offene Clients", "Open clients"), T("Wird automatisch alle zwei Sekunden aktualisiert.", "Refreshes automatically every two seconds."));
        clientList = new StackPanel { Margin = new Thickness(0, 15, 0, 0) }; clients.Children.Add(clientList);
        var rescan = Btn(T("↻  Aktualisieren", "↻  Refresh"), UpdateClients); rescan.HorizontalAlignment = HorizontalAlignment.Right; rescan.Margin = new Thickness(0, 14, 0, 0); clients.Children.Add(rescan);
        var game = Card(p, T("Roblox-Modus", "Roblox mode"));
        Switch(game, T("Nur im Roblox-Client ausführen", "Run only in the Roblox client"), Config.RobloxOnly, v => Update(s => s.RobloxOnly = v), T("Erkennt den Client automatisch; der Browser zählt nicht.", "Automatically detects the desktop client; the browser does not count."));
        Line(game);
        Switch(game, T("Bei Fensterwechsel stoppen", "Stop when switching windows"), Config.PauseOnFocusLoss, v => Update(s => s.PauseOnFocusLoss = v), T("Danach erneut mit deinem Hotkey starten.", "Use your hotkey to start again afterwards."));
        var hint = Text(T("Im Roblox-Modus wird außerhalb von Roblox immer pausiert. 4sibi selbst erhält keine Makro-Eingaben.", "Roblox mode always pauses outside Roblox. 4sibi itself never receives macro inputs."), 12, true); hint.Margin = new Thickness(0, 20, 0, 0); game.Children.Add(hint);
        focusLabel = Text("", 14); focusLabel.Margin = new Thickness(0, 21, 0, 0); game.Children.Add(focusLabel);
        clientLabel = Text("", 12, true); p.Children.Add(clientLabel); UpdateClients();
    }
    void UpdateClients()
    {
        if (clientList == null) return; clientList.Children.Clear();
        var clients = Testing ? new List<(int Id, string Name)>() : Native.RobloxClients();
        if (clients.Count == 0) {
            clientList.Children.Add(Text(T("Kein Roblox-Client geöffnet", "No Roblox client open"), 16, bold: true));
            var hint = Text(T("Starte Roblox. Dein Client erscheint hier automatisch.", "Launch Roblox. Your client will appear here automatically."), 13, true); hint.Margin = new Thickness(0, 8, 0, 8); clientList.Children.Add(hint);
        } else foreach (var item in clients) {
            var labels = new StackPanel(); labels.Children.Add(Text("Roblox", 17, bold: true)); labels.Children.Add(Text($"{item.Name}.exe · PID {item.Id}", 12, true));
            var status = Text(T("●  Client erkannt", "●  Client detected"), 13); status.SetResourceReference(TextBlock.ForegroundProperty, "Accent"); Row(clientList, labels, status, 8);
        }
    }
    void Percent(Panel parent, string label, double value, double min, Action<double> action)
    {
        var amount = Text($"{value:P0}", 12, true); Row(parent, Text(label, 13), amount, 15);
        var slider = new Slider { Minimum = min, Maximum = 1, Value = value, SmallChange = .05, LargeChange = .1, Margin = new Thickness(0, 7, 0, 0) }; Accessible(slider, label); parent.Children.Add(slider);
        slider.ValueChanged += (_, _) => { amount.Text = $"{slider.Value:P0}"; action(slider.Value); };
    }
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    bool StartupEnabled() { if (Testing) return false; try { using var k = Registry.CurrentUser.OpenSubKey(RunKey); return k?.GetValue("4sibi") is string; } catch { return false; } }
    void SetStartup(bool on)
    {
        if (Testing) return;
        try { using var k = Registry.CurrentUser.CreateSubKey(RunKey); if (on) k.SetValue("4sibi", "\"" + Environment.ProcessPath + "\" --tray"); else k.DeleteValue("4sibi", false); }
        catch (Exception ex) { ShowMessage(T("Autostart konnte nicht geändert werden: ", "Could not change startup: ") + ex.Message); ShowPage(page); }
    }
    void SetupTray()
    {
        tray = new Forms.NotifyIcon { Icon = Branding.TrayIcon, Text = "4sibi", Visible = true };
        tray.DoubleClick += (_, _) => Dispatcher.Invoke(Restore); SetupTrayMenu();
    }
    void SetupTrayMenu()
    {
        if (tray == null) return;
        var old = tray.ContextMenuStrip; tray.ContextMenuStrip = CreateTrayMenu(); old?.Dispose();
    }
    internal Forms.ContextMenuStrip CreateTrayMenu()
    {
        var menu = new Forms.ContextMenuStrip { Renderer = new DarkTrayRenderer(), BackColor = System.Drawing.Color.FromArgb(19, 21, 26), ForeColor = System.Drawing.Color.FromArgb(232, 234, 239), ShowImageMargin = false, ShowCheckMargin = false, Padding = new Forms.Padding(6), Font = new System.Drawing.Font("Segoe UI", 10) };
        menu.Items.Add(T("4sibi öffnen", "Open 4sibi"), null, (_, _) => Dispatcher.Invoke(Restore));
        menu.Items.Add(T("Overlay anpassen", "Customize overlay"), null, (_, _) => Dispatcher.Invoke(() => { Restore(); ShowPage(8); }));
        menu.Items.Add(T("Einstellungen", "Settings"), null, (_, _) => Dispatcher.Invoke(() => { Restore(); ShowPage(2); }));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(T("Makro stoppen", "Stop macro"), null, (_, _) => engine?.Stop());
        menu.Items.Add(T("Beenden", "Quit"), null, (_, _) => Dispatcher.Invoke(Close));
        foreach (Forms.ToolStripItem item in menu.Items) if (item is Forms.ToolStripMenuItem) item.Padding = new Forms.Padding(12, 8, 16, 8);
        return menu;
    }
    public void Restore() { Show(); WindowState = WindowState.Normal; Activate(); }
    void SyncOverlay()
    {
        if (Testing || !IsLoaded) return;
        if (Config.Overlay) { overlay ??= new OverlayWindow(this); overlay.ApplyTheme(); if (!overlay.IsVisible) overlay.Show(); }
        else overlay?.Hide();
    }
    public void SaveOverlayPosition(double x, double y) { Config.OverlayX = x; Config.OverlayY = y; SaveSoon(); }
    public void HideOverlay() { Update(s => s.Overlay = false, true); }
    public void StopMacro() { engine?.Stop(); Refresh(); }
    void Refresh()
    {
        string status = engine?.Status ?? "paused";
        if (Config.Beep && previousStatus != status && status is "running" or "paused" && !Testing) System.Media.SystemSounds.Asterisk.Play();
        previousStatus = status;
        if (stateLabel != null) stateLabel.Text = "●  " + StatusText(status);
        if (rateLabel != null) rateLabel.Text = T($"Not-Aus: {KeyLabel(Config.Emergency)}  ·  Ist: {engine?.ActualAps ?? 0:0} APS", $"Stop: {KeyLabel(Config.Emergency)}  ·  Actual: {engine?.ActualAps ?? 0:0} APS");
        if (startButton != null) { startButton.Content = status is "running" or "waiting" ? T("Stoppen", "Stop") : T("Starten", "Start"); startButton.IsEnabled = !Config.Hold; startButton.ToolTip = Config.Hold ? T("Zum Starten den gewählten Hotkey gedrückt halten.", "Hold your selected trigger to start.") : null; }
        if (focusLabel != null) focusLabel.Text = engine?.RobloxForeground == true ? T("●  Roblox im Vordergrund", "●  Roblox in foreground") : T("○  Roblox nicht im Vordergrund", "○  Roblox not in foreground");
        if (clientLabel != null) clientLabel.Text = T("Makro: ", "Macro: ") + StatusText(status);
        overlay?.UpdateStatus(status);
        if (++refreshTicks % 14 == 0) UpdateClients();
        // Suppress global activation while typing into the APS field.
        if (engine != null) engine.Suspended = capturing || (IsActive && Keyboard.FocusedElement is TextBox);
    }
}

public sealed class CaptureWindow : Window
{
    public int CapturedKey;
    public CaptureWindow(MainWindow owner, bool keyboardOnly)
    {
        Owner = owner; Title = "4sibi"; Width = 490; Height = 215; ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = MainWindow.Brush("#14161B"); Foreground = Brushes.White; FontFamily = owner.FontFamily;
        var panel = new StackPanel { Margin = new Thickness(27) }; Content = panel;
        panel.Children.Add(owner.Text(owner.T("Neue Taste drücken", "Press a new key"), 23, bold: true));
        var hint = owner.Text(keyboardOnly ? owner.T("Eine Tastaturtaste drücken. Esc bricht ab.", "Press a keyboard key. Esc cancels.") : owner.T("Tastatur oder Maustaste drücken. Esc bricht ab.", "Press a keyboard or mouse button. Esc cancels."), 14, true); hint.Margin = new Thickness(0, 15, 0, 0); panel.Children.Add(hint);
        PreviewKeyDown += (_, e) => { e.Handled = true; if (e.Key == Key.Escape) { DialogResult = false; return; } var k = e.Key == Key.System ? e.SystemKey : e.Key; Accept(KeyInterop.VirtualKeyFromKey(k)); };
        PreviewMouseDown += (_, e) => { if (keyboardOnly) return; e.Handled = true; Accept(e.ChangedButton switch { MouseButton.Left => 1, MouseButton.Right => 2, MouseButton.Middle => 4, MouseButton.XButton1 => 5, _ => 6 }); };
        void Accept(int vk) { if (!Bindings.Valid(vk)) return; CapturedKey = vk; DialogResult = true; }
    }
}
