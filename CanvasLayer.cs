namespace Paint;

internal sealed class CanvasLayer : IDisposable
{
    public string Name { get; set; }
    public Bitmap Bitmap { get; set; }

    public CanvasLayer(string name, Bitmap bitmap)
    {
        Name = name;
        Bitmap = bitmap;
    }

    public CanvasLayer Clone() => new(Name, (Bitmap)Bitmap.Clone());

    public void Dispose() => Bitmap.Dispose();
}

internal sealed class DrawingState : IDisposable
{
    public List<CanvasLayer> Layers { get; }
    public int ActiveLayerIndex { get; }

    public DrawingState(IEnumerable<CanvasLayer> layers, int activeLayerIndex)
    {
        Layers = layers.Select(layer => layer.Clone()).ToList();
        ActiveLayerIndex = activeLayerIndex;
    }

    public void Dispose()
    {
        foreach (CanvasLayer layer in Layers) layer.Dispose();
        Layers.Clear();
    }
}
