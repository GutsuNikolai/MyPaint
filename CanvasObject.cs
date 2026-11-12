using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace Paint;

internal sealed class CanvasObject : IDisposable
{
    public string Name { get; set; }
    public Bitmap Image { get; }
    public Rectangle Bounds { get; set; }
    public bool IsShape { get; }

    public CanvasObject(string name, Bitmap image, Rectangle bounds, bool isShape = false)
    {
        Name = name;
        Image = image;
        Bounds = bounds;
        IsShape = isShape;
    }

    public CanvasObject Clone() => new(Name, (Bitmap)Image.Clone(), Bounds, IsShape);

    public bool Contains(Point point)
    {
        if (!Bounds.Contains(point)) return false;
        if (IsShape) return true;
        int x = Math.Clamp((point.X - Bounds.X) * Image.Width / Math.Max(1, Bounds.Width), 0, Image.Width - 1);
        int y = Math.Clamp((point.Y - Bounds.Y) * Image.Height / Math.Max(1, Bounds.Height), 0, Image.Height - 1);
        return Image.GetPixel(x, y).A > 16;
    }

    public void Draw(Graphics graphics)
    {
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.DrawImage(Image, Bounds);
    }

    public void Dispose() => Image.Dispose();
}
