using System.Drawing;
using System.Runtime.Versioning;

namespace DockBuddy.Server.Gui;

[SupportedOSPlatform("windows")]
partial class MainForm
{
    private System.ComponentModel.IContainer components = null!;

    private Label lblTitle = null!;
    private Label lblStatus = null!;
    private Label lblTokenCaption = null!;
    private Label lblToken = null!;
    private Button btnCopyToken = null!;
    private Label lblAddressCaption = null!;
    private TextBox txtAddresses = null!;
    private CheckBox chkAutostart = null!;
    private Button btnMinimize = null!;
    private Button btnEditLayout = null!;
    private Button btnQuit = null!;
    private NotifyIcon trayIcon = null!;
    private ContextMenuStrip trayMenu = null!;
    private ToolStripMenuItem menuOpen = null!;
    private ToolStripMenuItem menuEditLayout = null!;
    private ToolStripMenuItem menuCopyToken = null!;
    private ToolStripSeparator menuSeparator = null!;
    private ToolStripMenuItem menuQuit = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing) components?.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        lblTitle = new Label();
        lblStatus = new Label();
        lblTokenCaption = new Label();
        lblToken = new Label();
        btnCopyToken = new Button();
        lblAddressCaption = new Label();
        txtAddresses = new TextBox();
        chkAutostart = new CheckBox();
        btnMinimize = new Button();
        btnEditLayout = new Button();
        btnQuit = new Button();
        trayMenu = new ContextMenuStrip(components);
        menuOpen = new ToolStripMenuItem();
        menuEditLayout = new ToolStripMenuItem();
        menuCopyToken = new ToolStripMenuItem();
        menuSeparator = new ToolStripSeparator();
        menuQuit = new ToolStripMenuItem();
        trayIcon = new NotifyIcon(components);
        trayMenu.SuspendLayout();
        SuspendLayout();

        // lblTitle
        lblTitle.AutoSize = false;
        lblTitle.BackColor = Color.Transparent;
        lblTitle.Font = new Font("Segoe UI", 18F, FontStyle.Bold);
        lblTitle.ForeColor = Color.White;
        lblTitle.Location = new Point(24, 14);
        lblTitle.Name = "lblTitle";
        lblTitle.Size = new Size(432, 40);
        lblTitle.TabIndex = 0;
        lblTitle.Text = "DockBuddy Server";
        lblTitle.TextAlign = ContentAlignment.MiddleLeft;

        // lblStatus
        lblStatus.AutoSize = false;
        lblStatus.BackColor = Color.Transparent;
        lblStatus.ForeColor = Color.FromArgb(76, 175, 80);
        lblStatus.Location = new Point(24, 58);
        lblStatus.Name = "lblStatus";
        lblStatus.Size = new Size(432, 24);
        lblStatus.TabIndex = 1;
        lblStatus.Text = "● Running";
        lblStatus.TextAlign = ContentAlignment.MiddleLeft;

        // lblTokenCaption
        lblTokenCaption.AutoSize = false;
        lblTokenCaption.BackColor = Color.Transparent;
        lblTokenCaption.ForeColor = Color.FromArgb(160, 160, 160);
        lblTokenCaption.Location = new Point(24, 92);
        lblTokenCaption.Name = "lblTokenCaption";
        lblTokenCaption.Size = new Size(300, 20);
        lblTokenCaption.TabIndex = 2;
        lblTokenCaption.Text = "Pairing token";
        lblTokenCaption.TextAlign = ContentAlignment.MiddleLeft;

        // lblToken
        lblToken.AutoSize = false;
        lblToken.BackColor = Color.FromArgb(34, 34, 34);
        lblToken.Font = new Font("Consolas", 15F, FontStyle.Bold);
        lblToken.ForeColor = Color.White;
        lblToken.Location = new Point(24, 114);
        lblToken.Name = "lblToken";
        lblToken.Padding = new Padding(8, 0, 0, 0);
        lblToken.Size = new Size(300, 34);
        lblToken.TabIndex = 3;
        lblToken.TextAlign = ContentAlignment.MiddleLeft;
        lblToken.UseMnemonic = false;

        // btnCopyToken
        StyleButton(btnCopyToken);
        btnCopyToken.Location = new Point(336, 114);
        btnCopyToken.Name = "btnCopyToken";
        btnCopyToken.Size = new Size(120, 34);
        btnCopyToken.TabIndex = 4;
        btnCopyToken.Text = "Copy token";
        btnCopyToken.Click += (_, _) => CopyToken();

        // lblAddressCaption
        lblAddressCaption.AutoSize = false;
        lblAddressCaption.BackColor = Color.Transparent;
        lblAddressCaption.ForeColor = Color.FromArgb(160, 160, 160);
        lblAddressCaption.Location = new Point(24, 164);
        lblAddressCaption.Name = "lblAddressCaption";
        lblAddressCaption.Size = new Size(432, 20);
        lblAddressCaption.TabIndex = 5;
        lblAddressCaption.Text = "Enter one of these addresses in the app";
        lblAddressCaption.TextAlign = ContentAlignment.MiddleLeft;

        // txtAddresses
        txtAddresses.BackColor = Color.FromArgb(34, 34, 34);
        txtAddresses.BorderStyle = BorderStyle.None;
        txtAddresses.Font = new Font("Consolas", 11F, FontStyle.Bold);
        txtAddresses.ForeColor = Color.White;
        txtAddresses.Location = new Point(24, 186);
        txtAddresses.Multiline = true;
        txtAddresses.Name = "txtAddresses";
        txtAddresses.ReadOnly = true;
        txtAddresses.ScrollBars = ScrollBars.Vertical;
        txtAddresses.Size = new Size(432, 64);
        txtAddresses.TabIndex = 6;

        // chkAutostart
        chkAutostart.AutoSize = false;
        chkAutostart.BackColor = Color.Transparent;
        chkAutostart.ForeColor = Color.White;
        chkAutostart.Location = new Point(24, 266);
        chkAutostart.Name = "chkAutostart";
        chkAutostart.Size = new Size(432, 24);
        chkAutostart.TabIndex = 7;
        chkAutostart.Text = "Start with Windows (minimized to the tray)";
        chkAutostart.UseVisualStyleBackColor = false;
        chkAutostart.CheckedChanged += (_, _) => OnAutostartChanged();

        // btnMinimize
        StyleButton(btnMinimize);
        btnMinimize.Location = new Point(24, 308);
        btnMinimize.Name = "btnMinimize";
        btnMinimize.Size = new Size(140, 34);
        btnMinimize.TabIndex = 8;
        btnMinimize.Text = "Minimize to tray";
        btnMinimize.Click += (_, _) => HideToTray();

        // btnEditLayout
        StyleButton(btnEditLayout);
        btnEditLayout.Location = new Point(176, 308);
        btnEditLayout.Name = "btnEditLayout";
        btnEditLayout.Size = new Size(140, 34);
        btnEditLayout.TabIndex = 9;
        btnEditLayout.Text = "Edit layout…";
        btnEditLayout.Click += (_, _) => EditLayout();

        // btnQuit
        StyleButton(btnQuit);
        btnQuit.Location = new Point(356, 308);
        btnQuit.Name = "btnQuit";
        btnQuit.Size = new Size(100, 34);
        btnQuit.TabIndex = 10;
        btnQuit.Text = "Quit";
        btnQuit.Click += (_, _) => Quit();

        // trayMenu
        trayMenu.Items.AddRange(new ToolStripItem[] { menuOpen, menuEditLayout, menuCopyToken, menuSeparator, menuQuit });
        trayMenu.Name = "trayMenu";
        menuOpen.Name = "menuOpen";
        menuOpen.Text = "Open DockBuddy";
        menuOpen.Click += (_, _) => ShowFromTray();
        menuEditLayout.Name = "menuEditLayout";
        menuEditLayout.Text = "Edit layout…";
        menuEditLayout.Click += (_, _) => { ShowFromTray(); EditLayout(); };
        menuCopyToken.Name = "menuCopyToken";
        menuCopyToken.Text = "Copy token";
        menuCopyToken.Click += (_, _) => CopyToken();
        menuSeparator.Name = "menuSeparator";
        menuQuit.Name = "menuQuit";
        menuQuit.Text = "Quit";
        menuQuit.Click += (_, _) => Quit();

        // trayIcon
        trayIcon.ContextMenuStrip = trayMenu;
        trayIcon.Text = "DockBuddy Server - running";
        trayIcon.Visible = true;
        trayIcon.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) ShowFromTray(); };

        // MainForm
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Color.FromArgb(16, 16, 16);
        ClientSize = new Size(480, 360);
        Controls.Add(lblTitle);
        Controls.Add(lblStatus);
        Controls.Add(lblTokenCaption);
        Controls.Add(lblToken);
        Controls.Add(btnCopyToken);
        Controls.Add(lblAddressCaption);
        Controls.Add(txtAddresses);
        Controls.Add(chkAutostart);
        Controls.Add(btnMinimize);
        Controls.Add(btnEditLayout);
        Controls.Add(btnQuit);
        Font = new Font("Segoe UI", 9.5F);
        ForeColor = Color.White;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        Name = "MainForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "DockBuddy Server";
        trayMenu.ResumeLayout(false);
        ResumeLayout(false);
    }

    private static void StyleButton(Button b)
    {
        b.BackColor = Color.FromArgb(48, 48, 48);
        b.FlatStyle = FlatStyle.Flat;
        b.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 80);
        b.ForeColor = Color.White;
        b.UseVisualStyleBackColor = false;
    }
}
