using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.Versioning;

namespace DockBuddy.Server.Gui;

/// <summary>
/// The layout editor for the 7x4 phone screen: buttons and the built-in widgets (volume, mute, monitor, ...). The PC owns the layout: edits are saved to deck.json immediately and the
/// phone picks them up on its next refresh.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed partial class LayoutForm : Form
{
    private readonly ConfigStore _store;
    private Deck _deck;
    private readonly CellControl[] _cells = new CellControl[DeckLayout.Cells];
    private int _page;
    private bool _loading;
    private CellControl? _down;
    private Point _downAt;

    public LayoutForm(ConfigStore store)
    {
        _store = store;
        _deck = store.CloneDeck();
        if (_deck.Pages.Count == 0) _deck.Pages.Add(DeckLayout.NewPage("Main"));
        InitializeComponent();

        for (var i = 0; i < _cells.Length; i++)
        {
            var c = new CellControl { Index = i, Dock = DockStyle.Fill, Margin = new Padding(4) };
            c.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { _down = (CellControl)s!; _downAt = e.Location; } };
            c.MouseMove += CellMouseMove;
            c.MouseUp += CellMouseUp;
            c.DragEnter += (_, e) => { if (IsDrag(e)) e.Effect = DragDropEffects.Move; };
            c.DragOver += (_, e) => { if (IsDrag(e)) e.Effect = DragDropEffects.Move; };
            c.DragDrop += (s, e) => OnDrop(e, ((CellControl)s!).Index);
            _cells[i] = c;
            tblBoard.Controls.Add(c, i % DeckLayout.Cols, i / DeckLayout.Cols);
        }
        DeckLayout.Migrate(_deck);
        ReloadPages();
    }

    private DeckPage Page => _deck.Pages[_page];

    private void ReloadPages()
    {
        _loading = true;
        cboPage.Items.Clear();
        foreach (var p in _deck.Pages) cboPage.Items.Add(p.Name);
        _page = Math.Clamp(_page, 0, _deck.Pages.Count - 1);
        cboPage.SelectedIndex = _page;
        _loading = false;
        btnDeletePage.Enabled = _deck.Pages.Count > 1;
        Redraw();
        ShowApp();
    }

    private void PageChanged()
    {
        if (_loading || cboPage.SelectedIndex < 0) return;
        CommitApp(); // the page being left
        _page = cboPage.SelectedIndex;
        Redraw();
        ShowApp();
    }

    private void ShowApp()
    {
        _loading = true;
        cboApp.Text = Page.App ?? "";
        _loading = false;
    }

    private const string NoApp = "(none)";

    private void FillApps()
    {
        var typed = cboApp.Text;
        cboApp.Items.Clear();
        cboApp.Items.Add(NoApp);
        var old = Cursor;
        Cursor = Cursors.WaitCursor; // the first scan of the installed programs takes a moment
        try { cboApp.Items.AddRange(InstalledApps.AllWithRunning().ToArray<object>()); }
        finally { Cursor = old; }
        cboApp.Text = typed;
    }

    /// <summary>A program was chosen from the list: store its process name, not the "Name  (process)" text.</summary>
    private void AppPicked()
    {
        var picked = cboApp.SelectedItem;
        BeginInvoke(() =>
        {
            if (picked is AppEntry entry) cboApp.Text = entry.Process;
            CommitApp();
        });
    }

    /// <summary>Saves the app this page is anchored to (empty = not anchored).</summary>
    private void CommitApp()
    {
        if (_loading) return;
        var app = cboApp.Text.Trim();
        if (app == NoApp) app = "";
        if (app.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) app = app[..^4];
        var value = app.Length == 0 ? null : app;
        cboApp.Text = app;
        if (Page.App == value) return;
        Page.App = value;
        Save();
    }

    private void Redraw()
    {
        DeckLayout.Place(Page);
        tblBoard.SuspendLayout();
        foreach (var c in _cells)
        {
            c.Button = null;
            c.Widget = null;
            c.Visible = true;
            tblBoard.SetColumnSpan(c, 1);
            tblBoard.SetRowSpan(c, 1);
        }
        foreach (var w in Page.Widgets.Where(w => w.Visible))
        {
            for (var r = w.Row; r < w.Row + w.H; r++)
                for (var col = w.Col; col < w.Col + w.W; col++)
                    _cells[r * DeckLayout.Cols + col].Visible = false;
            var origin = _cells[w.Row * DeckLayout.Cols + w.Col];
            origin.Visible = true;
            origin.Widget = w;
            tblBoard.SetColumnSpan(origin, w.W);
            tblBoard.SetRowSpan(origin, w.H);
        }
        foreach (var b in Page.Buttons) if (b.Slot >= 0) _cells[b.Slot].Button = b;
        tblBoard.ResumeLayout(true);
        foreach (var c in _cells) c.Invalidate();
    }

    // ---- widgets ----
    private static bool IsDrag(DragEventArgs e)
        => e.Data?.GetDataPresent(typeof(int)) == true || e.Data?.GetDataPresent(DataFormats.StringFormat) == true;

    private void OnDrop(DragEventArgs e, int to)
    {
        if (e.Data?.GetData(typeof(int)) is int from)
        {
            if (_cells[to].Widget == null) MoveButton(from, to);
        }
        else if (e.Data?.GetData(DataFormats.StringFormat) is string s && s.StartsWith("w:"))
        {
            var w = Page.Widgets.FirstOrDefault(x => x.Kind == s[2..]);
            if (w != null) PlaceWidget(w, to);
        }
    }

    /// <summary>Puts the widget with its top-left corner on a cell (pulled in so it stays on the grid) and shows it.</summary>
    private void PlaceWidget(DeckWidget w, int cell)
    {
        TryChange(() =>
        {
            w.Visible = true;
            w.Col = Math.Min(cell % DeckLayout.Cols, DeckLayout.Cols - w.W);
            w.Row = Math.Min(cell / DeckLayout.Cols, DeckLayout.Rows - w.H);
        });
    }

    private void EditWidget(DeckWidget w)
    {
        using var f = new WidgetForm(w);
        if (f.ShowDialog(this) != DialogResult.OK) return;
        TryChange(() =>
        {
            if (f.Removed) { w.Visible = false; return; }
            w.W = f.WidgetWidth;
            w.H = f.WidgetHeight;
            w.Col = Math.Min(w.Col, DeckLayout.Cols - w.W);
            w.Row = Math.Min(w.Row, DeckLayout.Rows - w.H);
            w.Color = f.ColorHex;
            w.Options = f.Options;
        });
    }

    private void ShowAddMenu(CellControl cell)
    {
        var hidden = Page.Widgets.Where(w => !w.Visible).ToList();
        if (hidden.Count == 0) { AddAt(cell.Index); return; }
        var menu = new ContextMenuStrip();
        menu.Items.Add("Add button…", null, (_, _) => AddAt(cell.Index));
        menu.Items.Add(new ToolStripSeparator());
        foreach (var w in hidden)
        {
            var target = w;
            menu.Items.Add("Add " + DeckLayout.Title(target.Kind), null, (_, _) => PlaceWidget(target, cell.Index));
        }
        menu.Closed += (_, _) => BeginInvoke(menu.Dispose);
        menu.Show(Cursor.Position);
    }

    /// <summary>
    /// Applies a widget change if the result is valid (on the grid, no overlaps, no button left without a cell);
    /// otherwise puts everything back and says so.
    /// </summary>
    private bool TryChange(Action change)
    {
        var backup = JsonCopy(_deck);
        var unplacedBefore = Unplaced(backup.Pages[_page]);
        change();
        if (!Fits(Page) || Unplaced(Page) > unplacedBefore)
        {
            _deck = backup;
            Redraw();
            MessageBox.Show(this, "That does not fit: widgets can't overlap each other, and every button needs a free cell on this page.",
                "DockBuddy", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return false;
        }
        Redraw();
        Save();
        return true;
    }

    private static bool Fits(DeckPage page)
    {
        var shown = page.Widgets.Where(w => w.Visible).ToList();
        foreach (var w in shown)
            if (w.Col < 0 || w.Row < 0 || w.Col + w.W > DeckLayout.Cols || w.Row + w.H > DeckLayout.Rows) return false;
        for (var i = 0; i < shown.Count; i++)
            for (var j = i + 1; j < shown.Count; j++)
                if (DeckLayout.Overlaps(shown[i], shown[j])) return false;
        return true;
    }

    /// <summary>Re-places the page's buttons around its widgets and counts the ones that found no cell.</summary>
    private static int Unplaced(DeckPage page)
    {
        DeckLayout.Place(page);
        return page.Buttons.Count(b => b.Slot < 0);
    }

    private void Save()
    {
        // The store gets its own copy so the web server never reads what this form is editing.
        _store.SaveDeck(JsonCopy(_deck));
    }

    private static Deck JsonCopy(Deck d) =>
        System.Text.Json.JsonSerializer.Deserialize<Deck>(System.Text.Json.JsonSerializer.Serialize(d, ConfigStore.Json), ConfigStore.Json)!;

    // ---- cells: click = add/edit, drag = move ----
    private void CellMouseMove(object? sender, MouseEventArgs e)
    {
        if (_down == null || e.Button != MouseButtons.Left || (_down.Button == null && _down.Widget == null)) return;
        var drag = SystemInformation.DragSize;
        if (Math.Abs(e.X - _downAt.X) < drag.Width && Math.Abs(e.Y - _downAt.Y) < drag.Height) return;
        var cell = _down;
        _down = null; // a drag is not a click
        if (cell.Widget != null) cell.DoDragDrop("w:" + cell.Widget.Kind, DragDropEffects.Move);
        else cell.DoDragDrop(cell.Index, DragDropEffects.Move);
    }

    private void CellMouseUp(object? sender, MouseEventArgs e)
    {
        var cell = _down;
        _down = null;
        if (cell == null || e.Button != MouseButtons.Left || !cell.ClientRectangle.Contains(e.Location)) return;
        if (cell.Widget != null) EditWidget(cell.Widget);
        else if (cell.Button == null) ShowAddMenu(cell);
        else EditButton(cell.Button);
    }

    private void MoveButton(int from, int to)
    {
        if (from == to) return;
        var a = Page.Buttons.FirstOrDefault(b => b.Slot == from);
        if (a == null) return;
        var other = Page.Buttons.FirstOrDefault(b => b.Slot == to);
        a.Slot = to;
        if (other != null) other.Slot = from;
        Redraw();
        Save();
    }

    private void AddAt(int slot)
    {
        var b = new DeckButton { Slot = slot, Color = "#37474F", Type = "open" };
        using var f = new ButtonEditForm(b, isNew: true, _store);
        if (f.ShowDialog(this) != DialogResult.OK || f.Deleted) return;
        Page.Buttons.Add(b);
        Redraw();
        Save();
    }

    private void EditButton(DeckButton b)
    {
        using var f = new ButtonEditForm(b, isNew: false, _store);
        if (f.ShowDialog(this) != DialogResult.OK) return;
        if (f.Deleted) { Page.Buttons.Remove(b); IconStore.DeleteAll(_store, b); }
        Redraw();
        Save();
    }

    // ---- pages ----
    private void AddPage()
    {
        var name = Prompt.Ask(this, "New page", "");
        if (name == null) return;
        _deck.Pages.Add(DeckLayout.NewPage(name));
        _page = _deck.Pages.Count - 1;
        ReloadPages();
        Save();
    }

    /// <summary>Saves the current page, with its custom icons, to a file that can be shared.</summary>
    private void ExportPage()
    {
        using var dlg = new SaveFileDialog
        {
            Title = "Export page",
            Filter = "DockBuddy page (*.dbpage.json)|*.dbpage.json|JSON (*.json)|*.json",
            FileName = SafeFileName(Page.Name) + ".dbpage.json",
            OverwritePrompt = true
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try { File.WriteAllText(dlg.FileName, Pages.PageFile.Export(Page, _store)); }
        catch (Exception ex) { MessageBox.Show(this, "Could not save the file: " + ex.Message, "DockBuddy", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    /// <summary>Adds a page from a file someone shared (or one exported earlier). It never replaces an existing page.</summary>
    private void ImportPage()
    {
        using var dlg = new OpenFileDialog { Title = "Import page", Filter = "DockBuddy page (*.json)|*.json|All files|*.*" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        DeckPage page;
        try { page = Pages.PageFile.Import(File.ReadAllText(dlg.FileName), _store); }
        catch (Exception ex) when (ex is FormatException or IOException)
        {
            MessageBox.Show(this, ex.Message, "Import page", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        var risks = Pages.PageFile.Risks(page);
        var question = $"Import the page \"{page.Name}\" with {page.Buttons.Count} buttons?";
        if (risks.Count > 0)
            question += "\n\nOf these, " + string.Join("; ", risks) + " on this PC when tapped. Only import pages from people you trust.";
        if (MessageBox.Show(this, question, "Import page", MessageBoxButtons.YesNo,
                risks.Count > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Question) != DialogResult.Yes)
        {
            foreach (var b in page.Buttons) IconStore.DeleteAll(_store, b); // drop the icons just copied
            return;
        }
        var name = page.Name;
        for (var n = 2; _deck.Pages.Any(p => p.Name == name); n++) name = $"{page.Name} ({n})";
        page.Name = name;
        _deck.Pages.Add(page);
        _page = _deck.Pages.Count - 1;
        ReloadPages();
        Save();
    }

    private static string SafeFileName(string name)
    {
        var bad = Path.GetInvalidFileNameChars();
        var clean = new string(name.Select(c => bad.Contains(c) ? '_' : c).ToArray()).Trim();
        return clean.Length > 0 ? clean : "page";
    }

    private void RenamePage()
    {
        var name = Prompt.Ask(this, "Rename page", Page.Name);
        if (name == null) return;
        Page.Name = name;
        ReloadPages();
        Save();
    }

    private void DeletePage()
    {
        if (_deck.Pages.Count < 2) return;
        var ok = MessageBox.Show(this, $"Delete page \"{Page.Name}\" and its buttons?", "DockBuddy",
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (ok != DialogResult.Yes) return;
        foreach (var b in Page.Buttons) IconStore.DeleteAll(_store, b);
        _deck.Pages.RemoveAt(_page);
        _page = Math.Max(0, _page - 1);
        ReloadPages();
        Save();
    }
}

/// <summary>One cell of the editor board: a colored tile for a button, or a dim "+" for a free cell.</summary>
[SupportedOSPlatform("windows")]
internal sealed class CellControl : Control
{
    public int Index;
    public DeckButton? Button;
    public DeckWidget? Widget;

    public CellControl()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        AllowDrop = true;
        Cursor = Cursors.Hand;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Color.Black);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = Rounded(r, 12);
        var fill = Widget != null ? WidgetFill(Widget)
            : Button == null ? Color.FromArgb(14, 19, 23) : Theme.Parse(Button.Color, Color.FromArgb(55, 71, 79));
        using (var brush = new SolidBrush(fill)) g.FillPath(brush, path);

        using var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter };
        if (Widget != null)
        {
            using var edge = new Pen(Color.FromArgb(120, 255, 255, 255)) { DashStyle = DashStyle.Dash };
            g.DrawPath(edge, path);
            using var wf = new Font("Segoe UI", 10.5F, FontStyle.Bold);
            g.DrawString(DeckLayout.Title(Widget.Kind), wf, Brushes.White, new Rectangle(4, Height / 2 - 18, Width - 8, 24), center);
            using var ws = new Font("Segoe UI", 8F);
            g.DrawString($"widget {Widget.W}×{Widget.H}", ws, Brushes.Silver, new Rectangle(4, Height / 2 + 4, Width - 8, 18), center);
            return;
        }
        if (Button == null)
        {
            using var f = new Font("Segoe UI", 20F);
            g.DrawString("+", f, Brushes.DimGray, ClientRectangle, center);
            return;
        }
        using var bold = new Font("Segoe UI", 10.5F, FontStyle.Bold);
        g.DrawString(Button.Label, bold, Brushes.White, new Rectangle(4, Height / 2 - 4, Width - 8, Height / 2 - 4), center);
        using var small = new Font("Segoe UI", 8F);
        using var dim = new SolidBrush(Color.FromArgb(190, 255, 255, 255));
        g.DrawString(string.IsNullOrEmpty(Button.Icon) ? Button.Type : Button.Icon, small, dim, new Rectangle(4, 6, Width - 8, Height / 2 - 10), center);
    }

    private static Color WidgetFill(DeckWidget w)
        => DeckLayout.HasColor(w.Kind)
            ? Theme.Parse(w.Color ?? DeckLayout.DefaultColor(w.Kind), Color.FromArgb(55, 71, 79))
            : Color.FromArgb(27, 38, 48);

    private static GraphicsPath Rounded(Rectangle r, int radius)
    {
        var d = radius * 2;
        var p = new GraphicsPath();
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }
}
