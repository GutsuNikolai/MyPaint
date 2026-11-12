using System.Drawing.Imaging;

namespace Paint;

public partial class Form1
{
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
        layersMenuItem.DropDownItems.AddRange(new ToolStripItem[]
        {
            add, delete, new ToolStripSeparator(), mergeDown, mergeUp, new ToolStripSeparator()
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
