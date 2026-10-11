namespace Paint;

internal sealed class TextOptionsForm : Form
{
    private readonly Func<Keys, bool> handleShortcut;

    public TextOptionsForm(Control content, Func<Keys, bool> handleShortcut)
    {
        this.handleShortcut = handleShortcut;
        SuspendLayout();
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 10F);
        Text = "Plain Text";
        FormBorderStyle = FormBorderStyle.FixedToolWindow;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        ControlBox = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Controls.Add(content);
        UiTheme.Apply(this);
        ResumeLayout(true);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if ((keyData == Keys.Escape || (keyData & Keys.Control) != Keys.None)
            && handleShortcut(keyData)) return true;
        return base.ProcessCmdKey(ref msg, keyData);
    }
}
