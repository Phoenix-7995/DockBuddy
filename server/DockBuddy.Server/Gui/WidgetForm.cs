using System.Drawing;
using System.Runtime.Versioning;

namespace DockBuddy.Server.Gui;

/// <summary>Configures one built-in widget: how many cells it covers, its color, or remove it from the screen.</summary>
[SupportedOSPlatform("windows")]
public sealed partial class WidgetForm : Form
{
    private static readonly int[] Presets = { 0x1E88E5, 0x43A047, 0xE53935, 0xFB8C00, 0x8E24AA, 0x00897B, 0x546E7A, 0x6D4C41, 0x3949AB, 0xD81B60, 0xFDD835, 0x212121 };

    private readonly string _kind;
    private Color _color;
    private bool _colorChosen;

    private readonly List<(WidgetOption Option, Control Input)> _optionInputs = new();

    public bool Removed { get; private set; }
    public int WidgetWidth => (int)numW.Value;
    public int WidgetHeight => (int)numH.Value;

    /// <summary>The widget's option values (null if its kind has no options).</summary>
    public Dictionary<string, string>? Options { get; private set; }

    /// <summary>The chosen color as #RRGGBB, or the widget's existing value (null = default) if it wasn't changed.</summary>
    public string? ColorHex { get; private set; }

    public WidgetForm(DeckWidget widget)
    {
        _kind = widget.Kind;
        ColorHex = widget.Color;
        InitializeComponent();
        Text = DeckLayout.Title(_kind);
        lblKind.Text = DeckLayout.Title(_kind);
        numW.Maximum = DeckLayout.Cols;
        numH.Maximum = DeckLayout.Rows;
        numW.Value = widget.W;
        numH.Value = widget.H;

        var hasColor = DeckLayout.HasColor(_kind);
        lblColor.Text = DeckLayout.ColorLabel(_kind);
        lblColor.Visible = pnlSwatch.Visible = btnColor.Visible = hasColor;
        _color = Theme.Parse(widget.Color ?? DeckLayout.DefaultColor(_kind), Color.FromArgb(0x37, 0x47, 0x4F));
        pnlSwatch.BackColor = _color;
        Options = widget.Options;
        BuildOptionInputs(widget);
        colorDialog.CustomColors = Presets.Select(c => ((c & 0xFF) << 16) | (c & 0xFF00) | (c >> 16)).ToArray();
    }

    /// <summary>One caption + input per option declared in WidgetRegistry; the buttons below are pushed down to make room.</summary>
    private void BuildOptionInputs(DeckWidget widget)
    {
        var options = WidgetRegistry.Find(_kind)?.Options;
        if (options == null || options.Length == 0) return;
        const int rowHeight = 54;
        var y = 188;
        foreach (var o in options)
        {
            var caption = new Label { Text = o.Label, AutoSize = false, BackColor = Color.Transparent, ForeColor = Theme.Muted, Location = new Point(16, y), Size = new Size(200, 20) };
            var current = widget.Options != null && widget.Options.TryGetValue(o.Key, out var v) ? v : o.Default;
            Control input;
            if (o.Choices is { Length: > 0 })
            {
                var box = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Size = new Size(200, 28) };
                box.Items.AddRange(o.Choices.ToArray<object>());
                box.SelectedItem = current;
                if (box.SelectedIndex < 0) box.SelectedItem = o.Default;
                input = box;
            }
            else input = new TextBox { Text = current, Size = new Size(328, 27) };
            input.Location = new Point(16, y + 20);
            Theme.StyleField(input);
            Controls.AddRange(new[] { caption, input });
            _optionInputs.Add((o, input));
            y += rowHeight;
        }
        var extra = options.Length * rowHeight;
        foreach (var b in new[] { btnRemove, btnCancel, btnOk }) b.Top += extra;
        ClientSize = new Size(ClientSize.Width, ClientSize.Height + extra);
    }

    private void ChooseColor()
    {
        colorDialog.Color = _color;
        if (colorDialog.ShowDialog(this) != DialogResult.OK) return;
        _color = colorDialog.Color;
        _colorChosen = true;
        pnlSwatch.BackColor = _color;
    }

    private void Accept()
    {
        if (_colorChosen) ColorHex = Theme.ToHex(_color);
        if (_optionInputs.Count > 0)
            Options = _optionInputs.ToDictionary(p => p.Option.Key, p => p.Input is ComboBox c ? c.Text : p.Input.Text.Trim());
        DialogResult = DialogResult.OK;
    }

    private void Remove()
    {
        Removed = true;
        DialogResult = DialogResult.OK;
    }
}
