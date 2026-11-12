using System.Drawing.Imaging;

namespace Paint;

public partial class Form1
{
    private enum CanvasResizeMode { None, Width, Height, Both }

    private ToolStripMenuItem CreateLayersMenu()
    {
        layersMenuItem = new ToolStripMenuItem("Layers");
        layersMenuItem.DropDownOpening += (_, _) => UpdateLayersMenu();
        return layersMenuItem;
    }

    private void UpdateLayersMenu()
    {
        if (layersMenuItem == null || currentDocument == null) return;
        layersMenuItem.DropDownItems.Clear();
        ToolStripMenuItem add = new("Add Layer", null, (_, _) => AddLayer());
        ToolStripMenuItem delete = new("Delete Active Layer", null, (_, _) => DeleteActiveLayer())
        {
            Enabled = currentDocument.Layers.Count > 1
        };
        ToolStripMenuItem mergeDown = new("Merge with Layer Below", null, (_, _) => MergeLayerDown())
        {
            Enabled = currentDocument.ActiveLayerIndex > 0
        };
        ToolStripMenuItem mergeUp = new("Merge with Layer Above", null, (_, _) => MergeLayerUp())
        {
            Enabled = currentDocument.ActiveLayerIndex < currentDocument.Layers.Count - 1
        };
        ToolStripMenuItem moveUp = new("Move Layer Up", null, (_, _) => MoveActiveLayer(1))
        {
            Enabled = currentDocument.ActiveLayerIndex < currentDocument.Layers.Count - 1,
            ShortcutKeyDisplayString = "Ctrl+L, +"
        };
        ToolStripMenuItem moveDown = new("Move Layer Down", null, (_, _) => MoveActiveLayer(-1))
        {
            Enabled = currentDocument.ActiveLayerIndex > 0,
            ShortcutKeyDisplayString = "Ctrl+L, −"
        };
        layersMenuItem.DropDownItems.AddRange(new ToolStripItem[]
        {
            add, delete, new ToolStripSeparator(), moveUp, moveDown,
            new ToolStripSeparator(), mergeDown, mergeUp, new ToolStripSeparator()
        });
        for (int index = currentDocument.Layers.Count - 1; index >= 0; index--)
        {
            int layerIndex = index;
            CanvasLayer layer = currentDocument.Layers[index];
            ToolStripMenuItem item = new(layer.Name)
            {
                Checked = index == currentDocument.ActiveLayerIndex,
                CheckOnClick = false
            };
            item.Click += (_, _) => SelectLayer(layerIndex);
            layersMenuItem.DropDownItems.Add(item);
        }
    }

    private void AddLayer()
    {
        FinishDrawing();
        SaveUndoState();
        int number = 1;
        HashSet<string> names = currentDocument.Layers.Select(layer => layer.Name).ToHashSet();
        while (names.Contains($"Layer {number}")) number++;
        Bitmap bitmap = new(canvasBitmap.Width, canvasBitmap.Height, PixelFormat.Format32bppArgb);
        int index = currentDocument.ActiveLayerIndex + 1;
        currentDocument.Layers.Insert(index, new CanvasLayer($"Layer {number}", bitmap));
        currentDocument.ActiveLayerIndex = index;
        ClearSelection();
        UpdateLayersMenu();
        UpdateStatus();
        canvasPictureBox.Invalidate();
    }

    private void DeleteActiveLayer()
    {
        FinishDrawing();
        if (currentDocument.Layers.Count <= 1) return;
        CanvasLayer layer = currentDocument.Layers[currentDocument.ActiveLayerIndex];
        if (MessageBox.Show(this, $"Удалить слой «{layer.Name}»?", "MyPaint",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        SaveUndoState();
        currentDocument.Layers.RemoveAt(currentDocument.ActiveLayerIndex);
        layer.Dispose();
        currentDocument.ActiveLayerIndex = Math.Min(currentDocument.ActiveLayerIndex,
            currentDocument.Layers.Count - 1);
        ClearSelection();
        UpdateLayersMenu();
        UpdateStatus();
        canvasPictureBox.Invalidate();
    }

    private void MergeLayerDown()
    {
        FinishDrawing();
        int sourceIndex = currentDocument.ActiveLayerIndex;
        if (sourceIndex <= 0) return;
        SaveUndoState();
        CanvasLayer source = currentDocument.Layers[sourceIndex];
        CanvasLayer target = currentDocument.Layers[sourceIndex - 1];
        using (Graphics graphics = Graphics.FromImage(target.Bitmap))
            graphics.DrawImageUnscaled(source.Bitmap, Point.Empty);
        currentDocument.Layers.RemoveAt(sourceIndex);
        source.Dispose();
        currentDocument.ActiveLayerIndex = sourceIndex - 1;
        ClearSelection();
        UpdateLayersMenu();
        UpdateStatus();
        canvasPictureBox.Invalidate();
    }

    private void MergeLayerUp()
    {
        FinishDrawing();
        int sourceIndex = currentDocument.ActiveLayerIndex;
        if (sourceIndex >= currentDocument.Layers.Count - 1) return;
        SaveUndoState();
        CanvasLayer source = currentDocument.Layers[sourceIndex];
        CanvasLayer upper = currentDocument.Layers[sourceIndex + 1];
        Bitmap merged = new(canvasBitmap.Width, canvasBitmap.Height, PixelFormat.Format32bppArgb);
        using (Graphics graphics = Graphics.FromImage(merged))
        {
            graphics.DrawImageUnscaled(source.Bitmap, Point.Empty);
            graphics.DrawImageUnscaled(upper.Bitmap, Point.Empty);
        }
        upper.Bitmap.Dispose();
        upper.Bitmap = merged;
        currentDocument.Layers.RemoveAt(sourceIndex);
        source.Dispose();
        currentDocument.ActiveLayerIndex = sourceIndex;
        ClearSelection();
        UpdateLayersMenu();
        UpdateStatus();
        canvasPictureBox.Invalidate();
    }

    private void SelectLayer(int index)
    {
        FinishDrawing();
        if (index < 0 || index >= currentDocument.Layers.Count) return;
        currentDocument.ActiveLayerIndex = index;
        ClearSelection();
        UpdateLayersMenu();
        UpdateStatus();
        canvasPictureBox.Invalidate();
    }

    private void MoveActiveLayer(int direction)
    {
        FinishDrawing();
        int source = currentDocument.ActiveLayerIndex;
        int target = source + Math.Sign(direction);
        if (target < 0 || target >= currentDocument.Layers.Count) return;
        SaveUndoState();
        (currentDocument.Layers[source], currentDocument.Layers[target]) =
            (currentDocument.Layers[target], currentDocument.Layers[source]);
        currentDocument.ActiveLayerIndex = target;
        UpdateLayersMenu();
        UpdateStatus();
        canvasPictureBox.Invalidate();
    }

    private void ResizeAllLayers(Size size, bool saveUndo)
    {
        size = new Size(Math.Max(1, size.Width), Math.Max(1, size.Height));
        if (size == canvasBitmap.Size) return;
        List<Bitmap> resizedLayers = new();
        try
        {
            foreach (CanvasLayer layer in currentDocument.Layers)
            {
                Bitmap resized = new(size.Width, size.Height, PixelFormat.Format32bppArgb);
                using Graphics graphics = Graphics.FromImage(resized);
                graphics.DrawImageUnscaled(layer.Bitmap, Point.Empty);
                resizedLayers.Add(resized);
            }
        }
        catch
        {
            foreach (Bitmap bitmap in resizedLayers) bitmap.Dispose();
            throw;
        }
        if (saveUndo) SaveUndoState();
        for (int i = 0; i < currentDocument.Layers.Count; i++)
        {
            currentDocument.Layers[i].Bitmap.Dispose();
            currentDocument.Layers[i].Bitmap = resizedLayers[i];
        }
        ClearSelection();
        UpdateCanvasLayout();
        canvasPictureBox.Invalidate();
    }

    private CanvasResizeMode HitTestCanvasResizeHandle(Point displayPoint)
    {
        int radius = Math.Max(8, (int)Math.Round(10 * DeviceDpi / 96f));
        Size display = canvasPictureBox.ClientSize;
        Point right = new(display.Width - 1, display.Height / 2);
        Point bottom = new(display.Width / 2, display.Height - 1);
        Point corner = new(display.Width - 1, display.Height - 1);
        bool Near(Point point) => Math.Abs(displayPoint.X - point.X) <= radius
            && Math.Abs(displayPoint.Y - point.Y) <= radius;
        if (Near(corner)) return CanvasResizeMode.Both;
        if (Near(right)) return CanvasResizeMode.Width;
        if (Near(bottom)) return CanvasResizeMode.Height;
        return CanvasResizeMode.None;
    }

    private bool BeginCanvasResize(Point displayPoint)
    {
        CanvasResizeMode mode = HitTestCanvasResizeHandle(displayPoint);
        if (mode == CanvasResizeMode.None) return false;
        FinishInlineText(true);
        canvasResizeMode = mode;
        canvasResizeStartSize = canvasBitmap.Size;
        canvasResizePreviewSize = canvasBitmap.Size;
        canvasPictureBox.Capture = true;
        return true;
    }

    private void UpdateCanvasResize(Point displayPoint)
    {
        if (canvasResizeMode == CanvasResizeMode.None) return;
        const int maximum = 16384;
        Point canvasPoint = ToCanvasPoint(displayPoint);
        int width = canvasResizeMode is CanvasResizeMode.Width or CanvasResizeMode.Both
            ? Math.Clamp(canvasPoint.X + 1, canvasResizeStartSize.Width, maximum)
            : canvasResizeStartSize.Width;
        int height = canvasResizeMode is CanvasResizeMode.Height or CanvasResizeMode.Both
            ? Math.Clamp(canvasPoint.Y + 1, canvasResizeStartSize.Height, maximum)
            : canvasResizeStartSize.Height;
        canvasResizePreviewSize = new Size(width, height);
        toolStatusLabel.Text = $"Новый размер холста: {width} × {height} px";
    }

    private void FinishCanvasResize()
    {
        Size size = canvasResizePreviewSize;
        canvasResizeMode = CanvasResizeMode.None;
        canvasPictureBox.Capture = false;
        ResizeAllLayers(size, true);
        UpdateStatus();
    }

    private void CancelCanvasResize()
    {
        canvasResizeMode = CanvasResizeMode.None;
        canvasResizePreviewSize = Size.Empty;
        UpdateStatus();
    }

    private void DrawCanvasResizeHandles(Graphics graphics)
    {
        if (pastedBitmap != null || canvasResizeMode != CanvasResizeMode.None) return;
        float size = Math.Max(5, 9 / ZoomFactor);
        float right = canvasBitmap.Width - 1;
        float bottom = canvasBitmap.Height - 1;
        using Brush brush = new SolidBrush(UiTheme.AccentBorder);
        graphics.FillPolygon(brush, new[]
        {
            new PointF(right - size, canvasBitmap.Height / 2f - size),
            new PointF(right, canvasBitmap.Height / 2f),
            new PointF(right - size, canvasBitmap.Height / 2f + size)
        });
        graphics.FillPolygon(brush, new[]
        {
            new PointF(canvasBitmap.Width / 2f - size, bottom - size),
            new PointF(canvasBitmap.Width / 2f, bottom),
            new PointF(canvasBitmap.Width / 2f + size, bottom - size)
        });
        graphics.FillPolygon(brush, new[]
        {
            new PointF(right - size, bottom), new PointF(right, bottom - size), new PointF(right, bottom)
        });
    }

    private static Bitmap RenderLayers(DrawingDocument document)
    {
        Size size = document.Layers[0].Bitmap.Size;
        Bitmap result = new(size.Width, size.Height, PixelFormat.Format32bppArgb);
        using Graphics graphics = Graphics.FromImage(result);
        graphics.Clear(Color.White);
        foreach (CanvasLayer layer in document.Layers)
            graphics.DrawImageUnscaled(layer.Bitmap, Point.Empty);
        return result;
    }
}
