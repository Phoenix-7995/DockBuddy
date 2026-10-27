using System.Drawing;
using System.Runtime.Versioning;

namespace DockBuddy.Server.Gui;

[SupportedOSPlatform("windows")]
partial class WidgetForm
{
    private System.ComponentModel.IContainer components = null!;

    private Label lblKind = null!;
    private Label lblSize = null!;
    private NumericUpDown numW = null!;
    private Label lblTimes = null!;
    private NumericUpDown numH = null!;
    private Label lblCells = null!;
    private Label lblColor = null!;
    private Panel pnlSwatch = null!;
    private Button btnColor = null!;
    private Button btnRemove = null!;
    private Button btnCancel = null!;
    private Button btnOk = null!;
    private ColorDialog colorDialog = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing) components?.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        lblKind = new Label();
        lblSize = new Label();
        numW = new NumericUpDown();
        lblTimes = new Label();
        numH = new NumericUpDown();
        lblCells = new Label();
        lblColor = new Label();
        pnlSwatch = new Panel();
        btnColor = new Button();
        btnRemove = new Button();
        btnCancel = new Button();
        btnOk = new Button();
        colorDialog = new ColorDialog();
        ((System.ComponentModel.ISupportInitialize)numW).BeginInit();
        ((System.ComponentModel.ISupportInitialize)numH).BeginInit();
        SuspendLayout();

        // lblKind
        lblKind.AutoSize = false;
        lblKind.BackColor = Color.Transparent;
        lblKind.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
        lblKind.Location = new Point(16, 14);
        lblKind.Name = "lblKind";
        lblKind.Size = new Size(328, 28);

        // size row
        Caption(lblSize, "lblSize", "Size", 16, 56, 200);
        Number(numW, "numW", 16, 78, 0);
        lblTimes.AutoSize = false;
        lblTimes.BackColor = Color.Transparent;
        lblTimes.ForeColor = Theme.Muted;
        lblTimes.Location = new Point(74, 80);
        lblTimes.Name = "lblTimes";
        lblTimes.Size = new Size(20, 24);
        lblTimes.Text = "×";
        Number(numH, "numH", 96, 78, 1);
        lblCells.AutoSize = false;
        lblCells.BackColor = Color.Transparent;
        lblCells.ForeColor = Theme.Muted;
        lblCells.Location = new Point(156, 80);
        lblCells.Name = "lblCells";
        lblCells.Size = new Size(190, 24);
        lblCells.Text = "cells (wide × tall)";

        // color row
        Caption(lblColor, "lblColor", "Color", 16, 118, 200);
        pnlSwatch.BorderStyle = BorderStyle.FixedSingle;
        pnlSwatch.Location = new Point(16, 140);
        pnlSwatch.Name = "pnlSwatch";
        pnlSwatch.Size = new Size(40, 28);
        pnlSwatch.TabIndex = 2;
        Theme.StyleButton(btnColor);
        btnColor.Location = new Point(66, 138);
        btnColor.Name = "btnColor";
        btnColor.Size = new Size(110, 32);
        btnColor.TabIndex = 3;
        btnColor.Text = "Choose…";
        btnColor.Click += (_, _) => ChooseColor();

        // buttons
        Theme.StyleButton(btnRemove);
        btnRemove.Location = new Point(16, 192);
        btnRemove.Name = "btnRemove";
        btnRemove.Size = new Size(110, 34);
        btnRemove.TabIndex = 6;
        btnRemove.Text = "Remove";
        btnRemove.Click += (_, _) => Remove();

        Theme.StyleButton(btnCancel);
        btnCancel.DialogResult = DialogResult.Cancel;
        btnCancel.Location = new Point(152, 192);
        btnCancel.Name = "btnCancel";
        btnCancel.Size = new Size(95, 34);
        btnCancel.TabIndex = 5;
        btnCancel.Text = "Cancel";

        Theme.StyleButton(btnOk);
        btnOk.Location = new Point(257, 192);
        btnOk.Name = "btnOk";
        btnOk.Size = new Size(95, 34);
        btnOk.TabIndex = 4;
        btnOk.Text = "Save";
        btnOk.Click += (_, _) => Accept();

        // WidgetForm
        AcceptButton = btnOk;
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Theme.Back;
        CancelButton = btnCancel;
        ClientSize = new Size(368, 244);
        Controls.AddRange(new Control[]
        {
            lblKind, lblSize, numW, lblTimes, numH, lblCells, lblColor, pnlSwatch, btnColor, btnRemove, btnCancel, btnOk
        });
        Font = new Font("Segoe UI", 9.5F);
        ForeColor = Color.White;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "WidgetForm";
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "Widget";
        ((System.ComponentModel.ISupportInitialize)numW).EndInit();
        ((System.ComponentModel.ISupportInitialize)numH).EndInit();
        ResumeLayout(false);
    }

    private static void Caption(Label l, string name, string text, int x, int y, int w)
    {
        l.AutoSize = false;
        l.BackColor = Color.Transparent;
        l.ForeColor = Theme.Muted;
        l.Location = new Point(x, y);
        l.Name = name;
        l.Size = new Size(w, 20);
        l.Text = text;
    }

    private static void Number(NumericUpDown n, string name, int x, int y, int tab)
    {
        n.BackColor = Theme.Field;
        n.ForeColor = Color.White;
        n.BorderStyle = BorderStyle.FixedSingle;
        n.Location = new Point(x, y);
        n.Minimum = 1;
        n.Maximum = 4;
        n.Name = name;
        n.Size = new Size(54, 27);
        n.TabIndex = tab;
        n.Value = 1;
    }
}
