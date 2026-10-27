using System.Drawing;
using System.Runtime.Versioning;

namespace DockBuddy.Server.Gui;

/// <summary>Edits one deck button: label, icon, color and what it does.</summary>
[SupportedOSPlatform("windows")]
public sealed partial class ButtonEditForm : Form
{
    internal static readonly string[] Icons =
    {
        "", "volume_up", "volume_down", "mute", "play", "pause", "playpause", "next", "prev", "lock", "power", "restart",
        "sleep", "app", "globe", "folder", "terminal", "desktop", "screen", "chip", "mic", "mic_off"
    };

    private static readonly string[] Types = { "open", "command", "script", "keys", "system", "volume", "macro" };

    private static readonly string[] Hints =
    {
        "Program, file, folder or URL (e.g. notepad.exe)",
        "Command line (e.g. ipconfig /all)",
        "Path to a .ps1, .bat, .cmd or .py script",
        "Keys (e.g. ctrl+shift+esc, win+d, media_play_pause, f5)",
        "lock, sleep, hibernate, screenoff, shutdown, restart, logoff",
        "up, down, mute, unmute, toggle-mute, a level 0-100, or mic-mute, mic-unmute, mic-toggle",
        "Several actions in a row: click Edit steps…"
    };

    private static readonly int[] Presets = { 0x1E88E5, 0x43A047, 0xE53935, 0xFB8C00, 0x8E24AA, 0x00897B, 0x546E7A, 0x6D4C41, 0x3949AB, 0xD81B60, 0xFDD835, 0x212121 };

    private readonly DeckButton _button;
    private readonly ConfigStore _store;
    private readonly bool _isAlt;                  // editing the second state of a toggle: no nesting, no delete
    private List<MacroStep> _steps = new();
    private DeckButton? _alt;                      // working copy of the toggle's second state
    private Color _color;
    private string? _pickedImage;   // image file chosen in this dialog, imported on Save
    private int _iconIndex;         // last committed combo selection, restored if the picker is cancelled
    private bool _loading = true;

    public bool Deleted { get; private set; }

    private int CustomIndex => Icons.Length;

    public ButtonEditForm(DeckButton button, bool isNew, ConfigStore store, bool isAlt = false)
    {
        _button = button;
        _store = store;
        _isAlt = isAlt;
        InitializeComponent();
        Text = isAlt ? "Second state" : isNew ? "New button" : "Edit button";
        btnDelete.Visible = !isNew && !isAlt;
        chkToggle.Visible = btnAlt.Visible = !isAlt;
        _steps = (button.Steps ?? new()).Select(s => new MacroStep { Type = s.Type, Value = s.Value, Args = s.Args, DelayMs = s.DelayMs }).ToList();
        if (button.Alt != null) _alt = ConfigStore.CloneButton(button.Alt);
        chkToggle.Checked = _alt != null;
        btnAlt.Enabled = chkToggle.Checked;

        cboIcon.Items.AddRange(Icons.Select(i => i.Length == 0 ? "(none)" : i).ToArray<object>());
        cboIcon.Items.Add("Custom image…");
        cboType.Items.AddRange(Types);
        // Mix in RGB -> BGR for the dialog's custom colors.
        colorDialog.CustomColors = Presets.Select(c => ((c & 0xFF) << 16) | (c & 0xFF00) | (c >> 16)).ToArray();

        txtLabel.Text = button.Label;
        cboIcon.SelectedIndex = IconStore.IsCustom(button.Icon) ? CustomIndex : Math.Max(0, Array.IndexOf(Icons, button.Icon));
        if (cboIcon.SelectedIndex == CustomIndex) cboIcon.Items[CustomIndex] = "Custom image";
        _iconIndex = cboIcon.SelectedIndex;
        cboType.SelectedIndex = Math.Max(0, Array.IndexOf(Types, button.Type));
        txtValue.Text = button.Value;
        txtArgs.Text = button.Args ?? "";
        _color = Theme.Parse(button.Color, Color.FromArgb(0x37, 0x47, 0x4F));
        pnlSwatch.BackColor = _color;
        UpdateHint();
        _loading = false;
    }

    private void OnIconChanged()
    {
        if (_loading) return;
        if (cboIcon.SelectedIndex != CustomIndex) { _iconIndex = cboIcon.SelectedIndex; return; }
        if (openImage.ShowDialog(this) == DialogResult.OK)
        {
            _pickedImage = openImage.FileName;
            _iconIndex = CustomIndex;
            cboIcon.Items[CustomIndex] = "Custom: " + Path.GetFileName(_pickedImage);
        }
        else cboIcon.SelectedIndex = _iconIndex; // cancelled: keep the previous choice
    }

    private void UpdateHint()
    {
        lblHint.Text = cboType.SelectedIndex >= 0 ? Hints[cboType.SelectedIndex] : "";
        var macro = cboType.SelectedIndex >= 0 && Types[cboType.SelectedIndex] == "macro";
        btnSteps.Visible = macro;
        txtValue.Enabled = txtArgs.Enabled = !macro;
    }

    private void EditSteps()
    {
        using var f = new MacroForm(_steps);
        if (f.ShowDialog(this) == DialogResult.OK) _steps = f.Steps;
    }

    private void ToggleChanged()
    {
        btnAlt.Enabled = chkToggle.Checked;
        if (chkToggle.Checked && _alt == null)
        {
            _alt = ConfigStore.CloneButton(_button);
            _alt.Alt = null;
            _alt.Label = txtLabel.Text.Trim().Length > 0 ? txtLabel.Text.Trim() + " (2)" : "Second state";
        }
    }

    private void EditAlt()
    {
        if (_alt == null) return;
        using var f = new ButtonEditForm(_alt, isNew: false, _store, isAlt: true);
        f.ShowDialog(this);
    }

    private void ChooseColor()
    {
        colorDialog.Color = _color;
        if (colorDialog.ShowDialog(this) != DialogResult.OK) return;
        _color = colorDialog.Color;
        pnlSwatch.BackColor = _color;
    }

    /// <summary>Stores the chosen icon on the button; imports a newly picked image and drops the one it replaces.</summary>
    private bool ApplyIcon()
    {
        var old = _button.Icon;
        string icon;
        if (cboIcon.SelectedIndex == CustomIndex)
        {
            if (_pickedImage == null) return true; // unchanged existing custom icon
            try { icon = IconStore.Import(_store, _pickedImage); }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not use that image: " + ex.Message, "DockBuddy", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
        }
        else icon = cboIcon.SelectedIndex <= 0 ? "" : Icons[cboIcon.SelectedIndex];
        _button.Icon = icon;
        if (old != icon) IconStore.Delete(_store, old);
        return true;
    }

    private void Accept()
    {
        var isMacro = Types[Math.Max(0, cboType.SelectedIndex)] == "macro";
        if (isMacro && _steps.Count == 0)
        {
            MessageBox.Show(this, "Add at least one step to the macro.", "DockBuddy", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        _button.Label = txtLabel.Text.Trim();
        if (!ApplyIcon()) return;
        _button.Color = Theme.ToHex(_color);
        _button.Type = Types[Math.Max(0, cboType.SelectedIndex)];
        _button.Value = txtValue.Text.Trim();
        _button.Args = string.IsNullOrWhiteSpace(txtArgs.Text) ? null : txtArgs.Text.Trim();
        _button.Steps = isMacro ? _steps : null;
        if (!_isAlt)
        {
            if (!chkToggle.Checked && _button.Alt != null) IconStore.DeleteAll(_store, _button.Alt);
            _button.Alt = chkToggle.Checked ? _alt : null;
        }
        if (_button.Label.Length == 0) _button.Label = _button.Value.Length > 0 ? _button.Value : "Button";
        DialogResult = DialogResult.OK;
    }
}
