using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace Paint;

public partial class Form1
{
    private static bool IsShapeTool(ToolType tool) => tool >= ToolType.Line && tool <= ToolType.LeftArrow;

    private static string ShapeName(ToolType tool) => tool switch
    {
        ToolType.Line => "Линия",
        ToolType.Rectangle => "Прямоугольник",
        ToolType.Ellipse => "Эллипс",
        ToolType.RoundedRectangle => "Скруглённый прямоугольник",
        ToolType.Triangle => "Треугольник",
        ToolType.RightTriangle => "Прямоугольный треугольник",
        ToolType.Diamond => "Ромб",
        ToolType.Pentagon => "Пятиугольник",
        ToolType.Hexagon => "Шестиугольник",
        ToolType.Star => "Звезда",
        ToolType.RightArrow => "Стрелка вправо",
        ToolType.LeftArrow => "Стрелка влево",
        _ => "Фигуры"
    };

    private void TogglePixelGrid()
    {
        showPixelGrid = !showPixelGrid;
        gridMenuItem.Checked = showPixelGrid;
        UpdateGridStatus();
        canvasPictureBox.Invalidate();
    }

    private void UpdateGridStatus() => zoomStatusLabel.Text =
        $"Масштаб: {ZoomLevels[currentDocument.ZoomIndex]}%" +
        (showPixelGrid ? ZoomFactor >= 4 ? " | Сетка включена" : " | Сетка видна от 400%" : "");

    private bool IsPointInSelection(Point point)
    {
        if (currentDocument.Selection.IsEmpty) return false;
        return currentDocument.SelectionPath?.IsVisible(point) ?? currentDocument.Selection.Contains(point);
    }

    private void CropSelection_Click(object? sender, EventArgs e)
    {
        FinishDrawing();
        if (currentDocument.Selection.IsEmpty) return;
        Rectangle bounds = currentDocument.Selection;
        Bitmap cropped = new(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
        using (Graphics graphics = Graphics.FromImage(cropped))
        {
            graphics.Clear(Color.White);
            graphics.TranslateTransform(-bounds.X, -bounds.Y);
            if (currentDocument.SelectionPath != null) graphics.SetClip(currentDocument.SelectionPath);
            graphics.DrawImageUnscaled(canvasBitmap, Point.Empty);
        }
        SaveUndoState();
        ReplaceCanvas(cropped);
    }

    private void DrawBrushPreview(Graphics graphics)
    {
        if (currentTool is not (ToolType.Pencil or ToolType.Eraser) || isDrawing) return;
        Point clientPoint = canvasPictureBox.PointToClient(Cursor.Position);
        if (!canvasPictureBox.ClientRectangle.Contains(clientPoint)) return;
        Point canvasPoint = ToCanvasPoint(clientPoint);
        using Pen pen = new(Color.Black, 1 / ZoomFactor) { DashStyle = DashStyle.Dash };
        float radius = brushWidth / 2f;
        graphics.DrawEllipse(pen, canvasPoint.X - radius, canvasPoint.Y - radius, brushWidth, brushWidth);
    }

    private void DrawPixelGrid(Graphics graphics, Rectangle visible)
    {
        int left = Math.Max(0, (int)(visible.Left / ZoomFactor));
        int top = Math.Max(0, (int)(visible.Top / ZoomFactor));
        int right = Math.Min(canvasBitmap.Width, (int)Math.Ceiling(visible.Right / ZoomFactor));
        int bottom = Math.Min(canvasBitmap.Height, (int)Math.Ceiling(visible.Bottom / ZoomFactor));
        using Pen pen = new(Color.FromArgb(90, Color.Gray));
        for (int x = left; x <= right; x++)
        {
            int position = (int)Math.Round(x * ZoomFactor);
            graphics.DrawLine(pen, position, top * ZoomFactor, position, bottom * ZoomFactor);
        }
        for (int y = top; y <= bottom; y++)
        {
            int position = (int)Math.Round(y * ZoomFactor);
            graphics.DrawLine(pen, left * ZoomFactor, position, right * ZoomFactor, position);
        }
    }

    private static PointF[] RegularPolygon(Rectangle bounds, int sides)
    {
        PointF[] points = new PointF[sides];
        for (int i = 0; i < sides; i++)
        {
            double angle = -Math.PI / 2 + 2 * Math.PI * i / sides;
            points[i] = new PointF(
                (float)(bounds.X + bounds.Width * (.5 + .5 * Math.Cos(angle))),
                (float)(bounds.Y + bounds.Height * (.5 + .5 * Math.Sin(angle))));
        }
        return points;
    }

    private static PointF[] StarPoints(Rectangle bounds)
    {
        PointF[] points = new PointF[10];
        for (int i = 0; i < points.Length; i++)
        {
            double angle = -Math.PI / 2 + i * Math.PI / 5;
            double radius = i % 2 == 0 ? .5 : .225;
            points[i] = new PointF(
                (float)(bounds.X + bounds.Width * (.5 + radius * Math.Cos(angle))),
                (float)(bounds.Y + bounds.Height * (.5 + radius * Math.Sin(angle))));
        }
        return points;
    }
}
