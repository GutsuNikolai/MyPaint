#nullable enable
namespace Paint;

partial class Form1
{
    private System.ComponentModel.IContainer? components;
    private PictureBox canvasPictureBox = null!;
    private Panel canvasPanel = null!;
    private Button colorButton = null!;
    private ToolStripButton undoButton = null!;
    private ToolStripButton redoButton = null!;
    private ToolStripMenuItem undoMenuItem = null!;
    private ToolStripMenuItem redoMenuItem = null!;
    private ToolStripButton eraseSelectionButton = null!;
    private ToolStripMenuItem eraseSelectionMenuItem = null!;
    private Label widthLabel = null!;
    private NumericUpDown widthInput = null!;
    private TrackBar widthSlider = null!;
    private Label textFontSizeLabel = null!;
    private NumericUpDown textFontSizeInput = null!;
    private TableLayoutPanel textOptionsPanel = null!;
    private TextOptionsForm textOptionsWindow = null!;
    private ComboBox textFontComboBox = null!;
    private ComboBox textHorizontalComboBox = null!;
    private ComboBox textVerticalComboBox = null!;
    private Button textColorButton = null!;
    private Button textBackgroundButton = null!;
    private ToolStripStatusLabel toolStatusLabel = null!;
    private ToolStripStatusLabel colorStatusLabel = null!;
    private ToolStripStatusLabel widthStatusLabel = null!;
    private ToolStripStatusLabel zoomStatusLabel = null!;
    private ToolStripStatusLabel coordinatesStatusLabel = null!;
    private ContextMenuStrip pageContextMenu = null!;
    private Font? documentTabFont;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Application.RemoveMessageFilter(this);
            isDrawing = false;
            FinishInlineText(false);
            movingBitmap?.Dispose();
            movingBitmap = null;
            if (canvasPictureBox != null) canvasPictureBox.Image = null;
            foreach (DrawingDocument document in documents) document.Dispose();
            documents.Clear();
            components?.Dispose();
            textOptionsWindow?.Dispose();
            textFonts.Dispose();
            documentTabFont?.Dispose();
        }
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        SuspendLayout();
        Text = "MyPaint — графический редактор";
        ClientSize = new Size(1120, 820);
        MinimumSize = new Size(760, 500);
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 10F);

        MenuStrip menu = new() { ShowItemToolTips = true, Renderer = new PageMenuRenderer() };
        ToolStripMenuItem fileMenu = new("File");
        ToolStripMenuItem newMenuItem = new("New", null, New_Click) { ShortcutKeys = Keys.Control | Keys.N };
        ToolStripMenuItem openMenuItem = new("Open", null, Open_Click) { ShortcutKeys = Keys.Control | Keys.O };
        ToolStripMenuItem saveMenuItem = new("Save", null, Save_Click) { ShortcutKeys = Keys.Control | Keys.S };
        ToolStripMenuItem saveAsMenuItem = new("Save As", null, SaveAs_Click)
        {
            ShortcutKeys = Keys.Control | Keys.Shift | Keys.S
        };
        fileMenu.DropDownItems.AddRange(new ToolStripItem[]
        {
            newMenuItem, openMenuItem, saveMenuItem, saveAsMenuItem, new ToolStripSeparator(),
            new ToolStripMenuItem("Exit", null, (_, _) => Close())
        });
        ToolStripMenuItem editMenu = new("Edit") { Visible = false };
        undoMenuItem = new("Undo", null, Undo_Click) { ShortcutKeys = Keys.Control | Keys.Z };
        redoMenuItem = new("Redo", null, Redo_Click) { ShortcutKeys = Keys.Control | Keys.Y };
        eraseSelectionMenuItem = new("Erase Selection (Del)", null, EraseSelection_Click);
        editMenu.DropDownItems.AddRange(new ToolStripItem[]
        {
            undoMenuItem, redoMenuItem, new ToolStripSeparator(), eraseSelectionMenuItem
        });
        menu.Items.AddRange(new ToolStripItem[]
        {
            fileMenu, editMenu,
            new ToolStripMenuItem("Settings", null, Settings_Click), new ToolStripSeparator()
        });
        MainMenuStrip = menu;
        pageContextMenu = new ContextMenuStrip(components);
        pageContextMenu.Items.Add("Delete", null, (_, _) =>
        {
            if (contextDocument != null) CloseDocument(contextDocument);
        });
        pageContextMenu.Items.Add("Copy", null, (_, _) =>
        {
            if (contextDocument != null) CopyDocument(contextDocument);
        });

        ToolStrip toolbar = new()
        {
            GripStyle = ToolStripGripStyle.Hidden, Padding = new Padding(8, 6, 8, 6),
            BackColor = Color.FromArgb(240, 243, 247)
        };
        foreach (ToolType tool in Enum.GetValues<ToolType>())
        {
            string toolCaption = tool == ToolType.Text ? "Plain Text" : tool == ToolType.Selection ? "Select Area" : tool.ToString();
            ToolStripButton button = new(toolCaption)
            {
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                AutoToolTip = false, ToolTipText = $"Клавиша {GetToolHotkey(tool)}", Padding = new Padding(5)
            };
            button.Click += (_, _) => SelectTool(tool);
            toolButtons.Add(tool, button);
            toolbar.Items.Add(button);
        }
        undoButton = new("Undo", null, Undo_Click);
        redoButton = new("Redo", null, Redo_Click);
        eraseSelectionButton = new("Erase Selection", null, EraseSelection_Click) { ToolTipText = "Удалить выделенную область — Del" };
        toolbar.Items.AddRange(new ToolStripItem[]
        {
            new ToolStripSeparator(), new ToolStripButton("Clear", null, Clear_Click),
            new ToolStripSeparator(), undoButton, redoButton, new ToolStripSeparator(), eraseSelectionButton
        });

        FlowLayoutPanel widthPanel = new()
        {
            Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(12, 7, 12, 7),
            BackColor = Color.FromArgb(248, 249, 251)
        };
        Label caption = new() { Text = "Толщина:", AutoSize = true, Margin = new Padding(0, 5, 8, 0) };
        widthInput = new()
        {
            Minimum = 1, Maximum = 100, Value = brushWidth, Width = 65
        };
        widthSlider = new()
        {
            Minimum = 1, Maximum = 100, Value = brushWidth, Width = 220,
            AutoSize = false, Height = 32, TickStyle = TickStyle.None,
            SmallChange = 1, LargeChange = 10, Margin = new Padding(8, 0, 0, 0)
        };
        widthLabel = new() { AutoSize = true, Margin = new Padding(10, 5, 0, 0) };
        widthInput.ValueChanged += (_, _) => SetBrushWidth((int)widthInput.Value);
        widthSlider.ValueChanged += (_, _) => SetBrushWidth(widthSlider.Value);
        colorButton = new()
        {
            Text = "Color", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(12, 5, 12, 5), Margin = new Padding(20, 0, 0, 0),
            UseVisualStyleBackColor = false
        };
        colorButton.Click += Color_Click;
        textFontSizeLabel = new()
        {
            Text = "Размер (px):", AutoSize = true, Margin = new Padding(8, 5, 8, 0)
        };
        textFontSizeInput = new()
        {
            Minimum = 8, Maximum = 100, Value = textFontSize, Width = 75
        };
        textFontSizeInput.ValueChanged += (_, _) =>
        {
            textFontSize = (int)textFontSizeInput.Value;
            UpdateInlineTextFont();
        };
        textOptionsPanel = new TableLayoutPanel
        {
            AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 2, RowCount = 7
        };
        textOptionsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        textOptionsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        for (int i = 0; i < 7; i++) textOptionsPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        textFontComboBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 190, DropDownWidth = 270 };
        textFontComboBox.SelectedIndexChanged += (_, _) =>
        {
            if (textFontComboBox.SelectedItem is not string name) return;
            textFontName = name;
            UpdateInlineTextFont();
        };
        RefreshFontChoices();
        textColorButton = CreateTextOptionButton("Цвет текста");
        textColorButton.Click += TextColor_Click;
        textBackgroundButton = CreateTextOptionButton("Фон текста");
        textBackgroundButton.Click += TextBackground_Click;
        textHorizontalComboBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 130 };
        textHorizontalComboBox.Items.AddRange(new object[] { "Слева", "По центру", "Справа" });
        textHorizontalComboBox.SelectedIndex = 0;
        textHorizontalComboBox.SelectedIndexChanged += (_, _) =>
        {
            textHorizontalAlignment = (System.Drawing.StringAlignment)textHorizontalComboBox.SelectedIndex;
            UpdateInlineTextLayout();
        };
        textVerticalComboBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 130 };
        textVerticalComboBox.Items.AddRange(new object[] { "Сверху", "По центру", "Снизу" });
        textVerticalComboBox.SelectedIndex = 0;
        textVerticalComboBox.SelectedIndexChanged += (_, _) =>
        {
            textVerticalAlignment = (System.Drawing.StringAlignment)textVerticalComboBox.SelectedIndex;
            UpdateInlineTextLayout();
        };
        textOptionsPanel.Controls.Add(new Label { Text = "Шрифт:", AutoSize = true, Margin = new Padding(3, 5, 8, 3) }, 0, 0);
        textOptionsPanel.Controls.Add(textFontComboBox, 1, 0);
        textOptionsPanel.Controls.Add(textFontSizeLabel, 0, 1);
        textOptionsPanel.Controls.Add(textFontSizeInput, 1, 1);
        textColorButton.Dock = textBackgroundButton.Dock = DockStyle.Fill;
        textOptionsPanel.Controls.Add(textColorButton, 0, 2);
        textOptionsPanel.SetColumnSpan(textColorButton, 2);
        textOptionsPanel.Controls.Add(textBackgroundButton, 0, 3);
        textOptionsPanel.SetColumnSpan(textBackgroundButton, 2);
        textOptionsPanel.Controls.Add(new Label { Text = "По ширине:", AutoSize = true, Margin = new Padding(3, 5, 8, 3) }, 0, 4);
        textOptionsPanel.Controls.Add(textHorizontalComboBox, 1, 4);
        textOptionsPanel.Controls.Add(new Label { Text = "По высоте:", AutoSize = true, Margin = new Padding(3, 5, 8, 3) }, 0, 5);
        textOptionsPanel.Controls.Add(textVerticalComboBox, 1, 5);
        Label textHint = new()
        {
            Text = "Ctrl+Enter — применить текст\nEsc — отменить ввод", AutoSize = true, Margin = new Padding(3, 10, 3, 3)
        };
        textOptionsPanel.Controls.Add(textHint, 0, 6);
        textOptionsPanel.SetColumnSpan(textHint, 2);
        textOptionsWindow = new TextOptionsForm(textOptionsPanel, ProcessTextOptionsKey);
        widthPanel.Controls.AddRange(new Control[]
        {
            caption, widthInput, widthSlider, widthLabel, colorButton
        });

        canvasPanel = new()
        {
            Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.FromArgb(210, 215, 222)
        };
        canvasPictureBox = new PictureBox
        {
            Location = Point.Empty, Size = new Size(1, 1), TabStop = true,
            BackColor = Color.White, SizeMode = PictureBoxSizeMode.StretchImage
        };
        canvasPanel.Controls.Add(canvasPictureBox);
        canvasPictureBox.MouseDown += Canvas_MouseDown;
        canvasPictureBox.MouseMove += Canvas_MouseMove;
        canvasPictureBox.MouseUp += Canvas_MouseUp;
        canvasPictureBox.Paint += Canvas_Paint;
        canvasPictureBox.MouseCaptureChanged += (_, _) =>
        {
            if (!canvasPictureBox.Capture) FinishDrawing();
        };
        canvasPictureBox.MouseLeave += (_, _) => coordinatesStatusLabel.Text = "X: —, Y: —";

        StatusStrip status = new();
        toolStatusLabel = new() { BorderSides = ToolStripStatusLabelBorderSides.Right };
        colorStatusLabel = new() { BorderSides = ToolStripStatusLabelBorderSides.Right };
        widthStatusLabel = new() { BorderSides = ToolStripStatusLabelBorderSides.Right };
        zoomStatusLabel = new() { BorderSides = ToolStripStatusLabelBorderSides.Right };
        coordinatesStatusLabel = new("X: —, Y: —") { Spring = true, TextAlign = ContentAlignment.MiddleRight };
        status.Items.AddRange(new ToolStripItem[]
        {
            toolStatusLabel, colorStatusLabel, widthStatusLabel, zoomStatusLabel, coordinatesStatusLabel
        });

        Controls.Add(canvasPanel);
        Controls.Add(widthPanel);
        Controls.Add(toolbar);
        Controls.Add(menu);
        Controls.Add(status);
        ResumeLayout(true);
    }

    private static Button CreateTextOptionButton(string text) => new()
    {
        Text = text, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
        Padding = new Padding(8, 5, 8, 5), UseVisualStyleBackColor = false
    };
}

