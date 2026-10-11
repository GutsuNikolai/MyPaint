using System.Drawing.Drawing2D;

namespace Paint;

internal static class UiTheme
{
    public static readonly Color Surface = Color.White;
    public static readonly Color Background = Color.FromArgb(247, 249, 245);
    public static readonly Color Workspace = Color.FromArgb(229, 233, 227);
    public static readonly Color Text = Color.FromArgb(42, 53, 44);
    public static readonly Color Muted = Color.FromArgb(105, 116, 106);
    public static readonly Color Border = Color.FromArgb(216, 224, 212);
    public static readonly Color Accent = Color.FromArgb(216, 237, 195);
    public static readonly Color AccentBorder = Color.FromArgb(139, 174, 111);
    public static readonly Color Hover = Color.FromArgb(239, 246, 231);
    public static readonly Color Pressed = Color.FromArgb(198, 223, 171);

    public static void Apply(Control root)
    {
        root.BackColor = Surface;
        root.ForeColor = Text;
        if (root is NumericUpDown or TextBoxBase or ComboBox) return;
        foreach (Control control in root.Controls)
        {
            Apply(control);
            if (control is Button button) StyleButton(button, Equals(button.Tag, "primary"));
            else if (control is ComboBox combo) combo.FlatStyle = FlatStyle.Flat;
            else if (control is NumericUpDown number) number.BorderStyle = BorderStyle.FixedSingle;
            else if (control is TextBox input) input.BorderStyle = BorderStyle.FixedSingle;
            else if (control is Label) control.ForeColor = Muted;
        }
    }

    public static void StyleButton(Button button, bool primary = false)
    {
        Color normal = primary ? Accent : Surface;
        button.FlatStyle = FlatStyle.Flat;
        button.UseVisualStyleBackColor = false;
        button.BackColor = normal;
        button.ForeColor = Text;
        button.FlatAppearance.BorderColor = primary ? AccentBorder : Border;
        button.FlatAppearance.MouseOverBackColor = Hover;
        button.FlatAppearance.MouseDownBackColor = Pressed;
        button.Cursor = Cursors.Hand;
        button.MouseEnter += (_, _) => button.BackColor = Hover;
        button.MouseLeave += (_, _) => button.BackColor = normal;
        button.MouseDown += (_, e) => { if (e.Button == MouseButtons.Left) button.BackColor = Pressed; };
        button.MouseUp += (_, _) => button.BackColor = button.ClientRectangle.Contains(button.PointToClient(Cursor.Position)) ? Hover : normal;
    }

    public static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
    {
        GraphicsPath path = new();
        int diameter = Math.Max(1, Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height)));
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal sealed class ColorSampleButton : Button
{
    private Color sampleColor;
    public Color SampleColor
    {
        get => sampleColor;
        set { sampleColor = value; Invalidate(); }
    }

    public ColorSampleButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(12, 7, 12, 7);
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        Size text = TextRenderer.MeasureText(Text, Font);
        int swatch = (int)(20 * DeviceDpi / 96f);
        return new Size(text.Width + swatch + Padding.Horizontal + 12,
            Math.Max(text.Height, swatch) + Padding.Vertical + 4);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        Rectangle edge = new(0, 0, Width - 1, Height - 1);
        using GraphicsPath outline = UiTheme.RoundedRectangle(edge, (int)(5 * DeviceDpi / 96f));
        using Pen border = new(Focused ? UiTheme.AccentBorder : UiTheme.Border);
        e.Graphics.DrawPath(border, outline);
        int size = (int)(20 * DeviceDpi / 96f);
        Rectangle sample = new(Padding.Left, (Height - size) / 2, size, size);
        using Brush brush = new SolidBrush(SampleColor);
        e.Graphics.FillRectangle(brush, sample);
        e.Graphics.DrawRectangle(border, sample);
        Rectangle text = new(sample.Right + 10, 0, Math.Max(1, Width - sample.Right - 10 - Padding.Right), Height);
        TextRenderer.DrawText(e.Graphics, Text, Font, text, Enabled ? ForeColor : UiTheme.Muted,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(edge, -4, -4));
    }
}
