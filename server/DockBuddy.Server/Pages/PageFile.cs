using System.Text.Json;

namespace DockBuddy.Server.Pages;

/// <summary>The shareable file for one page: the page itself plus the custom icon images its buttons use.</summary>
public class PageExport
{
    public string Format { get; set; } = "dockbuddy-page";
    public int Version { get; set; } = 1;
    public DeckPage Page { get; set; } = new();
    // Custom icon images as base64 PNG, keyed by the name the page refers to them by ("img:<name>").
    public Dictionary<string, string>? Icons { get; set; }
}

/// <summary>Turns a page into a self-contained file others can import, and back.</summary>
public static class PageFile
{
    private const int MaxFileBytes = 5_000_000, MaxButtons = 200, MaxIconBytes = 300_000;
    private static readonly HashSet<string> StepTypes = new() { "keys", "open", "command", "script", "system", "volume", "media" };

    private static IEnumerable<DeckButton> AllButtons(DeckPage page)
        => page.Buttons.SelectMany(b => b.Alt == null ? new[] { b } : new[] { b, b.Alt });

    /// <summary>The page as JSON, with its custom icon images embedded.</summary>
    public static string Export(DeckPage page, ConfigStore store)
    {
        var copy = JsonSerializer.Deserialize<DeckPage>(JsonSerializer.Serialize(page, ConfigStore.Json), ConfigStore.Json)!;
        var icons = new Dictionary<string, string>();
        foreach (var b in AllButtons(copy).Where(b => b.Icon.StartsWith("img:")))
        {
            var name = b.Icon[4..];
            var file = store.IconFile(name);
            if (file != null) icons[name] = Convert.ToBase64String(File.ReadAllBytes(file));
            else b.Icon = ""; // image is gone: don't point the recipient at nothing
        }
        return JsonSerializer.Serialize(new PageExport { Page = copy, Icons = icons.Count > 0 ? icons : null }, ConfigStore.Json);
    }

    /// <summary>
    /// Reads a shared page file (or a plain page JSON) into a new page: fresh ids, icons copied into this PC's icons
    /// folder, layout made valid. Throws FormatException with a readable message if the file is not usable.
    /// </summary>
    public static DeckPage Import(string json, ConfigStore store)
    {
        if (json.Length > MaxFileBytes) throw new FormatException("The file is too large to be a DockBuddy page.");
        DeckPage? page;
        Dictionary<string, string>? icons = null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) throw new FormatException("This is not a DockBuddy page file.");
            if (doc.RootElement.TryGetProperty("format", out var f) && f.GetString() == "dockbuddy-page")
            {
                var export = doc.RootElement.Deserialize<PageExport>(ConfigStore.Json);
                page = export?.Page;
                icons = export?.Icons;
            }
            else if (doc.RootElement.TryGetProperty("buttons", out _))
                page = doc.RootElement.Deserialize<DeckPage>(ConfigStore.Json); // a plain page file from the pages folder
            else throw new FormatException("This is not a DockBuddy page file.");
        }
        catch (JsonException) { throw new FormatException("The file is not valid JSON."); }
        if (page == null || page.Buttons == null) throw new FormatException("The file does not contain a page.");
        if (page.Buttons.Count > MaxButtons) throw new FormatException("The page has too many buttons.");

        page.Id = Guid.NewGuid().ToString("N")[..8];
        page.Widgets ??= new();
        if (string.IsNullOrWhiteSpace(page.Name)) page.Name = "Imported";

        var map = new Dictionary<string, string>(); // icon name in the file -> name on this PC
        foreach (var b in AllButtons(page))
        {
            b.Id = Guid.NewGuid().ToString("N")[..8];
            if (b.Steps != null)
            {
                b.Steps.RemoveAll(s => !StepTypes.Contains(s.Type));
                foreach (var s in b.Steps) s.DelayMs = Math.Clamp(s.DelayMs, 0, 30_000);
            }
            if (!b.Icon.StartsWith("img:")) continue;
            var old = b.Icon[4..];
            if (!map.TryGetValue(old, out var made))
            {
                made = icons != null && icons.TryGetValue(old, out var b64) ? SaveIcon(store, b64) : null;
                map[old] = made ?? "";
            }
            b.Icon = map[old].Length > 0 ? "img:" + map[old] : "";
        }
        DeckLayout.Normalize(page);
        return page;
    }

    private static string? SaveIcon(ConfigStore store, string base64)
    {
        try
        {
            var bytes = Convert.FromBase64String(base64);
            if (bytes.Length is 0 or > MaxIconBytes || bytes.Length < 8 || bytes[0] != 0x89 || bytes[1] != 0x50 || bytes[2] != 0x4E || bytes[3] != 0x47) return null; // PNG only
            Directory.CreateDirectory(store.IconsDir);
            var name = Guid.NewGuid().ToString("N")[..12];
            File.WriteAllBytes(Path.Combine(store.IconsDir, name + ".png"), bytes);
            return name;
        }
        catch (FormatException) { return null; }
    }

    /// <summary>Short descriptions of what the page's buttons can do on the PC, so the user can judge a file from someone else.</summary>
    public static List<string> Risks(DeckPage page)
    {
        var all = AllButtons(page).ToList();
        var steps = all.SelectMany(b => b.Steps ?? new()).Select(s => s.Type)
            .Concat(all.Where(b => b.Type != "macro").Select(b => b.Type)).ToList();
        var notes = new List<string>();
        int Count(string t) => steps.Count(x => x == t);
        if (Count("command") > 0) notes.Add($"{Count("command")} run commands");
        if (Count("script") > 0) notes.Add($"{Count("script")} run scripts");
        if (Count("open") > 0) notes.Add($"{Count("open")} open programs, files or web addresses");
        if (Count("system") > 0) notes.Add($"{Count("system")} change power state (lock, sleep, shutdown…)");
        return notes;
    }
}
