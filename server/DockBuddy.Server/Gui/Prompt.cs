using System.Drawing;
using System.Runtime.Versioning;

namespace DockBuddy.Server.Gui;

/// <summary>A tiny "type a name" dialog.</summary>
[SupportedOSPlatform("windows")]
internal static class Prompt
{
    public static string? Ask(IWin32Window owner, string title, string initial)
    {
        using var f = new Form
        {
            Text = title, ClientSize = new Size(340, 110), FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent, MaximizeBox = false, MinimizeBox = false, ShowInTaskbar = false,
            BackColor = Theme.Back, ForeColor = Color.White, Font = new Font("Segoe UI", 9.5F)
        };
        var box = new TextBox { Text = initial, Left = 16, Top = 16, Width = 308 };
        Theme.StyleField(box);
        var ok = new Button { Text = "OK", Left = 154, Top = 62, Width = 80, Height = 30, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Cancel", Left = 244, Top = 62, Width = 80, Height = 30, DialogResult = DialogResult.Cancel };
        Theme.StyleButton(ok);
        Theme.StyleButton(cancel);
        f.Controls.AddRange(new Control[] { box, ok, cancel });
        f.AcceptButton = ok;
        f.CancelButton = cancel;
        f.Shown += (_, _) => { box.SelectAll(); box.Focus(); };
        if (f.ShowDialog(owner) != DialogResult.OK) return null;
        var s = box.Text.Trim();
        return s.Length == 0 ? null : s;
    }
}
