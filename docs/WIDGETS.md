# Writing a custom widget

A *widget* is a built-in tile on the phone's 7×4 screen (volume slider, mute, system monitor, ...). The PC
decides where it sits, how big it is and whether it is shown; the phone draws it. A new widget is one line on
the server plus one class on the phone. `clock` is a complete working example — copy it.

## 1. Server: describe it (`server/DockBuddy.Server/Widgets.cs`)

Add a `WidgetDef` to the list in `WidgetRegistry`:

```csharp
new("clock", "Clock", Col: 1, Row: 0, W: 2, DefaultColor: "#1B2630", DefaultVisible: false, Options: new[]
{
    new WidgetOption("format", "Hours", "24h", new[] { "24h", "12h" }),   // drop-down
    new WidgetOption("label",  "Caption", ""),                            // text box (no Choices)
}),
```

| field | meaning |
|---|---|
| `Kind` | id shared with the phone |
| `Title` | name in the PC editor |
| `Col`, `Row`, `W`, `H` | default cell and size (grid is 7 columns × 4 rows) |
| `DefaultColor` | `#RRGGBB`; give one to get a color picker, leave `null` for none |
| `DefaultVisible` | `false` = starts removed; add it from a free cell in **Edit layout…** |
| `Options` | extra settings shown when the widget is clicked in the editor |

That is all the PC needs: the editor can now place, move, resize, recolor, remove and configure it, and the
choices are saved in `deck.json` and sent to the phone.

## 2. Phone: draw it (`android/.../app/`)

Subclass `DeckWidget`, then register it in `WidgetRegistry.java` with the same kind:

```java
class MyWidget extends DeckWidget {
    private final MyView view;
    MyWidget(DeckHost host) { super(host); view = new MyView(host.context()); }
    @Override public View view() { return view; }

    @Override public void onConfigChanged(Deck.Widget config, int cellPx) {
        super.onConfigChanged(config, cellPx);          // keeps `config` current
        view.setColor(color(0x1B2630));                  // PC-chosen color or this default
        String format = option("format", "24h");         // PC-chosen option or this default
    }
}
```

```java
register("my", new Factory() { @Override public DeckWidget create(DeckHost h) { return new MyWidget(h); } });
```

Override only the events you need:

| event | when |
|---|---|
| `onConfigChanged(config, cellPx)` | placed, resized, recolored or options changed (`config.w/h` are in cells) |
| `onStart()` / `onStop()` | deck screen shown / left — start and stop timers here |
| `onStats(json)` | every ~2 s, `/api/stats`; `null` if the PC is unreachable |
| `onVolume(level, muted)` | every ~2 s and after a change |
| `onNowPlaying(json)` | every ~2 s, `/api/nowplaying` |
| `onPage(deck, index)` | the visible page changed |
| `onButtonResult(button, ok)` | a deck button finished |
| `onScreenTouch(event)` | any touch on the screen (raw coordinates) |
| `isBusy()` | return true while the user is dragging, so polled data doesn't fight them |

`DeckHost` gives you `api()` (calls to the PC), `toast`, `openActivity`, `nextPage`, `showMenu` and
`sendVolume`. Draw in pixel coordinates (don't scale the canvas) and avoid `clipPath` so it works on
Android 4.x.

## Per page
Every page has its own set of widgets: each page can show or hide any widget and place or size it differently (a new page starts with the default arrangement). The phone applies the current page's arrangement whenever the page changes.

## Rules the editor enforces
Widgets can't overlap each other, buttons never sit under a widget, and a change that would leave a button
without a free cell is refused.
