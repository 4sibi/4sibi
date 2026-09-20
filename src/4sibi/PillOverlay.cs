using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace Sibi;

public sealed class OverlayWindow : Window
{
    readonly MainWindow main;
    readonly Border frame;
    readonly Ellipse dot;
    readonly TextBlock state, rate;
    string lastStatus = "paused";
    public OverlayWindow(MainWindow owner)
    {
        main = owner; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true; Background = Brushes.Transparent; ShowInTaskbar = false; ShowActivated = false; Topmost = true; FontFamily = owner.FontFamily;
        frame = new Border { Width = 294, Height = 38, Margin = new Thickness(3), Background = Brushes.Transparent };
        Content = new Viewbox { Child = frame, Stretch = Stretch.Uniform };
        var split = new Grid(); split.ColumnDefinitions.Add(new() { Width = new GridLength(144) }); split.ColumnDefinitions.Add(new() { Width = new GridLength(8) }); split.ColumnDefinitions.Add(new()); frame.Child = split;
        Border Chip() => new() { CornerRadius = new CornerRadius(10), Background = MainWindow.Brush("#121419"), BorderBrush = MainWindow.Brush("#383D46"), BorderThickness = new Thickness(1), Padding = new Thickness(10, 0, 10, 0) };
        var statusChip = Chip(); split.Children.Add(statusChip);
        var valuesChip = Chip(); Grid.SetColumn(valuesChip, 2); split.Children.Add(valuesChip);
        var row = new Grid(); row.ColumnDefinitions.Add(new() { Width = new GridLength(36) }); row.ColumnDefinitions.Add(new() { Width = new GridLength(16) }); row.ColumnDefinitions.Add(new()); statusChip.Child = row;
        var logo = Branding.Logo(26); logo.HorizontalAlignment = HorizontalAlignment.Left; row.Children.Add(logo);
        dot = new Ellipse { Width = 7, Height = 7, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center }; row.Children.Add(dot);
        Grid.SetColumn(dot, 1);
        state = owner.Text("", 12); state.TextWrapping = TextWrapping.NoWrap; Grid.SetColumn(state, 2); row.Children.Add(state);
        rate = owner.Text("", 11, true); rate.TextWrapping = TextWrapping.NoWrap; rate.TextTrimming = TextTrimming.CharacterEllipsis; rate.HorizontalAlignment = HorizontalAlignment.Center; valuesChip.Child = rate;
        valuesChip.SetBinding(VisibilityProperty, new System.Windows.Data.Binding(nameof(TextBlock.Text)) { Source = rate, Converter = new EmptyTextVisibility() });
        MouseLeftButtonDown += (_, _) => { if (main.Config.OverlayLocked) return; try { DragMove(); main.SaveOverlayPosition(Left, Top); } catch { } };
        var menu = new ContextMenu { Background = MainWindow.Brush("#171A21"), Foreground = Brushes.White, BorderBrush = MainWindow.Brush("#343943"), Padding = new Thickness(5), FontSize = 13 };
        frame.ContextMenu = menu;
        void PopulateMenu() {
            menu.Items.Clear();
            Add(main.T("4sibi öffnen", "Open 4sibi"), main.Restore);
            Add(main.T("Overlay anpassen", "Customize overlay"), () => { main.Restore(); main.ShowPage(8); });
            var locked = Add(main.T("Position sperren", "Lock position"), () => main.Update(s => s.OverlayLocked = !s.OverlayLocked)); locked.IsCheckable = true; locked.IsChecked = main.Config.OverlayLocked;
            menu.Items.Add(new Separator());
            Add(main.T("Makro stoppen", "Stop macro"), main.StopMacro);
            Add(main.T("Overlay ausblenden", "Hide overlay"), main.HideOverlay);
        }
        menu.Opened += (_, _) => PopulateMenu();
        MenuItem Add(string label, Action action) { var item = new MenuItem { Header = label, Padding = new Thickness(8, 6, 8, 6) }; item.Click += (_, _) => action(); menu.Items.Add(item); return item; }
        PopulateMenu(); ApplyTheme(); UpdateStatus("paused");
    }
    public void ApplyTheme()
    {
        double baseWidth = 170 + (main.Config.OverlayShowAps ? 78 : 0) + (main.Config.OverlayShowHotkey ? 52 : 0) + (main.Config.OverlayShowProfile ? 120 : 0);
        frame.Width = baseWidth - 6; Width = baseWidth * main.Config.OverlayScale; Height = 44 * main.Config.OverlayScale;
        rate.MaxWidth = baseWidth - 145; Opacity = main.Config.OverlayOpacity;
        var area = SystemParameters.WorkArea;
        Left = Math.Clamp(main.Config.OverlayX == -1 ? area.Right - Width - 24 : main.Config.OverlayX, SystemParameters.VirtualScreenLeft, Math.Max(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - Width));
        Top = Math.Clamp(main.Config.OverlayY == -1 ? area.Bottom - Height - 22 : main.Config.OverlayY, SystemParameters.VirtualScreenTop, Math.Max(SystemParameters.VirtualScreenTop, SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - Height));
        Cursor = main.Config.OverlayLocked ? Cursors.Arrow : Cursors.SizeAll;
        ToolTip = main.T("Ziehen zum Verschieben · Rechtsklick für Optionen", "Drag to move · Right-click for options");
        UpdateStatus(lastStatus);
    }
    public void Place(string corner)
    {
        var area = SystemParameters.WorkArea;
        Left = corner.EndsWith("l") ? area.Left + 22 : area.Right - Width - 22;
        Top = corner.StartsWith("t") ? area.Top + 22 : area.Bottom - Height - 22;
        main.SaveOverlayPosition(Left, Top);
    }
    public void UpdateStatus(string status)
    {
        lastStatus = status;
        state.Text = status switch { "running" => main.T("Aktiv", "Active"), "waiting" => main.T("Bereit", "Ready"), "error" => main.T("Blockiert", "Blocked"), _ => main.T("Pausiert", "Paused") };
        var accent = MainWindow.Brush(main.Config.Accent); dot.Fill = status == "paused" ? MainWindow.Brush("#858C99") : accent;
        dot.Effect = status == "running" ? new DropShadowEffect { Color = accent.Color, ShadowDepth = 0, BlurRadius = 6, Opacity = main.Config.Glow * .4 } : null;
        var values = new List<string>();
        if (main.Config.OverlayShowAps) values.Add($"{main.Config.Aps} APS");
        if (main.Config.OverlayShowHotkey) values.Add(Bindings.Name(main.Config.Trigger));
        if (main.Config.OverlayShowProfile) values.Add(main.ActiveProfileName);
        rate.Text = string.Join("   |   ", values); rate.ToolTip = rate.Text;
    }
}

internal sealed class EmptyTextVisibility : System.Windows.Data.IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => throw new NotSupportedException();
}
