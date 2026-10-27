using System.Drawing;
using System.Runtime.Versioning;

namespace DockBuddy.Server.Gui;

/// <summary>
/// The server's window. Minimizing or closing it sends it to the system tray and removes it from the
/// taskbar; the server keeps running. Use the tray icon to bring it back or to quit.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed partial class MainForm : Form
{
    private readonly ServerInfo _info;
    private bool _quitting;
    private bool _startHidden;
    private bool _tipShown;
    private bool _loading = true;

    public MainForm(ServerInfo info, bool startHidden)
    {
        _info = info;
        _startHidden = startHidden;

        InitializeComponent();
        Icon = trayIcon.Icon = LoadIcon();
        lblStatus.Text = $"● Running on {info.Name}";
        lblToken.Text = info.Token;
        txtAddresses.Text = info.Addresses.Length > 0
            ? string.Join(Environment.NewLine, info.Addresses.Select(a => $"{a}:{info.Port}"))
            : $"port {info.Port}";
        chkAutostart.Checked = GuiLauncher.IsAutostart();
        _loading = false;
    }

    private static Icon LoadIcon()
    {
        try { return Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application; }
        catch { return SystemIcons.Application; }
    }

    private void OnAutostartChanged()
    {
        if (_loading) return; // initial state, nothing to write
        try { GuiLauncher.SetAutostart(chkAutostart.Checked); }
        catch (Exception ex) { MessageBox.Show(ex.Message, "DockBuddy", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    private void EditLayout()
    {
        using var f = new LayoutForm(_info.Store);
        f.ShowDialog(this);
    }

    private void CopyToken()
    {
        try { Clipboard.SetText(_info.Token); trayIcon.ShowBalloonTip(1500, "DockBuddy", "Token copied", ToolTipIcon.Info); }
        catch { /* clipboard can be locked by another app; nothing useful to do */ }
    }

    private void HideToTray()
    {
        Hide(); // a hidden window has no taskbar button
        if (_tipShown) return;
        _tipShown = true;
        trayIcon.ShowBalloonTip(2500, "DockBuddy is still running", "Click the tray icon to open it again.", ToolTipIcon.Info);
    }

    public void ShowFromTray()
    {
        if (!Visible) Show();
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        Activate();
        BringToFront();
    }

    private void Quit()
    {
        _quitting = true;
        trayIcon.Visible = false;
        trayIcon.Dispose();
        Application.Exit();
    }

    // Start hidden (autostart) without ever flashing the window.
    protected override void SetVisibleCore(bool value)
    {
        if (_startHidden)
        {
            _startHidden = false;
            if (!IsHandleCreated) CreateHandle();
            value = false;
        }
        base.SetVisibleCore(value);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (WindowState == FormWindowState.Minimized) HideToTray();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_quitting && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            HideToTray();
        }
        base.OnFormClosing(e);
    }
}
