namespace Paint;

// The canvas handles keyboard shortcuts through Form1. It must not receive focus:
// ScrollableControl otherwise scrolls a large focused child back to its top-left corner.
internal sealed class CanvasPictureBox : PictureBox
{
    public CanvasPictureBox()
    {
        SetStyle(ControlStyles.Selectable, false);
        TabStop = false;
    }
}
