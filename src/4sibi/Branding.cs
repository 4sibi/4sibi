using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace Sibi;

internal static class Branding
{
    public static BitmapImage Source(string file) => new(new Uri("pack://application:,,,/Assets/" + file));
    public static Image Logo(double size) => new() { Source = Source("4sibi-logo.png"), Width = size, Height = size, VerticalAlignment = VerticalAlignment.Center };
    public static readonly Drawing.Icon TrayIcon = LoadIcon();
    static Drawing.Icon LoadIcon()
    {
        using var stream = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/4sibi.ico")).Stream;
        using var icon = new Drawing.Icon(stream, 32, 32);
        return (Drawing.Icon)icon.Clone();
    }
}

internal sealed class DarkTrayRenderer : Forms.ToolStripProfessionalRenderer
{
    public DarkTrayRenderer() { RoundedEdges = false; }
    protected override void OnRenderToolStripBackground(Forms.ToolStripRenderEventArgs e) => e.Graphics.Clear(Drawing.Color.FromArgb(19, 21, 26));
    protected override void OnRenderToolStripBorder(Forms.ToolStripRenderEventArgs e)
    {
        using var pen = new Drawing.Pen(Drawing.Color.FromArgb(51, 55, 64));
        e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
    }
    protected override void OnRenderMenuItemBackground(Forms.ToolStripItemRenderEventArgs e)
    {
        if (!e.Item.Selected) return;
        using var brush = new Drawing.SolidBrush(Drawing.Color.FromArgb(43, 46, 55));
        e.Graphics.FillRectangle(brush, new Drawing.Rectangle(0, 0, e.Item.Width, e.Item.Height));
    }
    protected override void OnRenderItemText(Forms.ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Text is "Beenden" or "Quit" ? Drawing.Color.FromArgb(255, 65, 85) : Drawing.Color.FromArgb(232, 234, 239);
        base.OnRenderItemText(e);
    }
    protected override void OnRenderSeparator(Forms.ToolStripSeparatorRenderEventArgs e)
    {
        using var pen = new Drawing.Pen(Drawing.Color.FromArgb(48, 52, 61));
        e.Graphics.DrawLine(pen, 12, e.Item.Height / 2, e.Item.Width - 12, e.Item.Height / 2);
    }
}
