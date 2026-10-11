using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace Paint;

// Customize native painting while retaining TrackBar input and accessibility.
internal sealed class ThemedTrackBar : TrackBar
{
    private const int ReflectedNotify = 0x204E;
    private const int CustomDraw = -12;
    private const uint PrePaint = 0x0001;
    private const uint ItemPrePaint = 0x10001;
    private const int NotifyItemDraw = 0x0020;
    private const int SkipDefault = 0x0004;
    private const ulong Thumb = 2;
    private const ulong Channel = 3;
    private bool hovered;

    public ThemedTrackBar() { Cursor = Cursors.Hand; }

    protected override void OnValueChanged(EventArgs e)
    {
        base.OnValueChanged(e);
        Invalidate();
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        hovered = true;
        base.OnMouseEnter(e);
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        hovered = false;
        base.OnMouseLeave(e);
        Invalidate();
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == ReflectedNotify && message.LParam != IntPtr.Zero)
        {
            NotificationHeader header = Marshal.PtrToStructure<NotificationHeader>(message.LParam);
            if (header.Code == CustomDraw)
            {
                CustomDrawData data = Marshal.PtrToStructure<CustomDrawData>(message.LParam);
                if (data.Stage == PrePaint)
                {
                    message.Result = (IntPtr)NotifyItemDraw;
                    return;
                }
                if (data.Stage == ItemPrePaint && data.Item.ToUInt64() is Thumb or Channel)
                {
                    using Graphics graphics = Graphics.FromHdc(data.DeviceContext);
                    graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    Rectangle bounds = Rectangle.FromLTRB(data.Bounds.Left, data.Bounds.Top, data.Bounds.Right, data.Bounds.Bottom);
                    if (data.Item.ToUInt64() == Channel) DrawChannel(graphics, bounds);
                    else DrawThumb(graphics, bounds);
                    message.Result = (IntPtr)SkipDefault;
                    return;
                }
            }
        }
        base.WndProc(ref message);
    }

    private void DrawChannel(Graphics graphics, Rectangle bounds)
    {
        float middle = bounds.Top + bounds.Height / 2f;
        float thickness = 4 * DeviceDpi / 96f;
        using Pen track = new(UiTheme.Border, thickness) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        graphics.DrawLine(track, bounds.Left, middle, bounds.Right, middle);
        if (Value <= Minimum || Maximum == Minimum) return;
        float fraction = (Value - Minimum) / (float)(Maximum - Minimum);
        using Pen filled = new(Enabled ? UiTheme.AccentBorder : UiTheme.Muted, thickness)
        {
            StartCap = LineCap.Round, EndCap = LineCap.Round
        };
        graphics.DrawLine(filled, bounds.Left, middle, bounds.Left + bounds.Width * fraction, middle);
    }

    private void DrawThumb(Graphics graphics, Rectangle bounds)
    {
        int size = Math.Max(2, Math.Min(bounds.Width, bounds.Height) - 2);
        Rectangle circle = new(bounds.Left + (bounds.Width - size) / 2, bounds.Top + (bounds.Height - size) / 2, size, size);
        Color color = !Enabled ? UiTheme.Border : Capture ? UiTheme.Pressed : hovered ? UiTheme.Accent : UiTheme.Surface;
        using Brush fill = new SolidBrush(color);
        using Pen outline = new(Enabled ? UiTheme.AccentBorder : UiTheme.Muted, (Focused ? 2f : 1.3f) * DeviceDpi / 96f);
        graphics.FillEllipse(fill, circle);
        graphics.DrawEllipse(outline, circle);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NotificationHeader
    {
        public IntPtr Window;
        public UIntPtr Id;
        public int Code;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRectangle
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CustomDrawData
    {
        public NotificationHeader Header;
        public uint Stage;
        public IntPtr DeviceContext;
        public NativeRectangle Bounds;
        public UIntPtr Item;
        public uint State;
        public IntPtr ItemData;
    }
}
