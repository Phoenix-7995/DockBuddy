using System.Drawing;
using System.Runtime.Versioning;

namespace DockBuddy.Server.Gui;

[SupportedOSPlatform("windows")]
partial class MacroForm
{
    private System.ComponentModel.IContainer components = null!;

    private Label lblHelp = null!;
    private ListBox lstSteps = null!;
    private Button btnAdd = null!;
    private Button btnEdit = null!;
    private Button btnRemove = null!;
    private Button btnUp = null!;
    private Button btnDown = null!;
    private Button btnOk = null!;
    private Button btnCancel = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing) components?.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        lblHelp = new Label();
        lstSteps = new ListBox();
        btnAdd = new Button();
        btnEdit = new Button();
        btnRemove = new Button();
        btnUp = new Button();
        btnDown = new Button();
        btnOk = new Button();
        btnCancel = new Button();
        SuspendLayout();

        // lblHelp
        lblHelp.AutoSize = false;
        lblHelp.BackColor = Color.Transparent;
        lblHelp.ForeColor = Theme.Muted;
        lblHelp.Location = new Point(16, 14);
        lblHelp.Name = "lblHelp";
        lblHelp.Size = new Size(488, 20);
        lblHelp.Text = "The steps run top to bottom when the button is tapped (waits add up to at most 30 s).";

        // lstSteps
        lstSteps.BackColor = Theme.Field;
        lstSteps.BorderStyle = BorderStyle.FixedSingle;
        lstSteps.ForeColor = Color.White;
        lstSteps.IntegralHeight = false;
        lstSteps.Location = new Point(16, 40);
        lstSteps.Name = "lstSteps";
        lstSteps.Size = new Size(380, 230);
        lstSteps.TabIndex = 0;
        lstSteps.SelectedIndexChanged += (_, _) => UpdateButtons();
        lstSteps.DoubleClick += (_, _) => EditStep();

        Side(btnAdd, "btnAdd", "Add…", 40, 1);
        btnAdd.Click += (_, _) => AddStep();
        Side(btnEdit, "btnEdit", "Edit…", 80, 2);
        btnEdit.Click += (_, _) => EditStep();
        Side(btnRemove, "btnRemove", "Remove", 120, 3);
        btnRemove.Click += (_, _) => RemoveStep();
        Side(btnUp, "btnUp", "Move up", 170, 4);
        btnUp.Click += (_, _) => MoveStep(-1);
        Side(btnDown, "btnDown", "Move down", 210, 5);
        btnDown.Click += (_, _) => MoveStep(1);

        Theme.StyleButton(btnCancel);
        btnCancel.DialogResult = DialogResult.Cancel;
        btnCancel.Location = new Point(312, 286);
        btnCancel.Name = "btnCancel";
        btnCancel.Size = new Size(95, 34);
        btnCancel.TabIndex = 7;
        btnCancel.Text = "Cancel";

        Theme.StyleButton(btnOk);
        btnOk.DialogResult = DialogResult.OK;
        btnOk.Location = new Point(417, 286);
        btnOk.Name = "btnOk";
        btnOk.Size = new Size(95, 34);
        btnOk.TabIndex = 6;
        btnOk.Text = "Done";

        // MacroForm
        AcceptButton = btnOk;
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Theme.Back;
        CancelButton = btnCancel;
        ClientSize = new Size(528, 336);
        Controls.AddRange(new Control[] { lblHelp, lstSteps, btnAdd, btnEdit, btnRemove, btnUp, btnDown, btnCancel, btnOk });
        Font = new Font("Segoe UI", 9.5F);
        ForeColor = Color.White;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "MacroForm";
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "Macro steps";
        ResumeLayout(false);
    }

    private void Side(Button b, string name, string text, int y, int tab)
    {
        Theme.StyleButton(b);
        b.Location = new Point(408, y);
        b.Name = name;
        b.Size = new Size(104, 32);
        b.TabIndex = tab;
        b.Text = text;
    }
}
