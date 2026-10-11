using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace Paint;

public partial class Form1 : Form, IMessageFilter
{
    private const int HistoryLimit = 20;
    private static readonly int[] ZoomLevels =
    {
        10, 25, 50, 75, 100, 125, 150, 200,
        250, 300, 350, 400, 450, 500, 550, 600, 650, 700, 750, 800
    };
    private float ZoomFactor => ZoomLevels[currentDocument.ZoomIndex] / 100f;
    private int wheelRemainder;
    private AppSettings settings = AppSettings.Load();
    private readonly List<DrawingDocument> documents = new();
    private readonly Dictionary<DrawingDocument, ToolStripMenuItem> documentTabs = new();
    private DrawingDocument currentDocument = null!;
    private Bitmap canvasBitmap
    {
        get => currentDocument.Bitmap;
        set => currentDocument.Bitmap = value;
    }
    private Stack<Bitmap> undoStack => currentDocument.UndoStack;
    private Stack<Bitmap> redoStack => currentDocument.RedoStack;
    private readonly Dictionary<ToolType, ToolStripButton> toolButtons = new();
    private ToolType currentTool = ToolType.Pencil;
    private Color currentColor = Color.Black;
    private int brushWidth = 3;
    private bool isDrawing;
    private Point startPoint;
    private Point lastPoint;
    private Bitmap? movingBitmap;
    private Rectangle moveSource;
    private Rectangle moveDestination;
    private TextBox? inlineTextEditor;
    private Font? inlineTextFont;
    private Rectangle inlineTextBounds;
    private int textFontSize = 24;
    private readonly TextFonts textFonts = new();
    private string textFontName = "Segoe UI";
    private Color textColor = Color.Black;
    private Color textBackgroundColor = Color.White;
    private StringAlignment textHorizontalAlignment = StringAlignment.Near;
    private StringAlignment textVerticalAlignment = StringAlignment.Near;
    private Panel? inlineTextPanel;
    private bool textOptionsPositioned;
    private int lastDocumentNumber;
    private DrawingDocument? contextDocument;

    public Form1()
    {
        InitializeComponent();
        New_Click(this, EventArgs.Empty);
        SelectTool(ToolType.Pencil);
        UpdateStatus();
        UpdateHistoryButtons();
        Application.AddMessageFilter(this);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        // The maximized window has its final layout only after it is shown.
        Bitmap bitmap = CreateBlankCanvas();
        ReplaceCanvas(bitmap);
    }

    private Size GetWindowCanvasSize() => new(
        Math.Max(1, canvasPanel.Width), Math.Max(1, canvasPanel.Height));

    private Size GetNewCanvasSize() => settings.CanvasWidth == 0
        ? GetWindowCanvasSize() : new Size(settings.CanvasWidth, settings.CanvasHeight);

    private Bitmap CreateBlankCanvas()
    {
        Size size = GetNewCanvasSize();
        Bitmap bitmap = new(size.Width, size.Height, PixelFormat.Format32bppArgb);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.White);
        return bitmap;
    }

    private void New_Click(object? sender, EventArgs e)
    {
        AddDocument(CreateBlankCanvas());
    }

    private bool AddDocument(Bitmap bitmap, string? filePath = null, bool isCopy = false, bool confirmChanges = true)
    {
        FinishDrawing();
        if (confirmChanges && currentDocument != null && !ConfirmLeavingDocument(currentDocument))
        {
            bitmap.Dispose();
            return false;
        }
        DrawingDocument document = new(++lastDocumentNumber, bitmap, filePath);
        if (isCopy) document.CurrentVersion = document.LastVersion = 1;
        documents.Add(document);
        documentTabFont ??= new Font(MainMenuStrip!.Font.FontFamily,
            MainMenuStrip.Font.Size * 1.5f, MainMenuStrip.Font.Style, MainMenuStrip.Font.Unit);
        ToolStripMenuItem tab = new(document.Number.ToString()) { Font = documentTabFont, Tag = document };
        tab.Click += (_, _) =>
        {
            if (!pageContextMenu.Visible) SwitchDocument(document);
        };
        tab.MouseDown += (_, e) =>
        {
            if (e.Button != MouseButtons.Right) return;
            contextDocument = document;
            pageContextMenu.Show(tab.Owner!, new Point(tab.Bounds.X + e.X, tab.Bounds.Y + e.Y));
        };
        documentTabs.Add(document, tab);
        MainMenuStrip!.Items.Add(tab);
        ActivateDocument(document);
        return true;
    }

    private bool SwitchDocument(DrawingDocument document)
    {
        if (!documents.Contains(document)) return false;
        if (document == currentDocument) return true;
        FinishDrawing();
        if (currentDocument != null && !ConfirmLeavingDocument(currentDocument)) return false;
        ActivateDocument(document);
        return true;
    }

    private void ActivateDocument(DrawingDocument document)
    {
        currentDocument = document;
        wheelRemainder = 0;
        canvasPictureBox.Image = canvasBitmap;
        UpdateCanvasLayout();
        foreach (var pair in documentTabs)
            pair.Value.Checked = pair.Key == document;
        coordinatesStatusLabel.Text = "X: —, Y: —";
        canvasPictureBox.Invalidate();
        UpdateDocumentTitle();
        UpdateHistoryButtons();
        UpdateSelectionButtons();
    }

    public bool PreFilterMessage(ref Message message)
    {
        const int MouseWheelMessage = 0x020A;
        const int ControlKeyFlag = 0x0008;
        if (message.Msg != MouseWheelMessage || (message.WParam.ToInt64() & ControlKeyFlag) == 0 || !Visible) return false;
        Control? target = Control.FromHandle(message.HWnd);
        Form? targetForm = target?.FindForm();
        if (targetForm != this && targetForm != textOptionsWindow) return false;
        long position = message.LParam.ToInt64();
        Point screenPoint = new((short)(position & 0xffff), (short)((position >> 16) & 0xffff));
        if (!canvasPanel.RectangleToScreen(canvasPanel.ClientRectangle).Contains(screenPoint)) return false;
        int delta = (short)((message.WParam.ToInt64() >> 16) & 0xffff);
        ZoomCanvas(delta, canvasPanel.PointToClient(screenPoint));
        // Consume the wheel so Ctrl+wheel does not also scroll the canvas or text.
        return true;
    }

    private void ZoomCanvas(int delta, Point anchor)
    {
        if (isDrawing) return;
        wheelRemainder += delta;
        int steps = wheelRemainder / SystemInformation.MouseWheelScrollDelta;
        wheelRemainder %= SystemInformation.MouseWheelScrollDelta;
        int index = Math.Clamp(currentDocument.ZoomIndex + steps, 0, ZoomLevels.Length - 1);
        if (index == currentDocument.ZoomIndex) return;
        PointF canvasPoint = new((anchor.X - canvasPictureBox.Left) / ZoomFactor,
            (anchor.Y - canvasPictureBox.Top) / ZoomFactor);
        currentDocument.ZoomIndex = index;
        UpdateCanvasLayout();
        canvasPanel.AutoScrollPosition = new Point(
            Math.Max(0, (int)Math.Round(canvasPoint.X * ZoomFactor - anchor.X)),
            Math.Max(0, (int)Math.Round(canvasPoint.Y * ZoomFactor - anchor.Y)));
        if (inlineTextPanel != null)
        {
            inlineTextPanel.Bounds = ToDisplayBounds(inlineTextBounds);
            UpdateInlineTextFont();
        }
        canvasPictureBox.Invalidate();
    }

    private Point ToCanvasPoint(Point point) => new(
        (int)Math.Floor(point.X / ZoomFactor), (int)Math.Floor(point.Y / ZoomFactor));

    private Rectangle ToDisplayBounds(Rectangle bounds) => new(
        (int)Math.Round(bounds.X * ZoomFactor), (int)Math.Round(bounds.Y * ZoomFactor),
        Math.Max(1, (int)Math.Round(bounds.Width * ZoomFactor)),
        Math.Max(1, (int)Math.Round(bounds.Height * ZoomFactor)));

    private bool ConfirmLeavingDocument(DrawingDocument document)
    {
        if (!document.HasUnsavedChanges) return true;
        string name = document.FilePath == null ? "Новый рисунок" : Path.GetFileName(document.FilePath);
        DialogResult answer = MessageBox.Show(this,
            $"Сохранить изменения страницы {document.Number} ({name})?",
            "MyPaint", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
        return answer switch
        {
            DialogResult.Yes => SaveDocument(document),
            DialogResult.No => true,
            _ => false
        };
    }

    private void CopyDocument(DrawingDocument document)
    {
        if (!documents.Contains(document)) return;
        if (document == currentDocument) FinishDrawing();
        AddDocument((Bitmap)document.Bitmap.Clone(), isCopy: true);
    }

    private bool CloseDocument(DrawingDocument document)
    {
        int index = documents.IndexOf(document);
        if (index < 0) return false;
        if (document == currentDocument) FinishDrawing();
        if (!ConfirmLeavingDocument(document)) return false;
        bool wasActive = document == currentDocument;
        if (wasActive)
        {
            canvasPictureBox.Image = null;
            currentDocument = null!;
        }
        ToolStripMenuItem tab = documentTabs[document];
        MainMenuStrip!.Items.Remove(tab);
        documentTabs.Remove(document);
        documents.RemoveAt(index);
        if (contextDocument == document) contextDocument = null;
        tab.Dispose();
        document.Dispose();
        if (documents.Count == 0) AddDocument(CreateBlankCanvas());
        else if (wasActive) ActivateDocument(documents[Math.Min(index, documents.Count - 1)]);
        return true;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
        if (e.Cancel) return;
        FinishDrawing();
        foreach (DrawingDocument document in documents)
        {
            if (ConfirmLeavingDocument(document)) continue;
            e.Cancel = true;
            return;
        }
    }

    private void UpdateDocumentTitle()
    {
        string name = currentDocument.FilePath == null
            ? "Новый рисунок" : Path.GetFileName(currentDocument.FilePath);
        Text = $"MyPaint — {currentDocument.Number}: {name}{(currentDocument.HasUnsavedChanges ? " *" : "")}";
        foreach (var pair in documentTabs)
        {
            string path = pair.Key.FilePath ?? "Новый рисунок";
            pair.Value.ToolTipText = $"{path}{(pair.Key.HasUnsavedChanges ? " — есть изменения" : "")}";
        }
        MainMenuStrip!.Invalidate();
    }

    private void SetBrushWidth(int width)
    {
        if (width == brushWidth) return;
        FinishDrawing();
        brushWidth = width;
        widthInput.Value = width;
        widthSlider.Value = width;
        UpdateStatus();
    }

    private void SelectTool(ToolType tool)
    {
        FinishDrawing();
        currentTool = tool;
        ActiveControl = null;
        if (!currentDocument.Selection.IsEmpty)
        {
            if (tool == ToolType.Eraser) PaintSelection(Color.White);
            else if (tool == ToolType.Fill) PaintSelection(currentColor);
            else if (tool != ToolType.Selection) ClearSelection();
        }
        foreach (var pair in toolButtons)
            pair.Value.Checked = pair.Key == tool;
        if (tool == ToolType.Text) ShowTextOptions();
        else textOptionsWindow.Hide();
        canvasPictureBox.Cursor = tool == ToolType.Text ? Cursors.IBeam
            : tool == ToolType.Eraser ? Cursors.Hand : Cursors.Cross;
        canvasPictureBox.Focus();
        UpdateStatus();
    }

    private void ShowTextOptions()
    {
        if (!textOptionsWindow.Visible) textOptionsWindow.Show(this);
        if (!textOptionsPositioned || !Screen.AllScreens.Any(screen => screen.WorkingArea.IntersectsWith(textOptionsWindow.Bounds)))
        {
            int margin = (int)(12 * DeviceDpi / 96f);
            Point corner = canvasPanel.PointToScreen(Point.Empty);
            Rectangle screen = Screen.FromControl(this).WorkingArea;
            int x = corner.X + canvasPanel.ClientSize.Width - textOptionsWindow.Width - margin;
            int y = corner.Y + margin;
            textOptionsWindow.Location = new Point(
                Math.Clamp(x, screen.Left, Math.Max(screen.Left, screen.Right - textOptionsWindow.Width)),
                Math.Clamp(y, screen.Top, Math.Max(screen.Top, screen.Bottom - textOptionsWindow.Height)));
            textOptionsPositioned = true;
        }
    }

    private bool ProcessTextOptionsKey(Keys keyData)
    {
        Message message = new();
        return ProcessCmdKey(ref message, keyData);
    }

    private static Keys GetToolHotkey(ToolType tool) => tool switch
    {
        ToolType.Pencil => Keys.P,
        ToolType.Eraser => Keys.E,
        ToolType.Line => Keys.L,
        ToolType.Rectangle => Keys.R,
        ToolType.Ellipse => Keys.O,
        ToolType.Text => Keys.T,
        ToolType.Fill => Keys.F,
        ToolType.Selection => Keys.S,
        _ => Keys.None
    };

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (inlineTextEditor != null)
        {
            if (keyData == (Keys.Control | Keys.Enter))
            {
                FinishInlineText(true);
                return true;
            }
            if (keyData == Keys.Escape)
            {
                FinishInlineText(false);
                return true;
            }
            if (inlineTextEditor.ContainsFocus && keyData == (Keys.Control | Keys.Z))
            {
                inlineTextEditor.Undo();
                return true;
            }
            // Letters and Del belong to text input, not to drawing tool shortcuts.
            if (inlineTextEditor.ContainsFocus) return base.ProcessCmdKey(ref msg, keyData);
        }
        if (keyData == (Keys.Control | Keys.Z))
        {
            Undo_Click(this, EventArgs.Empty);
            return true;
        }
        if (keyData == (Keys.Control | Keys.Y))
        {
            Redo_Click(this, EventArgs.Empty);
            return true;
        }
        if ((keyData & Keys.Modifiers) == Keys.None)
        {
            if (textOptionsPanel.ContainsFocus) return base.ProcessCmdKey(ref msg, keyData);
            if (keyData == Keys.Escape && movingBitmap != null)
            {
                CancelSelectionMove();
                return true;
            }
            if (keyData == Keys.Escape && isDrawing && currentTool == ToolType.Text)
            {
                isDrawing = false;
                canvasPictureBox.Capture = false;
                canvasPictureBox.Invalidate();
                return true;
            }
            if (keyData == Keys.Delete && !currentDocument.Selection.IsEmpty && !widthInput.ContainsFocus)
            {
                EraseSelection_Click(this, EventArgs.Empty);
                return true;
            }
            if (keyData == Keys.Escape && (isDrawing && currentTool == ToolType.Selection || !currentDocument.Selection.IsEmpty))
            {
                if (currentTool == ToolType.Selection)
                {
                    isDrawing = false;
                    canvasPictureBox.Capture = false;
                }
                ClearSelection();
                return true;
            }
            foreach (ToolType tool in Enum.GetValues<ToolType>())
            {
                if (keyData != GetToolHotkey(tool)) continue;
                SelectTool(tool);
                return true;
            }
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void UpdateStatus()
    {
        toolStatusLabel.Text = inlineTextEditor == null ? $"Инструмент: {currentTool}"
            : "Текст: Ctrl+Enter — применить, Esc — отменить";
        Color statusColor = currentTool == ToolType.Text ? textColor : currentColor;
        colorStatusLabel.Text = $"Цвет: #{statusColor.R:X2}{statusColor.G:X2}{statusColor.B:X2}";
        widthStatusLabel.Text = $"Толщина: {brushWidth} px";
        widthLabel.Text = $"{brushWidth} px";
        colorButton.BackColor = currentColor;
        colorButton.ForeColor = currentColor.GetBrightness() < 0.5f ? Color.White : Color.Black;
        UpdateTextColorButtons();
    }

    private Pen CreateDrawingPen()
    {
        return new Pen(currentTool == ToolType.Eraser ? Color.White : currentColor, brushWidth)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };
    }

    private Point ClampPoint(Point point) => new(
        Math.Clamp(point.X, 0, canvasBitmap.Width - 1),
        Math.Clamp(point.Y, 0, canvasBitmap.Height - 1));

    private void Canvas_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || isDrawing) return;
        FinishInlineText(true);
        ActiveControl = null;
        canvasPictureBox.Focus();
        startPoint = lastPoint = ClampPoint(ToCanvasPoint(e.Location));
        if (currentTool == ToolType.Fill)
        {
            if (currentDocument.Selection.IsEmpty) FillArea(startPoint);
            else PaintSelection(currentColor);
            return;
        }
        if (currentTool == ToolType.Eraser && !currentDocument.Selection.IsEmpty)
        {
            PaintSelection(Color.White);
            return;
        }
        if (currentTool == ToolType.Selection && currentDocument.Selection.Contains(startPoint))
        {
            moveSource = moveDestination = currentDocument.Selection;
            movingBitmap = canvasBitmap.Clone(moveSource, PixelFormat.Format32bppArgb);
            isDrawing = true;
            canvasPictureBox.Capture = true;
            canvasPictureBox.Cursor = Cursors.SizeAll;
            return;
        }
        if (currentTool == ToolType.Selection) ClearSelection();
        isDrawing = true;
        canvasPictureBox.Capture = true;
        if (currentTool is ToolType.Pencil or ToolType.Eraser)
        {
            SaveUndoState();
            // A dot also makes a single click a valid drawing action.
            using Graphics graphics = Graphics.FromImage(canvasBitmap);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using Brush brush = new SolidBrush(currentTool == ToolType.Eraser ? Color.White : currentColor);
            graphics.FillEllipse(brush, startPoint.X - brushWidth / 2f,
                startPoint.Y - brushWidth / 2f, brushWidth, brushWidth);
            canvasPictureBox.Invalidate();
        }
    }

    private void Canvas_MouseMove(object? sender, MouseEventArgs e)
    {
        Point canvasPoint = ToCanvasPoint(e.Location);
        coordinatesStatusLabel.Text = $"X: {canvasPoint.X}, Y: {canvasPoint.Y}";
        if (!isDrawing)
        {
            if (currentTool == ToolType.Selection)
                canvasPictureBox.Cursor = currentDocument.Selection.Contains(canvasPoint) ? Cursors.SizeAll : Cursors.Cross;
            return;
        }
        if (movingBitmap != null)
        {
            int x = Math.Clamp(moveSource.X + canvasPoint.X - startPoint.X, 0, canvasBitmap.Width - moveSource.Width);
            int y = Math.Clamp(moveSource.Y + canvasPoint.Y - startPoint.Y, 0, canvasBitmap.Height - moveSource.Height);
            moveDestination.Location = new Point(x, y);
            canvasPictureBox.Invalidate();
            return;
        }
        Point point = ClampPoint(canvasPoint);
        if (currentTool is ToolType.Pencil or ToolType.Eraser)
        {
            using Graphics graphics = Graphics.FromImage(canvasBitmap);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using Pen pen = CreateDrawingPen();
            graphics.DrawLine(pen, lastPoint, point);
        }
        lastPoint = point;
        canvasPictureBox.Invalidate();
    }

    private void Canvas_MouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || !isDrawing) return;
        Canvas_MouseMove(sender, e);
        FinishDrawing();
    }

    private void FinishDrawing()
    {
        FinishInlineText(true);
        if (!isDrawing) return;
        bool startTextEditor = currentTool == ToolType.Text;
        Rectangle textBounds = startTextEditor ? GetSelectionBounds() : Rectangle.Empty;
        if (movingBitmap != null)
        {
            if (moveDestination != moveSource)
            {
                SaveUndoState();
                using Graphics graphics = Graphics.FromImage(canvasBitmap);
                graphics.FillRectangle(Brushes.White, moveSource);
                graphics.DrawImageUnscaled(movingBitmap, moveDestination.Location);
            }
            currentDocument.Selection = moveDestination;
            movingBitmap.Dispose();
            movingBitmap = null;
            UpdateSelectionButtons();
        }
        else if (currentTool == ToolType.Selection)
        {
            currentDocument.Selection = GetSelectionBounds();
            UpdateSelectionButtons();
        }
        if (currentTool is ToolType.Line or ToolType.Rectangle or ToolType.Ellipse)
        {
            SaveUndoState();
            using Graphics graphics = Graphics.FromImage(canvasBitmap);
            DrawShape(graphics, startPoint, lastPoint);
        }
        isDrawing = false;
        canvasPictureBox.Capture = false;
        canvasPictureBox.Invalidate();
        UpdateHistoryButtons();
        if (startTextEditor && textBounds.Width >= 8 && textBounds.Height >= 8)
            BeginInlineText(textBounds);
    }

    private void Canvas_Paint(object? sender, PaintEventArgs e)
    {
        GraphicsState state = e.Graphics.Save();
        try
        {
            e.Graphics.ScaleTransform(ZoomFactor, ZoomFactor);
            DrawCanvasPreview(e.Graphics);
        }
        finally { e.Graphics.Restore(state); }
    }

    private void DrawCanvasPreview(Graphics graphics)
    {
        // The preview is temporary; the completed shape is stored in the Bitmap.
        if (isDrawing && currentTool is ToolType.Line or ToolType.Rectangle or ToolType.Ellipse)
            DrawShape(graphics, startPoint, lastPoint);
        if (movingBitmap != null)
        {
            graphics.FillRectangle(Brushes.White, moveSource);
            graphics.DrawImageUnscaled(movingBitmap, moveDestination.Location);
        }
        Rectangle selection = movingBitmap != null ? moveDestination
            : isDrawing && currentTool is ToolType.Selection or ToolType.Text
                ? GetSelectionBounds() : currentDocument.Selection;
        if (!selection.IsEmpty)
        {
            // The selection overlay belongs only to the preview, never to the saved Bitmap.
            using Brush shade = new SolidBrush(Color.FromArgb(45, Color.Gray));
            graphics.FillRectangle(shade, selection);
            using Pen border = new(Color.Black, 1 / ZoomFactor) { DashStyle = DashStyle.Dash };
            graphics.DrawRectangle(border, selection.X, selection.Y, selection.Width - 1, selection.Height - 1);
        }
    }

    private void CancelSelectionMove()
    {
        isDrawing = false;
        canvasPictureBox.Capture = false;
        movingBitmap?.Dispose();
        movingBitmap = null;
        canvasPictureBox.Cursor = Cursors.Cross;
        canvasPictureBox.Invalidate();
    }

    private void BeginInlineText(Rectangle bounds)
    {
        inlineTextBounds = bounds;
        inlineTextPanel = new Panel
        {
            Bounds = ToDisplayBounds(bounds), BorderStyle = BorderStyle.FixedSingle, BackColor = textBackgroundColor
        };
        inlineTextEditor = new TextBox
        {
            Multiline = true, AcceptsReturn = true, WordWrap = true,
            BorderStyle = BorderStyle.None,
            ForeColor = textColor, BackColor = textBackgroundColor, AutoSize = false,
            ScrollBars = ScrollBars.None
        };
        inlineTextEditor.TextChanged += (_, _) => UpdateInlineTextLayout();
        inlineTextPanel.Controls.Add(inlineTextEditor);
        canvasPictureBox.Controls.Add(inlineTextPanel);
        UpdateInlineTextFont();
        inlineTextPanel.BringToFront();
        inlineTextEditor.Focus();
        toolStatusLabel.Text = "Текст: Ctrl+Enter — применить, Esc — отменить";
    }

    private void UpdateInlineTextFont()
    {
        if (inlineTextEditor == null) return;
        Font? previous = inlineTextFont;
        inlineTextFont = textFonts.CreateFont(textFontName, textFontSize * ZoomFactor);
        inlineTextEditor.Font = inlineTextFont;
        previous?.Dispose();
        UpdateInlineTextLayout();
    }

    private RectangleF GetTextLayoutBounds(Graphics graphics, string text, Font font, RectangleF area)
    {
        using StringFormat format = new() { Alignment = textHorizontalAlignment };
        float height = Math.Min(area.Height, Math.Max(font.GetHeight(graphics),
            graphics.MeasureString(string.IsNullOrEmpty(text) ? " " : text, font,
                new SizeF(area.Width, 100000), format).Height));
        float offset = textVerticalAlignment switch
        {
            StringAlignment.Center => (area.Height - height) / 2,
            StringAlignment.Far => area.Height - height,
            _ => 0
        };
        return new RectangleF(area.X, area.Y + offset, area.Width, height);
    }

    private void UpdateInlineTextLayout()
    {
        if (inlineTextEditor == null || inlineTextPanel == null || inlineTextFont == null) return;
        inlineTextPanel.BackColor = inlineTextEditor.BackColor = textBackgroundColor;
        inlineTextEditor.ForeColor = textColor;
        inlineTextEditor.TextAlign = textHorizontalAlignment switch
        {
            StringAlignment.Center => HorizontalAlignment.Center,
            StringAlignment.Far => HorizontalAlignment.Right,
            _ => HorizontalAlignment.Left
        };
        using Graphics graphics = Graphics.FromImage(canvasBitmap);
        RectangleF area = new(1, 1, Math.Max(1, inlineTextPanel.ClientSize.Width - 2),
            Math.Max(1, inlineTextPanel.ClientSize.Height - 2));
        RectangleF layout = GetTextLayoutBounds(graphics, inlineTextEditor.Text, inlineTextFont, area);
        // Keep the editing area tall enough for a caret and another line.
        inlineTextEditor.Bounds = new Rectangle((int)layout.X, (int)layout.Y,
            (int)layout.Width, Math.Max(1, (int)(area.Bottom - layout.Y)));
    }

    private void UpdateTextColorButtons()
    {
        textColorButton.BackColor = textColor;
        textColorButton.ForeColor = textColor.GetBrightness() < 0.5f ? Color.White : Color.Black;
        textBackgroundButton.BackColor = textBackgroundColor;
        textBackgroundButton.ForeColor = textBackgroundColor.GetBrightness() < 0.5f ? Color.White : Color.Black;
    }

    private void TextColor_Click(object? sender, EventArgs e)
    {
        using ColorDialog dialog = new() { Color = textColor, FullOpen = true };
        if (dialog.ShowDialog(this) == DialogResult.OK) textColor = dialog.Color;
        UpdateStatus();
        UpdateInlineTextLayout();
        inlineTextEditor?.Focus();
    }

    private void TextBackground_Click(object? sender, EventArgs e)
    {
        using ColorDialog dialog = new() { Color = textBackgroundColor, FullOpen = true };
        if (dialog.ShowDialog(this) == DialogResult.OK) textBackgroundColor = dialog.Color;
        UpdateStatus();
        UpdateInlineTextLayout();
        inlineTextEditor?.Focus();
    }

    private void RefreshFontChoices()
    {
        textFontComboBox.BeginUpdate();
        textFontComboBox.Items.Clear();
        textFontComboBox.Items.AddRange(textFonts.Names.Cast<object>().ToArray());
        textFontComboBox.SelectedItem = textFontName;
        if (textFontComboBox.SelectedIndex < 0) textFontComboBox.SelectedIndex = 0;
        textFontComboBox.EndUpdate();
    }

    private void LoadFontFromDialog(IWin32Window owner)
    {
        using OpenFileDialog dialog = new() { Title = "Загрузить шрифт", Filter = "Шрифты TTF/OTF|*.ttf;*.otf" };
        if (dialog.ShowDialog(owner) != DialogResult.OK) return;
        try
        {
            textFontName = textFonts.LoadFile(dialog.FileName);
            RefreshFontChoices();
            UpdateInlineTextFont();
        }
        catch (Exception exception)
        {
            MessageBox.Show(owner, $"Не удалось загрузить шрифт.\n{exception.Message}",
                "MyPaint", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        inlineTextEditor?.Focus();
    }

    private void FinishInlineText(bool commit)
    {
        if (inlineTextEditor == null) return;
        TextBox editor = inlineTextEditor;
        string text = editor.Text;
        inlineTextEditor = null;
        if (inlineTextPanel != null)
        {
            canvasPictureBox.Controls.Remove(inlineTextPanel);
            inlineTextPanel.Dispose();
            inlineTextPanel = null;
        }
        else editor.Dispose();
        inlineTextFont?.Dispose();
        inlineTextFont = null;
        ActiveControl = null;
        if (commit && !string.IsNullOrWhiteSpace(text))
        {
            SaveUndoState();
            using Graphics graphics = Graphics.FromImage(canvasBitmap);
            graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            graphics.SetClip(inlineTextBounds);
            using Brush background = new SolidBrush(textBackgroundColor);
            graphics.FillRectangle(background, inlineTextBounds);
            using Font font = textFonts.CreateFont(textFontName, textFontSize);
            using Brush brush = new SolidBrush(textColor);
            RectangleF textArea = new(inlineTextBounds.X + 2, inlineTextBounds.Y + 2,
                Math.Max(1, inlineTextBounds.Width - 4), Math.Max(1, inlineTextBounds.Height - 4));
            RectangleF layout = GetTextLayoutBounds(graphics, text, font, textArea);
            using StringFormat format = new() { Alignment = textHorizontalAlignment };
            graphics.DrawString(text, font, brush, layout, format);
        }
        UpdateStatus();
        canvasPictureBox.Invalidate();
    }

    private Rectangle GetSelectionBounds()
    {
        if (startPoint == lastPoint) return Rectangle.Empty;
        return new Rectangle(Math.Min(startPoint.X, lastPoint.X), Math.Min(startPoint.Y, lastPoint.Y),
            Math.Abs(startPoint.X - lastPoint.X) + 1, Math.Abs(startPoint.Y - lastPoint.Y) + 1);
    }

    private void UpdateSelectionButtons()
    {
        eraseSelectionButton.Enabled = eraseSelectionMenuItem.Enabled = !currentDocument.Selection.IsEmpty;
    }

    private void ClearSelection()
    {
        currentDocument.Selection = Rectangle.Empty;
        UpdateSelectionButtons();
        canvasPictureBox.Invalidate();
    }

    private void PaintSelection(Color color)
    {
        if (currentDocument.Selection.IsEmpty) return;
        SaveUndoState();
        using Graphics graphics = Graphics.FromImage(canvasBitmap);
        using Brush brush = new SolidBrush(color);
        graphics.FillRectangle(brush, currentDocument.Selection);
        canvasPictureBox.Invalidate();
    }

    private void EraseSelection_Click(object? sender, EventArgs e)
    {
        FinishDrawing();
        PaintSelection(Color.White);
    }

    private void DrawShape(Graphics graphics, Point first, Point second)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using Pen pen = CreateDrawingPen();
        Rectangle bounds = new(Math.Min(first.X, second.X), Math.Min(first.Y, second.Y),
            Math.Abs(first.X - second.X), Math.Abs(first.Y - second.Y));
        if (currentTool == ToolType.Line)
            graphics.DrawLine(pen, first, second);
        else if (bounds.Width > 0 && bounds.Height > 0)
        {
            if (currentTool == ToolType.Rectangle) graphics.DrawRectangle(pen, bounds);
            else if (currentTool == ToolType.Ellipse) graphics.DrawEllipse(pen, bounds);
        }
    }

    private void DrawTextAtPoint(Point point, string text, float fontSize)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        SaveUndoState();
        using Graphics graphics = Graphics.FromImage(canvasBitmap);
        graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        using Font font = new("Segoe UI", fontSize, FontStyle.Regular, GraphicsUnit.Pixel);
        using Brush brush = new SolidBrush(currentColor);
        graphics.DrawString(text, font, brush, point, StringFormat.GenericTypographic);
        canvasPictureBox.Invalidate();
    }

    // Kept as an alternative text entry method, disconnected from Plain Text.
    private void ShowLegacyTextDialog(Point point)
    {
        using TextInputDialog dialog = new();
        if (dialog.ShowDialog(this) == DialogResult.OK)
            DrawTextAtPoint(point, dialog.DrawingText, dialog.FontSize);
    }

    private void FillArea(Point point)
    {
        int targetColor = canvasBitmap.GetPixel(point.X, point.Y).ToArgb();
        int replacementColor = currentColor.ToArgb();
        if (targetColor == replacementColor) return;
        SaveUndoState();
        int width = canvasBitmap.Width;
        int height = canvasBitmap.Height;
        Rectangle bounds = new(0, 0, width, height);
        BitmapData data = canvasBitmap.LockBits(bounds, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        try
        {
            int[] pixels = new int[width * height];
            for (int y = 0; y < height; y++)
                Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), pixels, y * width, width);

            // Mark pixels when enqueuing, so each pixel is visited once. No recursion.
            Queue<int> pending = new();
            int start = point.Y * width + point.X;
            pixels[start] = replacementColor;
            pending.Enqueue(start);
            void Visit(int index)
            {
                if (pixels[index] != targetColor) return;
                pixels[index] = replacementColor;
                pending.Enqueue(index);
            }
            while (pending.Count > 0)
            {
                int index = pending.Dequeue();
                int x = index % width;
                int y = index / width;
                if (x > 0) Visit(index - 1);
                if (x < width - 1) Visit(index + 1);
                if (y > 0) Visit(index - width);
                if (y < height - 1) Visit(index + width);
            }
            for (int y = 0; y < height; y++)
                Marshal.Copy(pixels, y * width, IntPtr.Add(data.Scan0, y * data.Stride), width);
        }
        finally { canvasBitmap.UnlockBits(data); }
        canvasPictureBox.Invalidate();
    }

    private static void DisposeHistory(Stack<Bitmap> history)
    {
        while (history.Count > 0) history.Pop().Dispose();
    }

    private static void PushHistory(Stack<Bitmap> history, Stack<long> versions, Bitmap bitmap, long version)
    {
        history.Push(bitmap);
        versions.Push(version);
        if (history.Count <= HistoryLimit) return;
        // Stack.ToArray returns the newest state first. Remove only the oldest.
        Bitmap[] states = history.ToArray();
        long[] stateVersions = versions.ToArray();
        states[^1].Dispose();
        history.Clear();
        versions.Clear();
        for (int i = states.Length - 2; i >= 0; i--)
        {
            history.Push(states[i]);
            versions.Push(stateVersions[i]);
        }
    }

    private void SaveUndoState()
    {
        PushHistory(undoStack, currentDocument.UndoVersions, (Bitmap)canvasBitmap.Clone(), currentDocument.CurrentVersion);
        DisposeHistory(redoStack);
        currentDocument.RedoVersions.Clear();
        currentDocument.CurrentVersion = ++currentDocument.LastVersion;
        UpdateDocumentTitle();
        UpdateHistoryButtons();
    }

    private void ReplaceCanvas(Bitmap bitmap)
    {
        Bitmap previous = canvasBitmap;
        canvasBitmap = bitmap;
        ClearSelection();
        canvasPictureBox.Image = canvasBitmap;
        UpdateCanvasLayout();
        previous.Dispose();
        canvasPictureBox.Invalidate();
    }

    private void UpdateCanvasLayout()
    {
        Size displaySize = ToDisplayBounds(new Rectangle(Point.Empty, canvasBitmap.Size)).Size;
        canvasPanel.AutoScrollMinSize = displaySize;
        canvasPictureBox.Size = displaySize;
        zoomStatusLabel.Text = $"Масштаб: {ZoomLevels[currentDocument.ZoomIndex]}%";
    }

    private void Undo_Click(object? sender, EventArgs e)
    {
        FinishDrawing();
        if (undoStack.Count == 0) return;
        PushHistory(redoStack, currentDocument.RedoVersions, (Bitmap)canvasBitmap.Clone(), currentDocument.CurrentVersion);
        ReplaceCanvas(undoStack.Pop());
        currentDocument.CurrentVersion = currentDocument.UndoVersions.Pop();
        UpdateDocumentTitle();
        UpdateHistoryButtons();
    }

    private void Redo_Click(object? sender, EventArgs e)
    {
        FinishDrawing();
        if (redoStack.Count == 0) return;
        PushHistory(undoStack, currentDocument.UndoVersions, (Bitmap)canvasBitmap.Clone(), currentDocument.CurrentVersion);
        ReplaceCanvas(redoStack.Pop());
        currentDocument.CurrentVersion = currentDocument.RedoVersions.Pop();
        UpdateDocumentTitle();
        UpdateHistoryButtons();
    }

    private void UpdateHistoryButtons()
    {
        undoButton.Enabled = undoMenuItem.Enabled = undoStack.Count > 0;
        redoButton.Enabled = redoMenuItem.Enabled = redoStack.Count > 0;
    }

    private void Color_Click(object? sender, EventArgs e)
    {
        if (inlineTextEditor == null) FinishDrawing();
        using ColorDialog dialog = new() { Color = currentColor, FullOpen = true };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            currentColor = dialog.Color;
        }
        UpdateStatus();
        inlineTextEditor?.Focus();
    }

    private void Settings_Click(object? sender, EventArgs e)
    {
        FinishDrawing();
        using SettingsDialog dialog = new(settings, canvasBitmap.Size);
        dialog.LoadFontRequested += (_, _) => LoadFontFromDialog(dialog);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try { ApplySettings(dialog.SelectedSettings); }
        catch (Exception exception)
        {
            MessageBox.Show(this, $"Не удалось применить настройки.\n{exception.Message}",
                "Settings", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ApplySettings(AppSettings newSettings)
    {
        FinishDrawing();
        Size size = newSettings.CanvasWidth == 0 ? GetWindowCanvasSize()
            : new Size(newSettings.CanvasWidth, newSettings.CanvasHeight);
        Bitmap? resized = null;
        try
        {
            if (canvasBitmap.Size != size)
            {
                resized = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppArgb);
                using Graphics graphics = Graphics.FromImage(resized);
                graphics.Clear(Color.White);
                graphics.DrawImageUnscaled(canvasBitmap, Point.Empty);
            }
            Directory.CreateDirectory(newSettings.SaveFolder);
            newSettings.Save();
            if (resized != null)
            {
                SaveUndoState();
                ReplaceCanvas(resized);
                resized = null;
            }
            settings = newSettings;
        }
        finally { resized?.Dispose(); }
    }

    private void Clear_Click(object? sender, EventArgs e)
    {
        FinishDrawing();
        if (MessageBox.Show(this, "Очистить холст?", "MyPaint",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        SaveUndoState();
        ClearSelection();
        using Graphics graphics = Graphics.FromImage(canvasBitmap);
        graphics.Clear(Color.White);
        canvasPictureBox.Invalidate();
    }

    private void Open_Click(object? sender, EventArgs e)
    {
        FinishDrawing();
        using OpenFileDialog dialog = new()
        {
            Title = "Открыть изображение",
            Filter = "Изображения|*.png;*.jpg;*.jpeg;*.bmp|PNG|*.png|JPEG|*.jpg;*.jpeg|BMP|*.bmp"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        OpenImage(dialog.FileName);
    }

    private void OpenImage(string filePath)
    {
        FinishDrawing();
        if (!ConfirmLeavingDocument(currentDocument)) return;
        Bitmap? loadedCanvas = null;
        try
        {
            // Read after saving, so reopening the same file uses its latest contents.
            using (Image image = Image.FromFile(filePath))
            {
                Size canvasSize = GetNewCanvasSize();
                loadedCanvas = new Bitmap(canvasSize.Width, canvasSize.Height, PixelFormat.Format32bppArgb);
                using Graphics graphics = Graphics.FromImage(loadedCanvas);
                graphics.Clear(Color.White);
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                float scale = Math.Min((float)loadedCanvas.Width / image.Width, (float)loadedCanvas.Height / image.Height);
                int width = Math.Max(1, (int)(image.Width * scale));
                int height = Math.Max(1, (int)(image.Height * scale));
                graphics.DrawImage(image, (loadedCanvas.Width - width) / 2, (loadedCanvas.Height - height) / 2, width, height);
            }
            AddDocument(loadedCanvas, filePath, confirmChanges: false);
            loadedCanvas = null; // AddDocument either owns or disposes the bitmap.
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, $"Не удалось открыть изображение.\n{exception.Message}",
                "MyPaint", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { loadedCanvas?.Dispose(); }
    }

    private void Save_Click(object? sender, EventArgs e)
    {
        FinishDrawing();
        SaveDocument(currentDocument);
    }

    private bool SaveDocument(DrawingDocument document) => document.FilePath == null
        ? SaveDocumentAs(document) : SaveDocumentToFile(document, document.FilePath);

    private void SaveAs_Click(object? sender, EventArgs e)
    {
        FinishDrawing();
        SaveDocumentAs(currentDocument);
    }

    private bool SaveDocumentAs(DrawingDocument document)
    {
        string? currentPath = document.FilePath;
        string extension = Path.GetExtension(currentPath)?.ToLowerInvariant() ?? ".png";
        try { Directory.CreateDirectory(settings.SaveFolder); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, $"Не удалось открыть папку сохранения.\n{exception.Message}",
                "MyPaint", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
        using SaveFileDialog dialog = new()
        {
            Title = "Сохранить изображение как",
            FileName = currentPath == null ? $"drawing-{document.Number}.png" : Path.GetFileName(currentPath),
            InitialDirectory = currentPath == null ? settings.SaveFolder : Path.GetDirectoryName(currentPath),
            FilterIndex = extension is ".jpg" or ".jpeg" ? 2 : extension == ".bmp" ? 3 : 1,
            DefaultExt = extension.TrimStart('.'),
            AddExtension = true, OverwritePrompt = true, Filter = "PNG|*.png|JPEG|*.jpg|BMP|*.bmp"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return false;
        return SaveDocumentToFile(document, dialog.FileName);
    }

    private bool SaveToFile(string filePath)
    {
        FinishDrawing();
        return SaveDocumentToFile(currentDocument, filePath);
    }

    private bool SaveDocumentToFile(DrawingDocument document, string filePath)
    {
        try
        {
            ImageFormat format = Path.GetExtension(filePath).ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => ImageFormat.Jpeg,
                ".bmp" => ImageFormat.Bmp,
                ".png" => ImageFormat.Png,
                _ => throw new InvalidOperationException("Выберите расширение PNG, JPG или BMP.")
            };
            document.Bitmap.Save(filePath, format);
            document.FilePath = filePath;
            document.SavedVersion = document.CurrentVersion;
            UpdateDocumentTitle();
            return true;
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, $"Не удалось сохранить изображение.\n{exception.Message}",
                "MyPaint", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }
}
