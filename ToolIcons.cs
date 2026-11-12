using System.Drawing.Drawing2D;

namespace Paint;

// Small vector drawings keep icons sharp without an external icon library.
internal static class ToolIcons
{
    public static Bitmap Create(string name)
    {
        Bitmap image = new(48, 48);
        using Graphics g = Graphics.FromImage(image);
        g.ScaleTransform(2, 2);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using Pen pen = new(UiTheme.Text, 1.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        using Brush accent = new SolidBrush(UiTheme.Accent);
        switch (name)
        {
            case "Mouse":
                PointF[] pointer = { new(5, 3), new(5, 20), new(10, 15), new(14, 22), new(17, 20), new(13, 13), new(20, 13) };
                g.FillPolygon(accent, pointer); g.DrawPolygon(pen, pointer); break;
            case "Pencil":
                PointF[] pencil = { new(4, 16), new(15, 5), new(19, 9), new(8, 20), new(3, 21) };
                g.FillPolygon(accent, pencil); g.DrawPolygon(pen, pencil);
                g.DrawLine(pen, 13, 7, 17, 11); g.DrawLine(pen, 4, 16, 8, 20); break;
            case "Eraser":
                PointF[] eraser = { new(3, 14), new(12, 5), new(21, 14), new(14, 21), new(10, 21) };
                g.FillPolygon(accent, eraser); g.DrawPolygon(pen, eraser);
                g.DrawLine(pen, 8, 9, 17, 18); g.DrawLine(pen, 5, 21, 21, 21); break;
            case "Line":
                g.DrawLine(pen, 4, 20, 20, 4); g.FillEllipse(accent, 2, 18, 4, 4); g.DrawEllipse(pen, 2, 18, 4, 4); g.DrawEllipse(pen, 18, 2, 4, 4); break;
            case "Rectangle": g.FillRectangle(accent, 4, 6, 16, 13); g.DrawRectangle(pen, 4, 6, 16, 13); break;
            case "Ellipse": g.FillEllipse(accent, 3, 5, 18, 15); g.DrawEllipse(pen, 3, 5, 18, 15); break;
            case "RoundedRectangle":
                using (GraphicsPath rounded = UiTheme.RoundedRectangle(new Rectangle(4, 5, 16, 14), 4))
                { g.FillPath(accent, rounded); g.DrawPath(pen, rounded); }
                break;
            case "Triangle":
                PointF[] triangle = { new(12, 3), new(22, 20), new(2, 20) };
                g.FillPolygon(accent, triangle); g.DrawPolygon(pen, triangle); break;
            case "RightTriangle":
                PointF[] rightTriangle = { new(3, 3), new(3, 21), new(21, 21) };
                g.FillPolygon(accent, rightTriangle); g.DrawPolygon(pen, rightTriangle); break;
            case "Diamond":
                PointF[] diamond = { new(12, 2), new(22, 12), new(12, 22), new(2, 12) };
                g.FillPolygon(accent, diamond); g.DrawPolygon(pen, diamond); break;
            case "Pentagon":
            case "Hexagon":
                int sides = name == "Pentagon" ? 5 : 6;
                PointF[] polygon = new PointF[sides];
                for (int i = 0; i < sides; i++)
                {
                    double angle = -Math.PI / 2 + 2 * Math.PI * i / sides;
                    polygon[i] = new PointF((float)(12 + 10 * Math.Cos(angle)), (float)(12 + 10 * Math.Sin(angle)));
                }
                g.FillPolygon(accent, polygon); g.DrawPolygon(pen, polygon); break;
            case "Star":
                PointF[] star = new PointF[10];
                for (int i = 0; i < star.Length; i++)
                {
                    double angle = -Math.PI / 2 + i * Math.PI / 5;
                    double radius = i % 2 == 0 ? 10 : 4.5;
                    star[i] = new PointF((float)(12 + radius * Math.Cos(angle)), (float)(12 + radius * Math.Sin(angle)));
                }
                g.FillPolygon(accent, star); g.DrawPolygon(pen, star); break;
            case "RightArrow":
            case "LeftArrow":
                PointF[] arrow = { new(2, 9), new(13, 9), new(13, 3), new(22, 12), new(13, 21), new(13, 15), new(2, 15) };
                if (name == "LeftArrow") for (int i = 0; i < arrow.Length; i++) arrow[i].X = 24 - arrow[i].X;
                g.FillPolygon(accent, arrow); g.DrawPolygon(pen, arrow); break;
            case "Text": g.DrawLine(pen, 4, 5, 20, 5); g.DrawLine(pen, 12, 5, 12, 20); g.DrawLine(pen, 8, 20, 16, 20); break;
            case "Fill":
                PointF[] bucket = { new(4, 11), new(11, 4), new(19, 12), new(12, 19) };
                g.FillPolygon(accent, bucket); g.DrawPolygon(pen, bucket); g.DrawLine(pen, 6, 11, 17, 11);
                g.DrawLine(pen, 9, 3, 14, 8); g.FillEllipse(accent, 18, 17, 4, 5); g.DrawEllipse(pen, 18, 17, 4, 5); break;
            case "Selection": pen.DashStyle = DashStyle.Dash; g.DrawRectangle(pen, 3, 4, 18, 16); break;
            case "Lasso":
                pen.DashStyle = DashStyle.Dash; g.DrawEllipse(pen, 4, 4, 16, 14);
                pen.DashStyle = DashStyle.Solid; g.DrawLines(pen, new PointF[] { new(8, 17), new(5, 21), new(3, 23) }); break;
            case "Eyedropper":
                PointF[] pipette = { new(4, 17), new(13, 8), new(17, 12), new(8, 21), new(3, 22) };
                g.FillPolygon(accent, pipette); g.DrawPolygon(pen, pipette);
                g.DrawLine(pen, 11, 7, 18, 14); g.DrawLine(pen, 14, 6, 18, 2);
                g.DrawArc(pen, 15, 2, 7, 7, 225, 180); break;
            case "Clear":
                g.DrawRectangle(pen, 6, 7, 12, 14); g.DrawLine(pen, 4, 7, 20, 7); g.DrawLine(pen, 9, 3, 15, 3); g.DrawLine(pen, 10, 11, 10, 17); g.DrawLine(pen, 14, 11, 14, 17); break;
            case "Erase Selection":
                pen.DashStyle = DashStyle.Dash; g.DrawRectangle(pen, 3, 4, 18, 16); pen.DashStyle = DashStyle.Solid; g.DrawLine(pen, 8, 8, 16, 16); g.DrawLine(pen, 16, 8, 8, 16); break;
            case "Crop":
                g.DrawLine(pen, 6, 2, 6, 18); g.DrawLine(pen, 6, 18, 22, 18);
                g.DrawLine(pen, 2, 6, 18, 6); g.DrawLine(pen, 18, 6, 18, 22); break;
            case "Undo":
            case "Redo":
                if (name == "Redo") { g.TranslateTransform(24, 0); g.ScaleTransform(-1, 1); }
                g.DrawLines(pen, new PointF[] { new(9, 4), new(4, 9), new(9, 14) });
                g.DrawLine(pen, 4, 9, 14, 9); g.DrawArc(pen, 9, 9, 12, 12, -90, 180); break;
        }
        return image;
    }
}
