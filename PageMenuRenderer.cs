namespace Paint;

internal sealed class PageMenuRenderer : ToolStripProfessionalRenderer
{
    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        if (e.Item is ToolStripMenuItem { Checked: true, Tag: DrawingDocument } && e.ToolStrip is MenuStrip)
        {
            Rectangle bounds = new(Point.Empty, e.Item.Size);
            using Brush background = new SolidBrush(e.ToolStrip.BackColor);
            e.Graphics.FillRectangle(background, bounds);
            using Brush highlight = new SolidBrush(Color.FromArgb(65, Color.LawnGreen));
            e.Graphics.FillRectangle(highlight, bounds);
            return;
        }
        base.OnRenderMenuItemBackground(e);
    }
}
