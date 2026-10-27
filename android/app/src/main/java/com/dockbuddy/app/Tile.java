package com.dockbuddy.app;

/** Shared look for every cube on the deck. */
final class Tile {
    private Tile() {}

    static final int BG = 0xFF1B2630;
    static final int TRACK = 0xFF2E3B47;
    static final int ACCENT = 0xFF1E88E5;
    static final int TEXT = 0xFFFFFFFF;
    static final int MUTED = 0xFF90A4AE;
    static final int GOOD = 0xFF43A047, WARN = 0xFFFB8C00, BAD = 0xFFE53935;

    /** Gap between neighbouring cubes is twice this. */
    static float pad(float density) { return 4 * density; }

    static float radius(float density) { return 10 * density; }

    static int levelColor(int percent) {
        return percent < 60 ? GOOD : percent < 85 ? WARN : BAD;
    }
}
