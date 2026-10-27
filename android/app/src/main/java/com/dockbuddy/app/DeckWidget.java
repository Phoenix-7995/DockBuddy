package com.dockbuddy.app;

import android.graphics.Color;
import android.view.MotionEvent;
import android.view.View;

import org.json.JSONObject;

/**
 * A built-in tile of the deck screen (volume fader, clock, ...). The PC decides where it sits, how big it
 * is and whether it is shown; the widget only draws itself and reacts to events.
 *
 * <p>To make a new one: subclass this, create the view in the constructor, override the events you care
 * about, and register it in {@link WidgetRegistry}. Then add the matching line on the server
 * (Widgets.cs) so it shows up in the PC layout editor. See {@link ClockWidget} for a complete example.
 */
public abstract class DeckWidget {
    protected final DeckHost host;
    /** Placement, color and options as set on the PC. Updated before {@link #onConfigChanged}. */
    protected Deck.Widget config;

    protected DeckWidget(DeckHost host) {
        this.host = host;
    }

    /** The widget's view. The deck sizes and places it; it is created once and reused. */
    public abstract View view();

    /** The widget was placed, resized, recolored or its options changed. cellPx is the size of one grid cube. */
    public void onConfigChanged(Deck.Widget config, int cellPx) {
        this.config = config;
    }

    /** The deck screen is on screen: start timers and animations here. */
    public void onStart() {}

    /** The deck screen was left: stop whatever onStart began. */
    public void onStop() {}

    /** Every ~2 s with /api/stats (cpu, ramPercent, disks, ...); null when the PC can't be reached. */
    public void onStats(JSONObject stats) {}

    /** Every ~2 s and after a volume change, with the PC's volume (0-100) and mute state. */
    public void onVolume(int level, boolean muted) {}

    /** Every ~2 s with /api/nowplaying (status, title, app, isVideo, ...); null when unreachable. */
    public void onNowPlaying(JSONObject nowPlaying) {}

    /** The visible page changed (or the deck was reloaded). */
    public void onPage(Deck deck, int pageIndex) {}

    /** A deck button finished running. */
    public void onButtonResult(Deck.Button button, boolean ok) {}

    /** Every touch anywhere on the screen, in raw screen coordinates (e.g. for an animation that follows the finger). */
    public void onScreenTouch(MotionEvent e) {}

    /** True while the user is interacting; the deck then skips overwriting the widget with polled data. */
    public boolean isBusy() { return false; }

    // ---- helpers for subclasses ----

    /** The color the PC chose for this widget, or the fallback (0xRRGGBB) if none. Returned as opaque ARGB. */
    protected int color(int fallbackRgb) {
        if (config != null && config.color.length() > 0) {
            try { return Color.parseColor(config.color) | 0xFF000000; } catch (Exception ignored) { }
        }
        return 0xFF000000 | fallbackRgb;
    }

    /** A per-widget option set in the PC editor (declared in Widgets.cs), or the fallback. */
    protected String option(String key, String fallback) {
        String v = config == null ? null : config.options.get(key);
        return v == null ? fallback : v;
    }
}
