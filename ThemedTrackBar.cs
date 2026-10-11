using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace Paint;

// Customize native painting while retaining TrackBar input and accessibility.
internal sealed class ThemedTrackBar : TrackBar
{
    private const int ReflectedNotify = 0x204E;
    private const int CustomDraw = -12;
    private const uint PrePaint = 0x0001;
    private const int SkipDefault = 0x0004;
    private const int GetThumbRectangle = 0x0419;
    private const int GetChannelRectangle = 0x041A;
    private bool hovered;
    private int paintedValue;

    public ThemedTrackBar() { Cursor = Cursors.Hand; }

    protected override void OnValueChanged(EventArgs e)
    {
        paintedValue = Value;
        base.OnValueChanged(e);
        Invalidate();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        paintedValue = Value;
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
                    NativeRectangle channel = new(), thumb = new();
                    SendMessage(Handle, GetChannelRectangle, IntPtr.Zero, ref channel);
                    SendMessage(Handle, GetThumbRectangle, IntPtr.Zero, ref thumb);
                    using Graphics graphics = Graphics.FromHdc(data.DeviceContext);
                    using Brush background = new SolidBrush(BackColor);
                    graphics.FillRectangle(background, ClientRectangle);
                    graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    // Native partial updates can repaint only the thumb. Always draw both parts together.
                    DrawChannel(graphics, channel.ToRectangle());
                    DrawThumb(graphics, thumb.ToRectangle());
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
        // Reading Value here updates TrackBar's internal state before it can raise ValueChanged.
        if (paintedValue <= Minimum || Maximum == Minimum) return;
        float fraction = (paintedValue - Minimum) / (float)(Maximum - Minimum);
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

        public readonly Rectangle ToRectangle() => Rectangle.FromLTRB(Left, Top, Right, Bottom);
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, ref NativeRectangle rectangle);

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
