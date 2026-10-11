namespace Paint;

internal sealed class TextInputDialog : Form
{
    private readonly TextBox textInput;
    private readonly NumericUpDown fontSizeInput;
    public string DrawingText => textInput.Text;
    public float FontSize => (float)fontSizeInput.Value;

    public TextInputDialog()
    {
        SuspendLayout();
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 10F);
        Text = "Plain Text";
        ClientSize = new Size(480, 320);
        MinimumSize = new Size(400, 300);
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;

        TableLayoutPanel layout = new()
        {
            Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 4
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Label caption = new() { Text = "Введите текст:", AutoSize = true };
        textInput = new TextBox
        {
            Multiline = true, AcceptsReturn = true, ScrollBars = ScrollBars.Vertical,
            Dock = DockStyle.Fill, MinimumSize = new Size(100, 90), TabIndex = 0
        };
        FlowLayoutPanel sizeRow = new()
        {
            AutoSize = true, Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(0, 6, 0, 6)
        };
        Label sizeCaption = new()
        {
            Text = "Размер шрифта (px):", AutoSize = true, Margin = new Padding(3, 5, 8, 3)
        };
        fontSizeInput = new NumericUpDown
        {
            Minimum = 8, Maximum = 100, Value = 24, Width = 85, TabIndex = 1
        };
        sizeRow.Controls.AddRange(new Control[] { sizeCaption, fontSizeInput });
        FlowLayoutPanel buttons = new()
        {
            AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };
        Button insertButton = new()
        {
            Text = "Добавить", DialogResult = DialogResult.OK, AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(12, 6, 12, 6), TabIndex = 2
        };
        Button cancelButton = new()
        {
            Text = "Отмена", DialogResult = DialogResult.Cancel, AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(12, 6, 12, 6), TabIndex = 3
        };
        buttons.Controls.AddRange(new Control[] { cancelButton, insertButton });
        layout.Controls.Add(caption, 0, 0);
        layout.Controls.Add(textInput, 0, 1);
        layout.Controls.Add(sizeRow, 0, 2);
        layout.Controls.Add(buttons, 0, 3);
        Controls.Add(layout);
        AcceptButton = insertButton;
        CancelButton = cancelButton;
        insertButton.Tag = "primary";
        UiTheme.Apply(this);
        Shown += (_, _) => textInput.Focus();
        ResumeLayout(true);
    }
}
