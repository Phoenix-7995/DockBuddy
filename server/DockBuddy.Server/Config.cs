using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DockBuddy.Server;

public class ServerConfig
{
    public string Name { get; set; } = Environment.MachineName;
    public int Port { get; set; } = 8765;
    public int DiscoveryPort { get; set; } = 8766;
    public string Token { get; set; } = "";
}

public class DeckButton
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Label { get; set; } = "";
    public string Icon { get; set; } = "";
    public string Color { get; set; } = "#37474F";
    // open | command | script | keys | system | volume | macro
    public string Type { get; set; } = "command";
    public string Value { get; set; } = "";
    public string? Args { get; set; }
    // type "macro": these run in order (each followed by its delay)
    public List<MacroStep>? Steps { get; set; }
    // Toggle button: the second state. Taps alternate between this button's action and Alt's action;
    // the tile shows the face (label/icon/color) of whichever runs next.
    public DeckButton? Alt { get; set; }
    // Cell on the 7x4 screen grid (row * 7 + col); -1 = let the app place it.
    public int Slot { get; set; } = -1;
}

/// <summary>One action of a macro button.</summary>
public class MacroStep
{
    public string Type { get; set; } = "keys";
    public string Value { get; set; } = "";
    public string? Args { get; set; }
    // Pause after this step, in milliseconds (a macro's total pauses are capped at 30 s).
    public int DelayMs { get; set; }
}

public class DeckPage
{
    // Names this page's file in the pages folder (pages/<id>.json); stays the same when the page is renamed or moved.
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = "Main";
    // Optional: process name (e.g. "chrome"). While that program is in front on the PC, the phone shows this page.
    public string? App { get; set; }
    // The built-in tiles (volume, mute, ...) of this page: where they sit, their size, and whether they are shown.
    public List<DeckWidget> Widgets { get; set; } = new();
    public List<DeckButton> Buttons { get; set; } = new();
}

/// <summary>
/// A built-in tile of the phone screen (volume fader, mute, ...). It sits on the same 7 x 4 grid as the buttons,
/// can be moved and resized, and can be hidden.
/// </summary>
public class DeckWidget
{
    // volume | mute | stats | page | mascot | menu
    public string Kind { get; set; } = "";
    public bool Visible { get; set; } = true;
    public int Col { get; set; }
    public int Row { get; set; }
    public int W { get; set; } = 1;
    public int H { get; set; } = 1;
    public string? Color { get; set; }
    // Per-widget settings declared in WidgetRegistry (key -> value); the phone passes them to the widget.
    public Dictionary<string, string>? Options { get; set; }
}

public class Deck
{
    // 3 = widgets are per page; 2 = 7x4 grid shared by buttons and widgets; 1 = old 5x4 button board
    public int Version { get; set; }
    public List<DeckPage> Pages { get; set; } = new();
    // Only read from decks saved before widgets were per page (version < 3); moved onto the pages on load.
    public List<DeckWidget>? Widgets { get; set; }
}

public class ConfigStore
{
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _dir;
    public string ConfigDir => _dir;
    private readonly object _lock = new();
    /// <summary>Custom button images live here as &lt;name&gt;.png; a button refers to one as "img:&lt;name&gt;".</summary>
    public string IconsDir => Path.Combine(_dir, "icons");

    /// <summary>Full path of a custom icon file, or null if the name is malformed or the file is missing.</summary>
    public string? IconFile(string name)
    {
        if (name.Length is < 1 or > 32 || !name.All(c => char.IsAsciiLetterOrDigit(c))) return null; // blocks path tricks
        var path = Path.Combine(IconsDir, name + ".png");
        return File.Exists(path) ? path : null;
    }

    public ServerConfig Config { get; private set; }
    public Deck Deck { get; private set; }

    public ConfigStore(string? dir = null)
    {
        _dir = dir ?? Path.Combine(AppContext.BaseDirectory);
        Directory.CreateDirectory(_dir);
        Config = Load<ServerConfig>("config.json") ?? new ServerConfig();
        if (string.IsNullOrEmpty(Config.Token))
        {
            Config.Token = Convert.ToHexString(RandomNumberGenerator.GetBytes(4));
            Save("config.json", Config);
        }
        Deck = LoadDeck(out var storedPerPage);
        if (DeckLayout.Migrate(Deck) || !storedPerPage) WriteDeckFiles(Deck); // also converts a one-file deck.json to per-page files
    }

    /// <summary>One JSON file per page lives here (pages/&lt;id&gt;.json); deck.json only lists their order.</summary>
    public string PagesDir => Path.Combine(_dir, "pages");

    private HashSet<string> _savedPageIds = new();

    private static bool SafeId(string id) => id.Length is >= 1 and <= 32 && id.All(char.IsAsciiLetterOrDigit);

    /// <summary>
    /// Reads deck.json. Current format: {"version":n,"pages":["id1","id2"]} with each page in pages/id.json.
    /// Older format: every page inside deck.json itself (storedPerPage = false, so the caller rewrites it).
    /// </summary>
    private Deck LoadDeck(out bool storedPerPage)
    {
        storedPerPage = false;
        var path = Path.Combine(_dir, "deck.json");
        if (!File.Exists(path)) return DefaultDeck();
        try
        {
            var text = File.ReadAllText(path);
            using var doc = JsonDocument.Parse(text);
            var pages = doc.RootElement.TryGetProperty("pages", out var p) ? p : default;
            if (pages.ValueKind == JsonValueKind.Array && pages.GetArrayLength() > 0 && pages[0].ValueKind == JsonValueKind.String)
            {
                var deck = new Deck { Version = doc.RootElement.TryGetProperty("version", out var v) && v.TryGetInt32(out var n) ? n : 0 };
                foreach (var idEl in pages.EnumerateArray())
                {
                    var id = idEl.GetString() ?? "";
                    var page = SafeId(id) ? Load<DeckPage>(Path.Combine("pages", id + ".json")) : null;
                    if (page == null) continue; // a missing or unreadable page file is skipped, the rest still load
                    page.Id = id;
                    deck.Pages.Add(page);
                }
                if (deck.Pages.Count == 0) return DefaultDeck();
                _savedPageIds = deck.Pages.Select(x => x.Id).ToHashSet();
                storedPerPage = true;
                return deck;
            }
            return JsonSerializer.Deserialize<Deck>(text, Json) ?? DefaultDeck();
        }
        catch
        {
            try { File.Copy(path, path + ".bak", overwrite: true); } catch { } // never lose an unreadable deck
            return DefaultDeck();
        }
    }

    /// <summary>Writes every page to its own file and deck.json with the page order; removes files of deleted pages.</summary>
    private void WriteDeckFiles(Deck deck)
    {
        lock (_lock)
        {
            var seen = new HashSet<string>();
            foreach (var page in deck.Pages)
                if (!SafeId(page.Id) || !seen.Add(page.Id)) page.Id = NewPageId(seen);
            Directory.CreateDirectory(PagesDir);
            foreach (var page in deck.Pages)
                File.WriteAllText(Path.Combine(PagesDir, page.Id + ".json"), JsonSerializer.Serialize(page, Json));
            File.WriteAllText(Path.Combine(_dir, "deck.json"),
                JsonSerializer.Serialize(new { version = deck.Version, pages = deck.Pages.Select(x => x.Id).ToList() }, Json));
            foreach (var gone in _savedPageIds.Where(id => !seen.Contains(id)))
                try { File.Delete(Path.Combine(PagesDir, gone + ".json")); } catch { }
            _savedPageIds = seen;
        }
    }

    private static string NewPageId(HashSet<string> taken)
    {
        string id;
        do id = Guid.NewGuid().ToString("N")[..8]; while (!taken.Add(id));
        return id;
    }

    private T? Load<T>(string file)
    {
        var path = Path.Combine(_dir, file);
        if (!File.Exists(path)) return default;
        try { return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Json); }
        catch { return default; }
    }

    private void Save<T>(string file, T value)
    {
        lock (_lock)
            File.WriteAllText(Path.Combine(_dir, file), JsonSerializer.Serialize(value, Json));
    }

    /// <summary>A private copy the layout editor can change without racing the web server's reads.</summary>
    public Deck CloneDeck() => JsonSerializer.Deserialize<Deck>(JsonSerializer.Serialize(Deck, Json), Json)!;

    public void SaveDeck(Deck deck)
    {
        Deck = deck;
        WriteDeckFiles(deck);
    }

    private static DeckButton B(string label, string icon, string color, string type, string value, string? args = null)
        => new() { Label = label, Icon = icon, Color = color, Type = type, Value = value, Args = args };

    // The phone has a built-in volume fader and mute cube, so the default pages don't repeat them.
    public static DeckButton CloneButton(DeckButton b)
        => JsonSerializer.Deserialize<DeckButton>(JsonSerializer.Serialize(b, Json), Json)!;

    public static Deck DefaultDeck() => new()
    {
        Version = DeckLayout.CurrentVersion,
        Pages = new()
        {
            new DeckPage
            {
                Name = "Main",
                Widgets = DeckLayout.DefaultWidgets(),
                Buttons = new()
                {
                    B("Previous", "prev", "#43A047", "keys", "media_prev"),
                    B("Play/Pause", "playpause", "#43A047", "keys", "media_play_pause"),
                    B("Next", "next", "#43A047", "keys", "media_next"),
                    B("Lock PC", "lock", "#6D4C41", "system", "lock"),
                    B("Task Mgr", "chip", "#546E7A", "keys", "ctrl+shift+esc"),
                    B("Desktop", "desktop", "#00897B", "keys", "win+d"),
                    B("Notepad", "app", "#8E24AA", "open", "notepad.exe"),
                    B("Browser", "globe", "#FB8C00", "open", "https://www.google.com"),
                    B("Explorer", "folder", "#0277BD", "open", "explorer.exe"),
                    B("Calculator", "app", "#3949AB", "open", "calc.exe"),
                    B("Screenshot", "screen", "#455A64", "keys", "win+shift+s"),
                    B("Sleep", "sleep", "#5E35B1", "system", "sleep"),
                }
            },
            new DeckPage
            {
                Name = "System",
                Widgets = DeckLayout.DefaultWidgets(),
                Buttons = new()
                {
                    B("Screen Off", "screen", "#455A64", "system", "screenoff"),
                    B("Restart", "restart", "#F4511E", "system", "restart"),
                    B("Shutdown", "power", "#D81B60", "system", "shutdown"),
                    B("Sign out", "lock", "#6D4C41", "system", "logoff"),
                    B("Settings", "chip", "#00897B", "open", "ms-settings:"),
                    B("Ipconfig", "terminal", "#212121", "command", "ipconfig"),
                }
            }
        }
    };
}

/// <summary>
/// The phone screen is a fixed 7 x 4 grid. Every button sits in one cell ("slot" = row * 7 + col) and the
/// built-in widgets cover rectangles of cells; buttons never sit under a widget.
/// </summary>
public static class DeckLayout
{
    public const int Cols = 7, Rows = 4, Cells = Cols * Rows, CurrentVersion = 3;

    public static IEnumerable<string> Kinds => WidgetRegistry.All.Select(d => d.Kind);

    public static string Title(string kind) => WidgetRegistry.Find(kind)?.Title ?? kind;

    /// <summary>Every widget with a DefaultColor has a configurable color (its fill, fader or background).</summary>
    public static bool HasColor(string kind) => WidgetRegistry.Find(kind)?.DefaultColor != null;

    public static string ColorLabel(string kind) => WidgetRegistry.Find(kind)?.ColorLabel ?? "Color";

    public static string DefaultColor(string kind) => WidgetRegistry.Find(kind)?.DefaultColor ?? "#37474F";

    public static List<DeckWidget> DefaultWidgets() => WidgetRegistry.All.Select(d => d.CreateDefault()).ToList();

    public static bool Covers(DeckWidget w, int cell)
        => cell % Cols >= w.Col && cell % Cols < w.Col + w.W && cell / Cols >= w.Row && cell / Cols < w.Row + w.H;

    public static bool[] Blocked(IEnumerable<DeckWidget> widgets)
    {
        var blocked = new bool[Cells];
        foreach (var w in widgets.Where(w => w.Visible))
            for (var i = 0; i < Cells; i++) if (Covers(w, i)) blocked[i] = true;
        return blocked;
    }

    /// <summary>Gives each button of the page a unique free slot (not under one of the page's widgets), placing unplaced ones in the first free cells.</summary>
    public static void Place(DeckPage page)
    {
        var used = Blocked(page.Widgets);
        foreach (var b in page.Buttons)
        {
            if (b.Slot >= 0 && b.Slot < Cells && !used[b.Slot]) used[b.Slot] = true; else b.Slot = -1;
        }
        foreach (var b in page.Buttons.Where(b => b.Slot < 0))
        {
            var free = Array.IndexOf(used, false);
            if (free < 0) break; // page is full; the extra button stays hidden
            used[free] = true;
            b.Slot = free;
        }
    }

    /// <summary>
    /// Makes a loaded deck valid: old 5x4 slots moved over, widgets moved onto the pages, and on every page the
    /// widget list complete, inside the grid and without overlaps. True if anything changed.
    /// </summary>
    public static bool Migrate(Deck deck)
    {
        var changed = false;
        if (deck.Version < 2)
        {
            foreach (var b in deck.Pages.SelectMany(p => p.Buttons))
                b.Slot = b.Slot is >= 0 and < 20 ? (b.Slot / 5) * Cols + b.Slot % 5 + 1 : -1; // old board = columns 1..5
            changed = true;
        }
        if (deck.Version < CurrentVersion)
        {
            // Widgets used to be one set for the whole deck: every page starts with a copy of it.
            foreach (var p in deck.Pages)
                if (p.Widgets.Count == 0 && deck.Widgets is { Count: > 0 })
                    p.Widgets = CloneWidgets(deck.Widgets);
            deck.Widgets = null;
            deck.Version = CurrentVersion;
            changed = true;
        }
        foreach (var p in deck.Pages)
        {
            if (SanitizeWidgets(p)) changed = true;
            Place(p);
        }
        return changed;
    }

    public static List<DeckWidget> CloneWidgets(IEnumerable<DeckWidget> widgets)
        => JsonSerializer.Deserialize<List<DeckWidget>>(JsonSerializer.Serialize(widgets, ConfigStore.Json), ConfigStore.Json)!;

    /// <summary>One entry per registered widget kind, sized and placed on the grid, no two shown ones overlapping.</summary>
    private static bool SanitizeWidgets(DeckPage page)
    {
        var fixedWidgets = new List<DeckWidget>();
        foreach (var def in WidgetRegistry.All)
        {
            var w = page.Widgets.FirstOrDefault(x => x.Kind == def.Kind) ?? def.CreateDefault();
            w.W = Math.Clamp(w.W, 1, Cols);
            w.H = Math.Clamp(w.H, 1, Rows);
            w.Col = Math.Clamp(w.Col, 0, Cols - w.W);
            w.Row = Math.Clamp(w.Row, 0, Rows - w.H);
            if (w.Visible && fixedWidgets.Any(o => o.Visible && Overlaps(o, w))) w.Visible = false;
            fixedWidgets.Add(w);
        }
        var changed = page.Widgets.Count != fixedWidgets.Count;
        page.Widgets = fixedWidgets;
        return changed;
    }

    /// <summary>Makes one page valid on its own (widget list complete and on the grid, buttons in free cells): used when a page is imported.</summary>
    public static void Normalize(DeckPage page)
    {
        SanitizeWidgets(page);
        Place(page);
    }

    /// <summary>A new empty page with the default widget layout.</summary>
    public static DeckPage NewPage(string name) => new() { Name = name, Widgets = DefaultWidgets() };

    public static bool Overlaps(DeckWidget a, DeckWidget b)
        => a.Col < b.Col + b.W && b.Col < a.Col + a.W && a.Row < b.Row + b.H && b.Row < a.Row + a.H;
}
