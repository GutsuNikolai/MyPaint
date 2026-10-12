using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Paint;

public partial class Form1
{
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
        menu.DropDownItems.AddRange(new ToolStripItem[]
        {
            copyImageMenuItem, cutImageMenuItem,
            new ToolStripMenuItem("Paste", null, PasteImage_Click) { ShortcutKeyDisplayString = "Ctrl+V" },
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
        FinishDrawing();
        Rectangle bounds = currentDocument.Selection;
        if (bounds.IsEmpty) return;
        try
        {
            using Bitmap image = canvasBitmap.Clone(bounds, PixelFormat.Format32bppArgb);
            using MemoryStream png = new();
            image.Save(png, ImageFormat.Png);
            png.Position = 0;
            DataObject data = new();
            data.SetData(DataFormats.Bitmap, image);
            data.SetData("PNG", png);
            // Persist both formats before disposing the local image and stream.
            Clipboard.SetDataObject(data, true);
            if (cut) PaintSelection(Color.White);
        }
        catch (ExternalException exception)
        {
            ShowClipboardError(exception);
        }
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
            canvasPictureBox.Focus();
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
        using Bitmap image = pastedBitmap;
        pastedBitmap = null;
        isDraggingPaste = false;
        canvasPictureBox.Capture = false;
        if (commit)
        {
            SaveUndoState();
            using Graphics graphics = Graphics.FromImage(canvasBitmap);
            graphics.DrawImageUnscaled(image, pasteBounds.Location);
            currentDocument.Selection = pasteBounds;
        }
        canvasPictureBox.Cursor = Cursors.Cross;
        UpdateSelectionButtons();
        UpdateHistoryButtons();
        UpdateStatus();
        canvasPictureBox.Invalidate();
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
        FinishDrawing();
        Rectangle selection = currentDocument.Selection;
        using Bitmap image = selection.IsEmpty ? (Bitmap)canvasBitmap.Clone()
            : canvasBitmap.Clone(selection, PixelFormat.Format32bppArgb);
        image.RotateFlip(transform);
        if (selection.IsEmpty)
        {
            SaveUndoState();
            ReplaceCanvas((Bitmap)image.Clone());
        }
        else
        {
            using Bitmap fitted = CreateFittedBitmap(image);
            Rectangle destination = new(ClampImageLocation(selection.Location, fitted.Size), fitted.Size);
            SaveUndoState();
            using Graphics graphics = Graphics.FromImage(canvasBitmap);
            graphics.FillRectangle(Brushes.White, selection);
            graphics.DrawImageUnscaled(fitted, destination.Location);
            currentDocument.Selection = destination;
            UpdateSelectionButtons();
            canvasPictureBox.Invalidate();
        }
    }

    private Point ConstrainShapePoint(Point first, Point second)
    {
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
        if (e.KeyCode == Keys.ShiftKey && isDrawing && currentTool is ToolType.Rectangle or ToolType.Ellipse)
        {
            constrainShape = true;
            canvasPictureBox.Invalidate();
        }
        base.OnKeyDown(e);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.ShiftKey && isDrawing && currentTool is ToolType.Rectangle or ToolType.Ellipse)
        {
            constrainShape = false;
            canvasPictureBox.Invalidate();
        }
        base.OnKeyUp(e);
    }
}
