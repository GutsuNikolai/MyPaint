using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Paint;

public partial class Form1
{
    private enum PasteDragMode
    {
        None, Move, ResizeNorthWest, ResizeNorthEast, ResizeSouthWest, ResizeSouthEast, Crop
    }

    private ContextMenuStrip CreateCanvasContextMenu()
    {
        ContextMenuStrip menu = new(components!)
        {
            Renderer = new PageMenuRenderer(), BackColor = UiTheme.Surface, Font = Font
        };
        ToolStripMenuItem cut = new("Cut", null, (_, _) => CopyImage(true)) { ShortcutKeyDisplayString = "Ctrl+X" };
        ToolStripMenuItem copy = new("Copy", null, (_, _) => CopyImage(false)) { ShortcutKeyDisplayString = "Ctrl+C" };
        ToolStripMenuItem paste = new("Paste", null, PasteImage_Click) { ShortcutKeyDisplayString = "Ctrl+V" };
        ToolStripMenuItem delete = new("Delete", null, DeleteCanvasContent) { ShortcutKeyDisplayString = "Del" };
        ToolStripMenuItem crop = new("Crop inserted image", null, BeginPasteCrop);
        bringForwardMenuItem = new ToolStripMenuItem("Move Up", null, (_, _) => MoveSelectedObject(1)) { Enabled = false };
        sendBackwardMenuItem = new ToolStripMenuItem("Move Down", null, (_, _) => MoveSelectedObject(-1)) { Enabled = false };
        menu.Items.AddRange(new ToolStripItem[]
        {
            cut, copy, paste, delete, new ToolStripSeparator(), crop,
            new ToolStripSeparator(), bringForwardMenuItem, sendBackwardMenuItem
        });
        menu.Opening += (_, _) =>
        {
            Point client = canvasPictureBox.PointToClient(Cursor.Position);
            if (canvasPictureBox.ClientRectangle.Contains(client) && pastedBitmap == null)
            {
                CanvasObject? hit = HitTestObject(ToCanvasPoint(client));
                selectedObject = hit;
                canvasPictureBox.Invalidate();
            }
            bool hasContent = pastedBitmap != null || selectedObject != null || !currentDocument.Selection.IsEmpty;
            cut.Enabled = copy.Enabled = delete.Enabled = hasContent;
            crop.Enabled = pastedBitmap != null;
            UpdateObjectCommands();
            try { paste.Enabled = Clipboard.ContainsImage() || Clipboard.ContainsFileDropList()
                    || Clipboard.GetDataObject()?.GetDataPresent("PNG") == true; }
            catch (ExternalException) { paste.Enabled = true; }
        };
        return menu;
    }

    private void DeleteCanvasContent(object? sender, EventArgs e)
    {
        if (pastedBitmap != null) FinishPaste(false);
        else if (selectedObject != null) DeleteSelectedObject();
        else EraseSelection_Click(sender, e);
    }

    private void BeginPasteCrop(object? sender, EventArgs e)
    {
        if (pastedBitmap == null) return;
        isCroppingPaste = true;
        pasteCropBounds = Rectangle.Empty;
        toolStatusLabel.Text = "Crop: выделите нужную часть вставленного изображения; Esc — отменить crop";
        canvasPictureBox.Cursor = Cursors.Cross;
        canvasPictureBox.Invalidate();
    }

    private void CancelPasteCrop()
    {
        isCroppingPaste = false;
        pasteCropBounds = Rectangle.Empty;
        pasteDragMode = PasteDragMode.None;
        isDraggingPaste = false;
        canvasPictureBox.Capture = false;
        UpdateStatus();
        canvasPictureBox.Invalidate();
    }

    private ToolStripMenuItem CreateImageMenu()
    {
        ToolStripMenuItem menu = new("Image");
        copyImageMenuItem = new("Copy", null, (_, _) => CopyImage(false))
        {
            ShortcutKeyDisplayString = "Ctrl+C", Enabled = false
        };
        cutImageMenuItem = new("Cut", null, (_, _) => CopyImage(true))
        {
            ShortcutKeyDisplayString = "Ctrl+X", Enabled = false
        };
        cropMenuItem = new ToolStripMenuItem("Crop selection", null, CropSelection_Click) { Enabled = false };
        menu.DropDownItems.AddRange(new ToolStripItem[]
        {
            copyImageMenuItem, cutImageMenuItem,
            new ToolStripMenuItem("Paste", null, PasteImage_Click) { ShortcutKeyDisplayString = "Ctrl+V" },
            cropMenuItem,
            new ToolStripSeparator(),
            new ToolStripMenuItem("Rotate 90° clockwise", null, (_, _) => TransformImage(RotateFlipType.Rotate90FlipNone)),
            new ToolStripMenuItem("Rotate 90° counterclockwise", null, (_, _) => TransformImage(RotateFlipType.Rotate270FlipNone)),
            new ToolStripMenuItem("Flip horizontal", null, (_, _) => TransformImage(RotateFlipType.RotateNoneFlipX)),
            new ToolStripMenuItem("Flip vertical", null, (_, _) => TransformImage(RotateFlipType.RotateNoneFlipY))
        });
        menu.ToolTipText = "Поворот и отражение: выделенная область или весь холст";
        menu.DropDown.Renderer = new PageMenuRenderer();
        menu.DropDown.BackColor = UiTheme.Surface;
        return menu;
    }

    private void CopyImage(bool cut)
    {
        if (inlineTextEditor != null)
        {
            if (cut) inlineTextEditor.Cut();
            else inlineTextEditor.Copy();
            inlineTextEditor.Focus();
            return;
        }
        if (pastedBitmap != null)
        {
            try
            {
                using Bitmap image = RenderPastedBitmap();
                PutBitmapOnClipboard(image);
                if (cut) FinishPaste(false);
            }
            catch (ExternalException exception) { ShowClipboardError(exception); }
            return;
        }
        if (selectedObject != null && currentDocument.Objects.Contains(selectedObject))
        {
            try
            {
                using Bitmap image = (Bitmap)selectedObject.Image.Clone();
                PutBitmapOnClipboard(image);
                if (cut) DeleteSelectedObject();
            }
            catch (ExternalException exception) { ShowClipboardError(exception); }
            return;
        }
        FinishDrawing();
        Rectangle bounds = currentDocument.Selection;
        if (bounds.IsEmpty) return;
        try
        {
            using Bitmap image = CopySelectedPixels(bounds);
            PutBitmapOnClipboard(image);
            if (cut) PaintSelection(Color.White);
        }
        catch (ExternalException exception)
        {
            ShowClipboardError(exception);
        }
    }

    private Bitmap CopySelectedPixels(Rectangle bounds)
    {
        using Bitmap composite = RenderComposite(currentDocument);
        if (currentDocument.SelectionPath == null)
            return composite.Clone(bounds, PixelFormat.Format32bppArgb);
        Bitmap image = new(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
        using Graphics graphics = Graphics.FromImage(image);
        graphics.TranslateTransform(-bounds.X, -bounds.Y);
        graphics.SetClip(currentDocument.SelectionPath);
        graphics.DrawImageUnscaled(composite, Point.Empty);
        return image;
    }

    private static void PutBitmapOnClipboard(Bitmap image)
    {
        using MemoryStream png = new();
        image.Save(png, ImageFormat.Png);
        png.Position = 0;
        DataObject data = new();
        data.SetData(DataFormats.Bitmap, image);
        data.SetData("PNG", png);
        Clipboard.SetDataObject(data, true);
    }

    private Bitmap RenderPastedBitmap()
    {
        Bitmap rendered = new(pasteBounds.Width, pasteBounds.Height, PixelFormat.Format32bppArgb);
        using Graphics graphics = Graphics.FromImage(rendered);
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.DrawImage(pastedBitmap!, new Rectangle(Point.Empty, rendered.Size));
        return rendered;
    }

    private static Image? ReadClipboardImage()
    {
        IDataObject? data = Clipboard.GetDataObject();
        if (data?.GetData("PNG") is Stream png)
        {
            using (png)
            {
                if (png.CanSeek) png.Position = 0;
                using Image image = Image.FromStream(png);
                return new Bitmap(image);
            }
        }
        if (Clipboard.ContainsImage()) return Clipboard.GetImage();
        if (Clipboard.ContainsFileDropList())
        {
            foreach (string? file in Clipboard.GetFileDropList())
            {
                if (string.IsNullOrEmpty(file)) continue;
                string extension = Path.GetExtension(file).ToLowerInvariant();
                if (extension is not (".png" or ".jpg" or ".jpeg" or ".bmp")) continue;
                using Image image = Image.FromFile(file);
                return new Bitmap(image);
            }
        }
        return null;
    }

    private void PasteImage_Click(object? sender, EventArgs e)
    {
        if (inlineTextEditor != null)
        {
            inlineTextEditor.Paste();
            inlineTextEditor.Focus();
            return;
        }
        try
        {
            using Image? image = ReadClipboardImage();
            if (image == null)
            {
                MessageBox.Show(this, "В буфере обмена нет изображения.", "MyPaint",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            FinishDrawing();
            SelectTool(ToolType.Selection);
            ClearSelection();
            pastedBitmap = CreateFittedBitmap(image);
            Point visibleCorner = new(
                Math.Max(0, (int)Math.Ceiling(-canvasPictureBox.Left / ZoomFactor)),
                Math.Max(0, (int)Math.Ceiling(-canvasPictureBox.Top / ZoomFactor)));
            pasteBounds = new Rectangle(ClampImageLocation(visibleCorner, pastedBitmap.Size), pastedBitmap.Size);
            UpdateSelectionButtons();
            UpdateHistoryButtons();
            UpdateStatus();
            canvasPictureBox.Invalidate();
        }
        catch (Exception exception) when (exception is ExternalException or ArgumentException
            or IOException or OutOfMemoryException or UnauthorizedAccessException)
        {
            ShowClipboardError(exception);
        }
    }

    private void ShowClipboardError(Exception exception) => MessageBox.Show(this,
        $"Не удалось выполнить операцию с буфером обмена.\n{exception.Message}", "MyPaint",
        MessageBoxButtons.OK, MessageBoxIcon.Error);

    private Bitmap CreateFittedBitmap(Image image)
    {
        float scale = Math.Min(1f, Math.Min((float)canvasBitmap.Width / image.Width,
            (float)canvasBitmap.Height / image.Height));
        Size size = new(Math.Max(1, (int)(image.Width * scale)), Math.Max(1, (int)(image.Height * scale)));
        Bitmap bitmap = new(size.Width, size.Height, PixelFormat.Format32bppArgb);
        using Graphics graphics = Graphics.FromImage(bitmap);
        if (size == image.Size) graphics.DrawImageUnscaled(image, Point.Empty);
        else
        {
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            using ImageAttributes attributes = new();
            attributes.SetWrapMode(WrapMode.TileFlipXY);
            graphics.DrawImage(image, new Rectangle(Point.Empty, size), 0, 0,
                image.Width, image.Height, GraphicsUnit.Pixel, attributes);
        }
        return bitmap;
    }

    private Point ClampImageLocation(Point location, Size size) => new(
        Math.Clamp(location.X, 0, canvasBitmap.Width - size.Width),
        Math.Clamp(location.Y, 0, canvasBitmap.Height - size.Height));

    private void FinishPaste(bool commit)
    {
        if (pastedBitmap == null) return;
        Bitmap image = pastedBitmap;
        pastedBitmap = null;
        isDraggingPaste = false;
        isCroppingPaste = false;
        pasteDragMode = PasteDragMode.None;
        canvasPictureBox.Capture = false;
        if (commit)
        {
            SaveUndoState();
            AddObject(new CanvasObject("Изображение", image, pasteBounds));
        }
        else image.Dispose();
        canvasPictureBox.Cursor = Cursors.Cross;
        UpdateSelectionButtons();
        UpdateHistoryButtons();
        UpdateStatus();
        canvasPictureBox.Invalidate();
    }

    private PasteDragMode HitTestPasteHandle(Point point)
    {
        int radius = Math.Max(3, (int)Math.Ceiling(7 / ZoomFactor));
        if (Near(point, pasteBounds.Left, pasteBounds.Top, radius)) return PasteDragMode.ResizeNorthWest;
        if (Near(point, pasteBounds.Right, pasteBounds.Top, radius)) return PasteDragMode.ResizeNorthEast;
        if (Near(point, pasteBounds.Left, pasteBounds.Bottom, radius)) return PasteDragMode.ResizeSouthWest;
        if (Near(point, pasteBounds.Right, pasteBounds.Bottom, radius)) return PasteDragMode.ResizeSouthEast;
        return PasteDragMode.None;
    }

    private static bool Near(Point point, int x, int y, int radius) =>
        Math.Abs(point.X - x) <= radius && Math.Abs(point.Y - y) <= radius;

    private Cursor GetPasteCursor(Point point)
    {
        if (isCroppingPaste) return Cursors.Cross;
        return HitTestPasteHandle(point) switch
        {
            PasteDragMode.ResizeNorthWest or PasteDragMode.ResizeSouthEast => Cursors.SizeNWSE,
            PasteDragMode.ResizeNorthEast or PasteDragMode.ResizeSouthWest => Cursors.SizeNESW,
            _ => pasteBounds.Contains(point) ? Cursors.SizeAll : Cursors.Cross
        };
    }

    private Rectangle ResizePasteBounds(PasteDragMode mode, Point point)
    {
        const int minimum = 4;
        Rectangle original = pasteDragStartBounds;
        int left = original.Left, top = original.Top, right = original.Right, bottom = original.Bottom;
        if (mode is PasteDragMode.ResizeNorthWest or PasteDragMode.ResizeSouthWest)
            left = Math.Clamp(point.X, 0, right - minimum);
        else right = Math.Clamp(point.X, left + minimum, canvasBitmap.Width);
        if (mode is PasteDragMode.ResizeNorthWest or PasteDragMode.ResizeNorthEast)
            top = Math.Clamp(point.Y, 0, bottom - minimum);
        else bottom = Math.Clamp(point.Y, top + minimum, canvasBitmap.Height);
        return Rectangle.FromLTRB(left, top, right, bottom);
    }

    private Rectangle GetCropBounds(Point first, Point second)
    {
        first = ClampPointToRectangle(first, pasteBounds);
        second = ClampPointToRectangle(second, pasteBounds);
        return Rectangle.FromLTRB(Math.Min(first.X, second.X), Math.Min(first.Y, second.Y),
            Math.Max(first.X, second.X) + 1, Math.Max(first.Y, second.Y) + 1);
    }

    private static Point ClampPointToRectangle(Point point, Rectangle bounds) => new(
        Math.Clamp(point.X, bounds.Left, bounds.Right - 1),
        Math.Clamp(point.Y, bounds.Top, bounds.Bottom - 1));

    private void ApplyPasteCrop()
    {
        if (pastedBitmap == null || pasteCropBounds.Width < 2 || pasteCropBounds.Height < 2)
        {
            CancelPasteCrop();
            return;
        }
        Rectangle source = new(
            (int)Math.Floor((pasteCropBounds.Left - pasteBounds.Left) * pastedBitmap.Width / (double)pasteBounds.Width),
            (int)Math.Floor((pasteCropBounds.Top - pasteBounds.Top) * pastedBitmap.Height / (double)pasteBounds.Height),
            Math.Max(1, (int)Math.Ceiling(pasteCropBounds.Width * pastedBitmap.Width / (double)pasteBounds.Width)),
            Math.Max(1, (int)Math.Ceiling(pasteCropBounds.Height * pastedBitmap.Height / (double)pasteBounds.Height)));
        source.Intersect(new Rectangle(Point.Empty, pastedBitmap.Size));
        Bitmap cropped = pastedBitmap.Clone(source, PixelFormat.Format32bppArgb);
        pastedBitmap.Dispose();
        pastedBitmap = cropped;
        pasteBounds = pasteCropBounds;
        isCroppingPaste = false;
        pasteCropBounds = Rectangle.Empty;
        UpdateStatus();
        canvasPictureBox.Invalidate();
    }

    private void DrawPasteAdorners(Graphics graphics)
    {
        float size = Math.Max(4, 8 / ZoomFactor);
        using Brush handle = new SolidBrush(UiTheme.Surface);
        using Pen outline = new(UiTheme.AccentBorder, 1 / ZoomFactor);
        foreach (Point corner in new[]
        {
            new Point(pasteBounds.Left, pasteBounds.Top), new Point(pasteBounds.Right, pasteBounds.Top),
            new Point(pasteBounds.Left, pasteBounds.Bottom), new Point(pasteBounds.Right, pasteBounds.Bottom)
        })
        {
            RectangleF box = new(corner.X - size / 2, corner.Y - size / 2, size, size);
            graphics.FillRectangle(handle, box);
            graphics.DrawRectangle(outline, box.X, box.Y, box.Width, box.Height);
        }
        if (isCroppingPaste)
        {
            using Brush shade = new SolidBrush(Color.FromArgb(80, Color.Black));
            using Region outside = new(pasteBounds);
            if (!pasteCropBounds.IsEmpty) outside.Exclude(pasteCropBounds);
            graphics.FillRegion(shade, outside);
            if (!pasteCropBounds.IsEmpty)
            {
                using Pen cropPen = new(Color.White, 1 / ZoomFactor) { DashStyle = DashStyle.Dash };
                graphics.DrawRectangle(cropPen, pasteCropBounds.X, pasteCropBounds.Y,
                    pasteCropBounds.Width - 1, pasteCropBounds.Height - 1);
            }
        }
    }

    private void TransformImage(RotateFlipType transform)
    {
        if (pastedBitmap != null)
        {
            using Bitmap transformed = (Bitmap)pastedBitmap.Clone();
            transformed.RotateFlip(transform);
            Bitmap fitted = CreateFittedBitmap(transformed);
            pastedBitmap.Dispose();
            pastedBitmap = fitted;
            pasteBounds = new Rectangle(ClampImageLocation(pasteBounds.Location, fitted.Size), fitted.Size);
            canvasPictureBox.Invalidate();
            return;
        }
        if (selectedObject != null && currentDocument.Objects.Contains(selectedObject))
        {
            SaveUndoState();
            selectedObject.Image.RotateFlip(transform);
            if (transform is RotateFlipType.Rotate90FlipNone or RotateFlipType.Rotate270FlipNone
                or RotateFlipType.Rotate90FlipX or RotateFlipType.Rotate90FlipY
                or RotateFlipType.Rotate270FlipX or RotateFlipType.Rotate270FlipY)
            {
                Rectangle oldBounds = selectedObject.Bounds;
                Size size = new(oldBounds.Height, oldBounds.Width);
                selectedObject.Bounds = new Rectangle(ClampImageLocation(oldBounds.Location, size), size);
            }
            canvasPictureBox.Invalidate();
            return;
        }
        FinishDrawing();
        Rectangle selection = currentDocument.Selection;
        using Bitmap image = selection.IsEmpty ? RenderComposite(currentDocument) : CopySelectedPixels(selection);
        image.RotateFlip(transform);
        if (selection.IsEmpty)
        {
            SaveUndoState();
            DisposeObjects(currentDocument.Objects);
            currentDocument.Objects.Clear();
            ReplaceCanvas((Bitmap)image.Clone());
        }
        else
        {
            using Bitmap fitted = CreateFittedBitmap(image);
            Rectangle destination = new(ClampImageLocation(selection.Location, fitted.Size), fitted.Size);
            SaveUndoState();
            FlattenObjectsWithoutHistory();
            using Graphics graphics = Graphics.FromImage(canvasBitmap);
            if (currentDocument.SelectionPath != null) graphics.FillPath(Brushes.White, currentDocument.SelectionPath);
            else graphics.FillRectangle(Brushes.White, selection);
            graphics.DrawImageUnscaled(fitted, destination.Location);
            currentDocument.SelectionPath?.Dispose();
            currentDocument.SelectionPath = null;
            currentDocument.Selection = destination;
            UpdateSelectionButtons();
            canvasPictureBox.Invalidate();
        }
    }

    private Point ConstrainShapePoint(Point first, Point second)
    {
        int deltaX = second.X - first.X;
        int deltaY = second.Y - first.Y;
        if (currentTool == ToolType.Line)
        {
            double length = Math.Sqrt((double)deltaX * deltaX + (double)deltaY * deltaY);
            double angle = Math.Round(Math.Atan2(deltaY, deltaX) / (Math.PI / 12)) * (Math.PI / 12);
            return ClampPoint(new Point(
                (int)Math.Round(first.X + length * Math.Cos(angle)),
                (int)Math.Round(first.Y + length * Math.Sin(angle))));
        }
        int directionX = second.X < first.X ? -1 : 1;
        int directionY = second.Y < first.Y ? -1 : 1;
        int side = Math.Max(Math.Abs(second.X - first.X), Math.Abs(second.Y - first.Y));
        int availableX = directionX < 0 ? first.X : canvasBitmap.Width - 1 - first.X;
        int availableY = directionY < 0 ? first.Y : canvasBitmap.Height - 1 - first.Y;
        side = Math.Min(side, Math.Min(availableX, availableY));
        return new Point(first.X + side * directionX, first.Y + side * directionY);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.ShiftKey && isDrawing && IsShapeTool(currentTool))
        {
            constrainShape = true;
            canvasPictureBox.Invalidate();
        }
        base.OnKeyDown(e);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.ShiftKey && isDrawing && IsShapeTool(currentTool))
        {
            constrainShape = false;
            canvasPictureBox.Invalidate();
        }
        base.OnKeyUp(e);
    }
}
