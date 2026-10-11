namespace Paint;

internal sealed class SettingsDialog : Form
{
    private readonly CheckBox useWindowSize;
    private readonly NumericUpDown widthInput;
    private readonly NumericUpDown heightInput;
    private readonly TextBox folderInput;
    public event EventHandler? LoadFontRequested;
    public AppSettings SelectedSettings => new()
    {
        CanvasWidth = useWindowSize.Checked ? 0 : (int)widthInput.Value,
        CanvasHeight = useWindowSize.Checked ? 0 : (int)heightInput.Value,
        SaveFolder = Path.GetFullPath(folderInput.Text.Trim())
    };

    public SettingsDialog(AppSettings settings, Size currentSize)
    {
        SuspendLayout();
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 10F);
        Text = "Settings";
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        TableLayoutPanel layout = new()
        {
            AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 3, RowCount = 7
        };
        for (int i = 0; i < 3; i++) layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        for (int i = 0; i < 7; i++) layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        useWindowSize = new CheckBox
        {
            Text = "Размер холста по окну", AutoSize = true, Checked = settings.CanvasWidth == 0
        };
        layout.Controls.Add(useWindowSize, 0, 0);
        layout.SetColumnSpan(useWindowSize, 3);
        widthInput = new NumericUpDown
        {
            Minimum = 1, Maximum = 4096, Width = 100,
            Value = Math.Clamp(settings.CanvasWidth == 0 ? currentSize.Width : settings.CanvasWidth, 1, 4096)
        };
        heightInput = new NumericUpDown
        {
            Minimum = 1, Maximum = 4096, Width = 100,
            Value = Math.Clamp(settings.CanvasHeight == 0 ? currentSize.Height : settings.CanvasHeight, 1, 4096)
        };
        layout.Controls.Add(new Label { Text = "Ширина (px):", AutoSize = true, Margin = new Padding(3, 6, 8, 3) }, 0, 1);
        layout.Controls.Add(widthInput, 1, 1);
        layout.Controls.Add(new Label { Text = "Высота (px):", AutoSize = true, Margin = new Padding(3, 6, 8, 3) }, 0, 2);
        layout.Controls.Add(heightInput, 1, 2);
        Label hint = new()
        {
            Text = "Размер применяется к текущей вкладке и новым холстам.\nПри уменьшении края обрезаются; Undo восстанавливает рисунок.",
            AutoSize = true, Margin = new Padding(3, 10, 3, 10)
        };
        layout.Controls.Add(hint, 0, 3);
        layout.SetColumnSpan(hint, 3);
        layout.Controls.Add(new Label { Text = "Папка рисунков:", AutoSize = true, Margin = new Padding(3, 6, 8, 3) }, 0, 4);
        folderInput = new TextBox { Text = settings.SaveFolder, Width = 280, Anchor = AnchorStyles.Left | AnchorStyles.Right };
        layout.Controls.Add(folderInput, 1, 4);
        Button browseButton = CreateButton("Выбрать…");
        browseButton.Click += (_, _) =>
        {
            using FolderBrowserDialog dialog = new() { Description = "Папка сохранения MyPaint", UseDescriptionForTitle = true };
            if (Directory.Exists(folderInput.Text)) dialog.SelectedPath = folderInput.Text;
            if (dialog.ShowDialog(this) == DialogResult.OK) folderInput.Text = dialog.SelectedPath;
        };
        layout.Controls.Add(browseButton, 2, 4);
        Button loadFontButton = CreateButton("Загрузить шрифт…");
        loadFontButton.Click += (_, _) => LoadFontRequested?.Invoke(this, EventArgs.Empty);
        layout.Controls.Add(loadFontButton, 0, 5);
        layout.SetColumnSpan(loadFontButton, 3);
        FlowLayoutPanel buttons = new()
        {
            AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false
        };
        Button applyButton = CreateButton("Применить");
        applyButton.Click += (_, _) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(folderInput.Text)) throw new ArgumentException("Укажите папку сохранения.");
                _ = SelectedSettings;
                DialogResult = DialogResult.OK;
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException or IOException)
            {
                MessageBox.Show(this, exception.Message, "Settings", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };
        Button cancelButton = CreateButton("Отмена");
        cancelButton.DialogResult = DialogResult.Cancel;
        buttons.Controls.AddRange(new Control[] { cancelButton, applyButton });
        layout.Controls.Add(buttons, 0, 6);
        layout.SetColumnSpan(buttons, 3);
        useWindowSize.CheckedChanged += (_, _) => UpdateSizeInputs();
        UpdateSizeInputs();
        Controls.Add(layout);
        AcceptButton = applyButton;
        CancelButton = cancelButton;
        ResumeLayout(true);
    }

    private void UpdateSizeInputs() => widthInput.Enabled = heightInput.Enabled = !useWindowSize.Checked;

    private static Button CreateButton(string text) => new()
    {
        Text = text, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(10, 6, 10, 6)
    };
}
