using System.Drawing.Drawing2D;

namespace Paint;

internal sealed class PageMenuRenderer : ToolStripProfessionalRenderer
{
    public PageMenuRenderer() { RoundedEdges = false; }

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e) => e.Graphics.Clear(e.ToolStrip.BackColor);

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        using Pen border = new(UiTheme.Border);
        if (e.ToolStrip is ToolStripDropDown)
            e.Graphics.DrawRectangle(border, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
        else e.Graphics.DrawLine(border, 0, e.ToolStrip.Height - 1, e.ToolStrip.Width, e.ToolStrip.Height - 1);
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        bool page = e.Item.Tag is DrawingDocument && e.ToolStrip is MenuStrip;
        PaintItem(e.Graphics, e.Item, page && e.Item is ToolStripMenuItem { Checked: true }, page);
    }

    protected override void OnRenderButtonBackground(ToolStripItemRenderEventArgs e) =>
        PaintItem(e.Graphics, e.Item, e.Item is ToolStripButton { Checked: true }, false);

    private static void PaintItem(Graphics graphics, ToolStripItem item, bool active, bool page)
    {
        if (!page && !active && !item.Selected && !item.Pressed) return;
        Rectangle bounds = new(2, 2, Math.Max(1, item.Width - 5), Math.Max(1, item.Height - 5));
        Color fill = item.Enabled && item.Pressed ? UiTheme.Pressed
            : active ? Color.FromArgb(165, UiTheme.Accent) : item.Selected && item.Enabled ? UiTheme.Hover : UiTheme.Background;
        GraphicsState state = graphics.Save();
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using GraphicsPath path = UiTheme.RoundedRectangle(bounds, (int)(6 * item.Owner!.DeviceDpi / 96f));
        using Brush brush = new SolidBrush(fill);
        graphics.FillPath(brush, path);
        if (page || active)
        {
            using Pen border = new(active ? UiTheme.AccentBorder : UiTheme.Border);
            graphics.DrawPath(border, path);
        }
        graphics.Restore(state);
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? UiTheme.Text : UiTheme.Muted;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
    {
        using Brush brush = new SolidBrush(UiTheme.Surface);
        e.Graphics.FillRectangle(brush, e.AffectedBounds);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        using Pen pen = new(UiTheme.Border);
        if (e.Vertical) e.Graphics.DrawLine(pen, e.Item.Width / 2, 8, e.Item.Width / 2, e.Item.Height - 8);
        else e.Graphics.DrawLine(pen, 8, e.Item.Height / 2, e.Item.Width - 8, e.Item.Height / 2);
    }
}
