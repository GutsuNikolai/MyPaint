using System.Drawing.Drawing2D;

namespace Paint;

// Each tab owns its image, history and save location.
internal sealed class DrawingDocument : IDisposable
{
    public int Number { get; }
    public Bitmap Bitmap { get; set; }
    public string? FilePath { get; set; }
    public Rectangle Selection { get; set; }
    public GraphicsPath? SelectionPath { get; set; }
    public List<CanvasObject> Objects { get; } = new();
    public Stack<List<CanvasObject>> UndoObjects { get; } = new();
    public Stack<List<CanvasObject>> RedoObjects { get; } = new();
    public int ZoomIndex { get; set; } = 4; // 100% in the zoom level list.
    // History remembers versions so Undo can return to the saved state.
    public long CurrentVersion { get; set; }
    public long SavedVersion { get; set; }
    public long LastVersion { get; set; }
    public bool HasUnsavedChanges => CurrentVersion != SavedVersion;
    public Stack<Bitmap> UndoStack { get; } = new();
    public Stack<Bitmap> RedoStack { get; } = new();
    public Stack<long> UndoVersions { get; } = new();
    public Stack<long> RedoVersions { get; } = new();

    public DrawingDocument(int number, Bitmap bitmap, string? filePath = null)
    {
        Number = number;
        Bitmap = bitmap;
        FilePath = filePath;
    }

    public void Dispose()
    {
        Bitmap.Dispose();
        SelectionPath?.Dispose();
        DisposeObjects(Objects);
        while (UndoObjects.Count > 0) DisposeObjects(UndoObjects.Pop());
        while (RedoObjects.Count > 0) DisposeObjects(RedoObjects.Pop());
        while (UndoStack.Count > 0) UndoStack.Pop().Dispose();
        while (RedoStack.Count > 0) RedoStack.Pop().Dispose();
        UndoVersions.Clear();
        RedoVersions.Clear();
    }

    private static void DisposeObjects(IEnumerable<CanvasObject> objects)
    {
        foreach (CanvasObject item in objects) item.Dispose();
    }
}
