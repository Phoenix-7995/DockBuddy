namespace DockBuddy.Server;

/// <summary>A setting a widget offers in the PC layout editor. Shown as a text box, or a drop-down when Choices is set.</summary>
public record WidgetOption(string Key, string Label, string Default, string[]? Choices = null);

/// <summary>
/// Describes one kind of built-in phone widget: its name in the editor, where it sits by default, and
/// what can be configured. The drawing and behaviour live on the phone (a DeckWidget subclass registered
/// in WidgetRegistry.java under the same Kind).
/// </summary>
/// <param name="Kind">Id shared with the phone, e.g. "clock".</param>
/// <param name="Title">Name shown in the PC editor.</param>
/// <param name="Col">Default position (cell column, 0-6), row (0-3) and size in cells.</param>
/// <param name="DefaultColor">#RRGGBB, or null if the widget has no color setting.</param>
/// <param name="ColorLabel">What the color is called in the editor (e.g. "Fader color").</param>
/// <param name="DefaultVisible">False = it starts removed from the screen; add it from a free cell in the editor.</param>
/// <param name="Options">Extra settings shown when the widget is clicked in the editor.</param>
public record WidgetDef(
    string Kind, string Title, int Col, int Row, int W = 1, int H = 1,
    string? DefaultColor = null, bool DefaultVisible = true, WidgetOption[]? Options = null,
    string ColorLabel = "Color")
{
    public DeckWidget CreateDefault() => new()
    {
        Kind = Kind, Col = Col, Row = Row, W = W, H = H, Visible = DefaultVisible
    };
}

/// <summary>
/// All widget kinds. To add a custom widget: (1) add a line below, (2) add a DeckWidget subclass on the
/// phone and register it in WidgetRegistry.java with the same kind. Order = priority when two widgets'
/// default cells collide (the later one starts hidden). See docs/WIDGETS.md.
/// </summary>
public static class WidgetRegistry
{
    private static readonly List<WidgetDef> Defs = new()
    {
        new("volume", "Volume slider", Col: 0, Row: 0, H: 2, DefaultColor: "#1E88E5", ColorLabel: "Fader color"),
        new("mute", "Mute", Col: 0, Row: 2, DefaultColor: "#37474F"),
        new("menu", "Settings", Col: 0, Row: 3, DefaultColor: "#263238"),
        new("stats", "System monitor", Col: 6, Row: 0, H: 2, DefaultColor: "#1B2630", ColorLabel: "Background color"),
        new("mascot", "Mascot", Col: 6, Row: 2, DefaultColor: "#26A69A"),
        new("page", "Profile (page switch)", Col: 6, Row: 3, DefaultColor: "#37474F"),

        // Previous / play-pause / next in one tile (a row when wider than tall, stacked otherwise).
        new("media", "Media controls", Col: 1, Row: 3, W: 3, DefaultColor: "#37474F", DefaultVisible: false),

        // Microphone mute (phone side: MicWidget.java).
        new("mic", "Microphone mute", Col: 1, Row: 3, DefaultColor: "#37474F", DefaultVisible: false),

        // Per-application volume faders (phone side: MixerWidget.java). Best at 3+ cells wide.
        new("mixer", "Audio mixer", Col: 1, Row: 2, W: 3, H: 2, DefaultColor: "#1E88E5", DefaultVisible: false, ColorLabel: "Fader color"),

        // Example of a custom widget with options (phone side: ClockWidget.java).
        new("clock", "Clock", Col: 1, Row: 0, W: 2, DefaultColor: "#1B2630", ColorLabel: "Background color", DefaultVisible: false, Options: new[]
        {
            new WidgetOption("format", "Hours", "24h", new[] { "24h", "12h" }),
            new WidgetOption("seconds", "Seconds", "off", new[] { "off", "on" }),
        }),
    };

    public static IReadOnlyList<WidgetDef> All => Defs;

    public static WidgetDef? Find(string kind) => Defs.FirstOrDefault(d => d.Kind == kind);
}
