using System.Drawing;
using System.Runtime.Versioning;

namespace DockBuddy.Server.Gui;

[SupportedOSPlatform("windows")]
partial class LayoutForm
{
    private System.ComponentModel.IContainer components = null!;

    private Label lblPage = null!;
    private ComboBox cboPage = null!;
    private Button btnAddPage = null!;
    private Button btnRenamePage = null!;
    private Button btnDeletePage = null!;
    private Button btnExportPage = null!;
    private Button btnImportPage = null!;
    private ToolTip tips = null!;
    private TableLayoutPanel tblBoard = null!;
    private Label lblApp = null!;
    private ComboBox cboApp = null!;
    private Label lblHelp = null!;
    private Button btnClose = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing) components?.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        lblPage = new Label();
        cboPage = new ComboBox();
        btnAddPage = new Button();
        btnRenamePage = new Button();
        btnDeletePage = new Button();
        btnExportPage = new Button();
        btnImportPage = new Button();
        tips = new ToolTip(components);
        tblBoard = new TableLayoutPanel();
        lblApp = new Label();
        cboApp = new ComboBox();
        lblHelp = new Label();
        btnClose = new Button();
        SuspendLayout();

        // lblPage
        lblPage.AutoSize = false;
        lblPage.BackColor = Color.Transparent;
        lblPage.ForeColor = Theme.Muted;
        lblPage.Location = new Point(16, 18);
        lblPage.Name = "lblPage";
        lblPage.Size = new Size(46, 28);
        lblPage.Text = "Page";
        lblPage.TextAlign = ContentAlignment.MiddleLeft;

        // cboPage
        cboPage.DropDownStyle = ComboBoxStyle.DropDownList;
        cboPage.Location = new Point(66, 18);
        cboPage.Name = "cboPage";
        cboPage.Size = new Size(150, 28);
        cboPage.TabIndex = 0;
        cboPage.SelectedIndexChanged += (_, _) => PageChanged();
        Theme.StyleField(cboPage);

        // page buttons
        SetupButton(btnAddPage, "btnAddPage", "Add", 226, 16, 62, 1);
        btnAddPage.Click += (_, _) => AddPage();
        tips.SetToolTip(btnAddPage, "Add an empty page");
        SetupButton(btnRenamePage, "btnRenamePage", "Rename", 294, 16, 80, 2);
        btnRenamePage.Click += (_, _) => RenamePage();
        SetupButton(btnDeletePage, "btnDeletePage", "Delete", 380, 16, 72, 3);
        btnDeletePage.Click += (_, _) => DeletePage();
        SetupButton(btnExportPage, "btnExportPage", "Export…", 458, 16, 94, 9);
        btnExportPage.Click += (_, _) => ExportPage();
        tips.SetToolTip(btnExportPage, "Save this page to a file you can share");
        SetupButton(btnImportPage, "btnImportPage", "Import…", 558, 16, 94, 10);
        btnImportPage.Click += (_, _) => ImportPage();
        tips.SetToolTip(btnImportPage, "Add a page from a file");

        // tblBoard: 7 x 4 cells (cells are created in code)
        tblBoard.BackColor = Color.Black;
        tblBoard.ColumnCount = DeckLayout.Cols;
        tblBoard.RowCount = DeckLayout.Rows;
        for (var i = 0; i < DeckLayout.Cols; i++) tblBoard.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / DeckLayout.Cols));
        for (var i = 0; i < DeckLayout.Rows; i++) tblBoard.RowStyles.Add(new RowStyle(SizeType.Percent, 100F / DeckLayout.Rows));
        tblBoard.Location = new Point(30, 64);
        tblBoard.Name = "tblBoard";
        tblBoard.Padding = new Padding(4);
        tblBoard.Size = new Size(714, 420);
        tblBoard.TabIndex = 6;

        // lblApp
        lblApp.AutoSize = false;
        lblApp.BackColor = Color.Transparent;
        lblApp.ForeColor = Theme.Muted;
        lblApp.Location = new Point(16, 496);
        lblApp.Name = "lblApp";
        lblApp.Size = new Size(330, 28);
        lblApp.Text = "Show this page automatically when this app is in front (optional)";
        lblApp.TextAlign = ContentAlignment.MiddleLeft;

        // cboApp: pick any installed program (or one that is running) or type its process name; "(none)" turns it off
        cboApp.DropDownStyle = ComboBoxStyle.DropDown;
        cboApp.Location = new Point(356, 496);
        cboApp.Name = "cboApp";
        cboApp.Size = new Size(260, 28);
        cboApp.DropDownWidth = 420;
        cboApp.MaxDropDownItems = 16;
        cboApp.TabIndex = 4;
        cboApp.DropDown += (_, _) => FillApps();
        cboApp.SelectionChangeCommitted += (_, _) => AppPicked();
        cboApp.Leave += (_, _) => CommitApp();
        Theme.StyleField(cboApp);

        // lblHelp
        lblHelp.AutoSize = false;
        lblHelp.BackColor = Color.Transparent;
        lblHelp.ForeColor = Theme.Muted;
        lblHelp.Location = new Point(16, 534);
        lblHelp.Name = "lblHelp";
        lblHelp.Size = new Size(620, 40);
        lblHelp.Text = "Click a cell to add or edit a button or widget. Drag a button or widget onto another cell to move it (a button onto a button swaps). " +
                       "Changes are saved straight away and appear on the phone.";

        // btnClose
        SetupButton(btnClose, "btnClose", "Close", 664, 538, 100, 7);
        btnClose.DialogResult = DialogResult.OK;

        // LayoutForm
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Theme.Back;
        CancelButton = btnClose;
        ClientSize = new Size(780, 588);
        Controls.AddRange(new Control[] { lblPage, cboPage, btnAddPage, btnRenamePage, btnDeletePage, btnExportPage, btnImportPage, tblBoard, lblApp, cboApp, lblHelp, btnClose });
        Font = new Font("Segoe UI", 9.5F);
        ForeColor = Color.White;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "LayoutForm";
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "Edit layout";
        ResumeLayout(false);
    }

    private static void SetupButton(Button b, string name, string text, int x, int y, int w, int tab)
    {
        Theme.StyleButton(b);
        b.Location = new Point(x, y);
        b.Name = name;
        b.Size = new Size(w, 32);
        b.TabIndex = tab;
        b.Text = text;
    }
}
