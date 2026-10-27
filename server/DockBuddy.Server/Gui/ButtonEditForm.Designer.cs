using System.Drawing;
using System.Runtime.Versioning;

namespace DockBuddy.Server.Gui;

[SupportedOSPlatform("windows")]
partial class ButtonEditForm
{
    private System.ComponentModel.IContainer components = null!;

    private Label lblLabel = null!;
    private TextBox txtLabel = null!;
    private Label lblIcon = null!;
    private ComboBox cboIcon = null!;
    private Label lblColor = null!;
    private Panel pnlSwatch = null!;
    private Button btnColor = null!;
    private Label lblType = null!;
    private ComboBox cboType = null!;
    private Label lblValue = null!;
    private TextBox txtValue = null!;
    private Label lblHint = null!;
    private Label lblArgs = null!;
    private TextBox txtArgs = null!;
    private Button btnOk = null!;
    private Button btnCancel = null!;
    private Button btnDelete = null!;
    private Button btnSteps = null!;
    private CheckBox chkToggle = null!;
    private Button btnAlt = null!;
    private ColorDialog colorDialog = null!;
    private OpenFileDialog openImage = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing) components?.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        lblLabel = new Label();
        txtLabel = new TextBox();
        lblIcon = new Label();
        cboIcon = new ComboBox();
        lblColor = new Label();
        pnlSwatch = new Panel();
        btnColor = new Button();
        lblType = new Label();
        cboType = new ComboBox();
        lblValue = new Label();
        txtValue = new TextBox();
        lblHint = new Label();
        lblArgs = new Label();
        txtArgs = new TextBox();
        btnOk = new Button();
        btnCancel = new Button();
        btnDelete = new Button();
        btnSteps = new Button();
        chkToggle = new CheckBox();
        btnAlt = new Button();
        colorDialog = new ColorDialog();
        openImage = new OpenFileDialog();
        SuspendLayout();

        // captions
        SetCaption(lblLabel, "lblLabel", "Label", 16, 16);
        SetCaption(lblIcon, "lblIcon", "Icon", 16, 70);
        SetCaption(lblColor, "lblColor", "Color", 236, 70);
        SetCaption(lblType, "lblType", "Action", 16, 124);
        SetCaption(lblValue, "lblValue", "Value", 16, 178);
        SetCaption(lblArgs, "lblArgs", "Arguments (optional)", 16, 250);

        // txtLabel
        txtLabel.Location = new Point(16, 36);
        txtLabel.Name = "txtLabel";
        txtLabel.Size = new Size(448, 27);
        txtLabel.TabIndex = 0;

        // cboIcon
        cboIcon.DropDownStyle = ComboBoxStyle.DropDownList;
        cboIcon.Location = new Point(16, 90);
        cboIcon.Name = "cboIcon";
        cboIcon.Size = new Size(200, 28);
        cboIcon.TabIndex = 1;
        cboIcon.SelectedIndexChanged += (_, _) => OnIconChanged();

        // openImage
        openImage.Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.ico|All files|*.*";
        openImage.Title = "Choose an icon image";

        // pnlSwatch
        pnlSwatch.BorderStyle = BorderStyle.FixedSingle;
        pnlSwatch.Location = new Point(236, 90);
        pnlSwatch.Name = "pnlSwatch";
        pnlSwatch.Size = new Size(40, 28);
        pnlSwatch.TabIndex = 2;

        // btnColor
        Theme.StyleButton(btnColor);
        btnColor.Location = new Point(286, 88);
        btnColor.Name = "btnColor";
        btnColor.Size = new Size(110, 32);
        btnColor.TabIndex = 3;
        btnColor.Text = "Choose…";
        btnColor.Click += (_, _) => ChooseColor();

        // cboType
        cboType.DropDownStyle = ComboBoxStyle.DropDownList;
        cboType.Location = new Point(16, 144);
        cboType.Name = "cboType";
        cboType.Size = new Size(200, 28);
        cboType.TabIndex = 4;
        cboType.SelectedIndexChanged += (_, _) => UpdateHint();

        // btnSteps (macro only)
        Theme.StyleButton(btnSteps);
        btnSteps.Location = new Point(236, 142);
        btnSteps.Name = "btnSteps";
        btnSteps.Size = new Size(228, 32);
        btnSteps.TabIndex = 12;
        btnSteps.Text = "Edit steps…";
        btnSteps.Visible = false;
        btnSteps.Click += (_, _) => EditSteps();

        // chkToggle: taps alternate between this action and a second one
        chkToggle.AutoSize = false;
        chkToggle.BackColor = Color.Transparent;
        chkToggle.ForeColor = Color.White;
        chkToggle.Location = new Point(16, 306);
        chkToggle.Name = "chkToggle";
        chkToggle.Size = new Size(280, 28);
        chkToggle.TabIndex = 8;
        chkToggle.Text = "Toggle: taps alternate with a second state";
        chkToggle.CheckedChanged += (_, _) => ToggleChanged();

        // btnAlt
        Theme.StyleButton(btnAlt);
        btnAlt.Location = new Point(304, 304);
        btnAlt.Name = "btnAlt";
        btnAlt.Size = new Size(160, 32);
        btnAlt.TabIndex = 9;
        btnAlt.Text = "Second state…";
        btnAlt.Click += (_, _) => EditAlt();

        // txtValue
        txtValue.Location = new Point(16, 198);
        txtValue.Name = "txtValue";
        txtValue.Size = new Size(448, 27);
        txtValue.TabIndex = 5;

        // lblHint
        lblHint.AutoSize = false;
        lblHint.BackColor = Color.Transparent;
        lblHint.ForeColor = Theme.Muted;
        lblHint.Location = new Point(16, 226);
        lblHint.Name = "lblHint";
        lblHint.Size = new Size(448, 20);
        lblHint.TabIndex = 6;

        // txtArgs
        txtArgs.Location = new Point(16, 270);
        txtArgs.Name = "txtArgs";
        txtArgs.Size = new Size(448, 27);
        txtArgs.TabIndex = 7;

        // buttons
        Theme.StyleButton(btnDelete);
        btnDelete.Location = new Point(16, 356);
        btnDelete.Name = "btnDelete";
        btnDelete.Size = new Size(100, 34);
        btnDelete.TabIndex = 13;
        btnDelete.Text = "Delete";
        btnDelete.Click += (_, _) => { Deleted = true; DialogResult = DialogResult.OK; };

        Theme.StyleButton(btnCancel);
        btnCancel.DialogResult = DialogResult.Cancel;
        btnCancel.Location = new Point(264, 356);
        btnCancel.Name = "btnCancel";
        btnCancel.Size = new Size(95, 34);
        btnCancel.TabIndex = 11;
        btnCancel.Text = "Cancel";

        Theme.StyleButton(btnOk);
        btnOk.Location = new Point(369, 356);
        btnOk.Name = "btnOk";
        btnOk.Size = new Size(95, 34);
        btnOk.TabIndex = 10;
        btnOk.Text = "Save";
        btnOk.Click += (_, _) => Accept();

        foreach (var c in new Control[] { txtLabel, cboIcon, cboType, txtValue, txtArgs }) Theme.StyleField(c);

        // ButtonEditForm
        AcceptButton = btnOk;
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Theme.Back;
        CancelButton = btnCancel;
        ClientSize = new Size(480, 410);
        Controls.AddRange(new Control[]
        {
            lblLabel, txtLabel, lblIcon, cboIcon, lblColor, pnlSwatch, btnColor, lblType, cboType,
            btnSteps, lblValue, txtValue, lblHint, lblArgs, txtArgs, chkToggle, btnAlt, btnDelete, btnCancel, btnOk
        });
        Font = new Font("Segoe UI", 9.5F);
        ForeColor = Color.White;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "ButtonEditForm";
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "Button";
        ResumeLayout(false);
        PerformLayout();
    }

    private static void SetCaption(Label l, string name, string text, int x, int y)
    {
        l.AutoSize = false;
        l.BackColor = Color.Transparent;
        l.ForeColor = Theme.Muted;
        l.Location = new Point(x, y);
        l.Name = name;
        l.Size = new Size(200, 20);
        l.Text = text;
    }
}
