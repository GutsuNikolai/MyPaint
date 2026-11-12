using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Paint;

public partial class Form1
{
    private static List<CanvasObject> CloneObjects(IEnumerable<CanvasObject> objects) =>
        objects.Select(item => item.Clone()).ToList();

    private static void DisposeObjects(IEnumerable<CanvasObject> objects)
    {
        foreach (CanvasObject item in objects) item.Dispose();
    }

    private void RestoreObjects(List<CanvasObject> state)
    {
        DisposeObjects(currentDocument.Objects);
        currentDocument.Objects.Clear();
        currentDocument.Objects.AddRange(state);
        selectedObject = null;
        movingObject = null;
    }

    private CanvasObject? HitTestObject(Point point)
    {
        for (int i = currentDocument.Objects.Count - 1; i >= 0; i--)
            if (currentDocument.Objects[i].Contains(point)) return currentDocument.Objects[i];
        return null;
    }

    private void BeginObjectMove(CanvasObject item, Point point)
    {
        selectedObject = movingObject = item;
        objectDragOffset = new Point(point.X - item.Bounds.X, point.Y - item.Bounds.Y);
        objectMoveUndoSaved = false;
        canvasPictureBox.Cursor = Cursors.SizeAll;
        canvasPictureBox.Capture = true;
        canvasPictureBox.Invalidate();
        UpdateObjectCommands();
    }

    private void UpdateObjectMove(Point point)
    {
        if (movingObject == null) return;
        Point location = ClampImageLocation(
            new Point(point.X - objectDragOffset.X, point.Y - objectDragOffset.Y), movingObject.Bounds.Size);
        if (location == movingObject.Bounds.Location) return;
        if (!objectMoveUndoSaved)
        {
            SaveUndoState();
            objectMoveUndoSaved = true;
        }
        movingObject.Bounds = new Rectangle(location, movingObject.Bounds.Size);
        canvasPictureBox.Invalidate();
    }

    private void EndObjectMove()
    {
        movingObject = null;
        objectMoveUndoSaved = false;
        canvasPictureBox.Capture = false;
        canvasPictureBox.Cursor = currentTool == ToolType.Mouse ? Cursors.Default : Cursors.Cross;
        canvasPictureBox.Invalidate();
    }

    private void DrawCanvasObjects(Graphics graphics)
    {
        foreach (CanvasObject item in currentDocument.Objects) item.Draw(graphics);
        if (selectedObject == null || !currentDocument.Objects.Contains(selectedObject)) return;
        using Pen border = new(UiTheme.AccentBorder, 1.5f / ZoomFactor) { DashStyle = DashStyle.Dash };
        Rectangle bounds = selectedObject.Bounds;
        graphics.DrawRectangle(border, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
    }

    private Bitmap RenderComposite(DrawingDocument document)
    {
        Bitmap result = (Bitmap)document.Bitmap.Clone();
        using Graphics graphics = Graphics.FromImage(result);
        foreach (CanvasObject item in document.Objects) item.Draw(graphics);
        return result;
    }

    private Color GetCompositePixel(Point point)
    {
        for (int i = currentDocument.Objects.Count - 1; i >= 0; i--)
        {
            CanvasObject item = currentDocument.Objects[i];
            if (!item.Bounds.Contains(point)) continue;
            int x = Math.Clamp((point.X - item.Bounds.X) * item.Image.Width / Math.Max(1, item.Bounds.Width), 0, item.Image.Width - 1);
            int y = Math.Clamp((point.Y - item.Bounds.Y) * item.Image.Height / Math.Max(1, item.Bounds.Height), 0, item.Image.Height - 1);
            Color color = item.Image.GetPixel(x, y);
            if (color.A > 16) return color;
        }
        return canvasBitmap.GetPixel(point.X, point.Y);
    }

    private void AddObject(CanvasObject item)
    {
        currentDocument.Objects.Add(item);
        selectedObject = item;
        UpdateObjectCommands();
        canvasPictureBox.Invalidate();
    }

    private void DeleteSelectedObject()
    {
        if (selectedObject == null || !currentDocument.Objects.Contains(selectedObject)) return;
        SaveUndoState();
        currentDocument.Objects.Remove(selectedObject);
        selectedObject.Dispose();
        selectedObject = null;
        UpdateObjectCommands();
        canvasPictureBox.Invalidate();
    }

    private void MoveSelectedObject(int direction)
    {
        if (selectedObject == null) return;
        int index = currentDocument.Objects.IndexOf(selectedObject);
        int target = Math.Clamp(index + direction, 0, currentDocument.Objects.Count - 1);
        if (index < 0 || target == index) return;
        SaveUndoState();
        currentDocument.Objects.RemoveAt(index);
        currentDocument.Objects.Insert(target, selectedObject);
        UpdateObjectCommands();
        canvasPictureBox.Invalidate();
    }

    private void UpdateObjectCommands()
    {
        bool selected = selectedObject != null && currentDocument.Objects.Contains(selectedObject);
        bringForwardMenuItem.Enabled = selected && currentDocument.Objects.IndexOf(selectedObject!) < currentDocument.Objects.Count - 1;
        sendBackwardMenuItem.Enabled = selected && currentDocument.Objects.IndexOf(selectedObject!) > 0;
    }

    private CanvasObject CreateShapeObject(Point first, Point second)
    {
        if (constrainShape) second = ConstrainShapePoint(first, second);
        int margin = Math.Max(3, (int)Math.Ceiling(brushWidth / 2f) + 2);
        Rectangle geometry = Rectangle.FromLTRB(Math.Min(first.X, second.X), Math.Min(first.Y, second.Y),
            Math.Max(first.X, second.X) + 1, Math.Max(first.Y, second.Y) + 1);
        Rectangle bounds = Rectangle.Inflate(geometry, margin, margin);
        bounds.Intersect(new Rectangle(Point.Empty, canvasBitmap.Size));
        Bitmap image = new(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
        using Graphics graphics = Graphics.FromImage(image);
        graphics.TranslateTransform(-bounds.X, -bounds.Y);
        DrawShape(graphics, first, second);
        return new CanvasObject(ShapeName(currentTool), image, bounds, true);
    }

    private void FlattenObjectsWithoutHistory()
    {
        if (currentDocument.Objects.Count == 0) return;
        using Graphics graphics = Graphics.FromImage(canvasBitmap);
        foreach (CanvasObject item in currentDocument.Objects) item.Draw(graphics);
        DisposeObjects(currentDocument.Objects);
        currentDocument.Objects.Clear();
        selectedObject = null;
        UpdateObjectCommands();
    }

    private bool TryFillShapeObject(Point point)
    {
        for (int i = currentDocument.Objects.Count - 1; i >= 0; i--)
        {
            CanvasObject item = currentDocument.Objects[i];
            if (!item.IsShape || !item.Bounds.Contains(point)) continue;
            int x = Math.Clamp((point.X - item.Bounds.X) * item.Image.Width / Math.Max(1, item.Bounds.Width), 0, item.Image.Width - 1);
            int y = Math.Clamp((point.Y - item.Bounds.Y) * item.Image.Height / Math.Max(1, item.Bounds.Height), 0, item.Image.Height - 1);
            if (TryFillEnclosedArea(item.Image, new Point(x, y)))
            {
                selectedObject = item;
                return true;
            }
        }
        return false;
    }

    private bool TryFillEnclosedArea(Bitmap bitmap, Point point)
    {
        int width = bitmap.Width, height = bitmap.Height;
        Rectangle bounds = new(Point.Empty, bitmap.Size);
        int[] pixels = new int[width * height];
        BitmapData readData = bitmap.LockBits(bounds, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            for (int y = 0; y < height; y++)
                Marshal.Copy(IntPtr.Add(readData.Scan0, y * readData.Stride), pixels, y * width, width);
        }
        finally { bitmap.UnlockBits(readData); }
        int start = point.Y * width + point.X;
        int target = pixels[start];
        int replacement = currentColor.ToArgb();
        if (target == replacement) return true;
        bool[] visited = new bool[pixels.Length];
        Queue<int> pending = new();
        pending.Enqueue(start);
        visited[start] = true;
        List<int> area = new();
        bool touchesEdge = false;
        static bool Similar(int color, int targetColor)
        {
            Color a = Color.FromArgb(color), b = Color.FromArgb(targetColor);
            const int tolerance = 128;
            return Math.Abs(a.A - b.A) <= tolerance && Math.Abs(a.R - b.R) <= tolerance
                && Math.Abs(a.G - b.G) <= tolerance && Math.Abs(a.B - b.B) <= tolerance;
        }
        while (pending.Count > 0)
        {
            int index = pending.Dequeue();
            area.Add(index);
            int x = index % width, y = index / width;
            touchesEdge |= x == 0 || y == 0 || x == width - 1 || y == height - 1;
            void Visit(int next)
            {
                if (visited[next] || !Similar(pixels[next], target)) return;
                visited[next] = true;
                pending.Enqueue(next);
            }
            if (x > 0) Visit(index - 1);
            if (x < width - 1) Visit(index + 1);
            if (y > 0) Visit(index - width);
            if (y < height - 1) Visit(index + width);
        }
        if (touchesEdge) return false;
        SaveUndoState();
        foreach (int index in area) pixels[index] = replacement;
        BitmapData writeData = bitmap.LockBits(bounds, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            for (int y = 0; y < height; y++)
                Marshal.Copy(pixels, y * width, IntPtr.Add(writeData.Scan0, y * writeData.Stride), width);
        }
        finally { bitmap.UnlockBits(writeData); }
        canvasPictureBox.Invalidate();
        return true;
    }
}
