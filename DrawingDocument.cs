using System.Drawing.Drawing2D;

namespace Paint;

// Each tab owns its image, history and save location.
internal sealed class DrawingDocument : IDisposable
{
    public int Number { get; }
    public List<CanvasLayer> Layers { get; } = new();
    public int ActiveLayerIndex { get; set; }
    public Bitmap Bitmap
    {
        get => Layers[ActiveLayerIndex].Bitmap;
        set => Layers[ActiveLayerIndex].Bitmap = value;
    }
    public string? FilePath { get; set; }
    public Rectangle Selection { get; set; }
    public GraphicsPath? SelectionPath { get; set; }
    public int ZoomIndex { get; set; } = 4; // 100% in the zoom level list.
    // History remembers versions so Undo can return to the saved state.
    public long CurrentVersion { get; set; }
    public long SavedVersion { get; set; }
    public long LastVersion { get; set; }
    public bool HasUnsavedChanges => CurrentVersion != SavedVersion;
    public Stack<DrawingState> UndoStack { get; } = new();
    public Stack<DrawingState> RedoStack { get; } = new();
    public Stack<long> UndoVersions { get; } = new();
    public Stack<long> RedoVersions { get; } = new();

    public DrawingDocument(int number, Bitmap bitmap, string? filePath = null)
    {
        Number = number;
        Layers.Add(new CanvasLayer("Layer 1", bitmap));
        FilePath = filePath;
    }

    public void Dispose()
    {
        foreach (CanvasLayer layer in Layers) layer.Dispose();
        Layers.Clear();
        SelectionPath?.Dispose();
        while (UndoStack.Count > 0) UndoStack.Pop().Dispose();
        while (RedoStack.Count > 0) RedoStack.Pop().Dispose();
        UndoVersions.Clear();
        RedoVersions.Clear();
    }

    public DrawingState CaptureState() => new(Layers, ActiveLayerIndex);

    public void RestoreState(DrawingState state)
    {
        foreach (CanvasLayer layer in Layers) layer.Dispose();
        Layers.Clear();
        Layers.AddRange(state.Layers);
        ActiveLayerIndex = Math.Clamp(state.ActiveLayerIndex, 0, Layers.Count - 1);
        state.Layers.Clear();
    }
}
