using System.Runtime.Versioning;

namespace DockBuddy.Server.Gui;

/// <summary>Edits one macro step: the action and how long to wait before the next step.</summary>
[SupportedOSPlatform("windows")]
public sealed partial class StepForm : Form
{
    private static readonly string[] Types = { "keys", "open", "command", "script", "system", "volume", "media" };

    private static readonly string[] Hints =
    {
        "Keys (e.g. ctrl+c, win+d, media_play_pause)",
        "Program, file, folder or URL",
        "Command line (e.g. ipconfig /all)",
        "Path to a .ps1, .bat, .cmd or .py script",
        "lock, sleep, screenoff, shutdown, restart, logoff",
        "up, down, mute, unmute, toggle-mute, a level 0-100, or mic-mute, mic-unmute, mic-toggle",
        "playpause, next, prev, seek:+10, seek:-10"
    };

    private readonly MacroStep _step;

    public StepForm(MacroStep step)
    {
        _step = step;
        InitializeComponent();
        cboType.Items.AddRange(Types);
        cboType.SelectedIndex = Math.Max(0, Array.IndexOf(Types, step.Type));
        txtValue.Text = step.Value;
        txtArgs.Text = step.Args ?? "";
        numDelay.Value = Math.Clamp(step.DelayMs, 0, (int)numDelay.Maximum);
        UpdateHint();
    }

    private void UpdateHint() => lblHint.Text = cboType.SelectedIndex >= 0 ? Hints[cboType.SelectedIndex] : "";

    private void Accept()
    {
        _step.Type = Types[Math.Max(0, cboType.SelectedIndex)];
        _step.Value = txtValue.Text.Trim();
        _step.Args = string.IsNullOrWhiteSpace(txtArgs.Text) ? null : txtArgs.Text.Trim();
        _step.DelayMs = (int)numDelay.Value;
        DialogResult = DialogResult.OK;
    }
}
