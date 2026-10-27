using System.Drawing;
using System.Runtime.Versioning;

namespace DockBuddy.Server.Gui;

[SupportedOSPlatform("windows")]
partial class StepForm
{
    private System.ComponentModel.IContainer components = null!;

    private Label lblType = null!;
    private ComboBox cboType = null!;
    private Label lblValue = null!;
    private TextBox txtValue = null!;
    private Label lblHint = null!;
    private Label lblArgs = null!;
    private TextBox txtArgs = null!;
    private Label lblDelay = null!;
    private NumericUpDown numDelay = null!;
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
        lblType = new Label();
        cboType = new ComboBox();
        lblValue = new Label();
        txtValue = new TextBox();
        lblHint = new Label();
        lblArgs = new Label();
        txtArgs = new TextBox();
        lblDelay = new Label();
        numDelay = new NumericUpDown();
        btnOk = new Button();
        btnCancel = new Button();
        ((System.ComponentModel.ISupportInitialize)numDelay).BeginInit();
        SuspendLayout();

        Caption(lblType, "lblType", "Action", 16, 14);
        cboType.DropDownStyle = ComboBoxStyle.DropDownList;
        cboType.Location = new Point(16, 34);
        cboType.Name = "cboType";
        cboType.Size = new Size(200, 28);
        cboType.TabIndex = 0;
        cboType.SelectedIndexChanged += (_, _) => UpdateHint();

        Caption(lblValue, "lblValue", "Value", 16, 72);
        txtValue.Location = new Point(16, 92);
        txtValue.Name = "txtValue";
        txtValue.Size = new Size(408, 27);
        txtValue.TabIndex = 1;

        lblHint.AutoSize = false;
        lblHint.BackColor = Color.Transparent;
        lblHint.ForeColor = Theme.Muted;
        lblHint.Location = new Point(16, 120);
        lblHint.Name = "lblHint";
        lblHint.Size = new Size(408, 20);

        Caption(lblArgs, "lblArgs", "Arguments (optional)", 16, 148);
        txtArgs.Location = new Point(16, 168);
        txtArgs.Name = "txtArgs";
        txtArgs.Size = new Size(408, 27);
        txtArgs.TabIndex = 2;

        Caption(lblDelay, "lblDelay", "Wait after this step (ms)", 16, 206);
        numDelay.BackColor = Theme.Field;
        numDelay.ForeColor = Color.White;
        numDelay.BorderStyle = BorderStyle.FixedSingle;
        numDelay.Increment = 100;
        numDelay.Location = new Point(16, 226);
        numDelay.Maximum = 30000;
        numDelay.Name = "numDelay";
        numDelay.Size = new Size(110, 27);
        numDelay.TabIndex = 3;

        foreach (var c in new Control[] { cboType, txtValue, txtArgs }) Theme.StyleField(c);

        Theme.StyleButton(btnCancel);
        btnCancel.DialogResult = DialogResult.Cancel;
        btnCancel.Location = new Point(224, 270);
        btnCancel.Name = "btnCancel";
        btnCancel.Size = new Size(95, 34);
        btnCancel.TabIndex = 5;
        btnCancel.Text = "Cancel";

        Theme.StyleButton(btnOk);
        btnOk.Location = new Point(329, 270);
        btnOk.Name = "btnOk";
        btnOk.Size = new Size(95, 34);
        btnOk.TabIndex = 4;
        btnOk.Text = "Save";
        btnOk.Click += (_, _) => Accept();

        AcceptButton = btnOk;
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Theme.Back;
        CancelButton = btnCancel;
        ClientSize = new Size(440, 320);
        Controls.AddRange(new Control[] { lblType, cboType, lblValue, txtValue, lblHint, lblArgs, txtArgs, lblDelay, numDelay, btnCancel, btnOk });
        Font = new Font("Segoe UI", 9.5F);
        ForeColor = Color.White;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "StepForm";
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "Macro step";
        ((System.ComponentModel.ISupportInitialize)numDelay).EndInit();
        ResumeLayout(false);
    }

    private static void Caption(Label l, string name, string text, int x, int y)
    {
        l.AutoSize = false;
        l.BackColor = Color.Transparent;
        l.ForeColor = Theme.Muted;
        l.Location = new Point(x, y);
        l.Name = name;
        l.Size = new Size(260, 20);
        l.Text = text;
    }
}
