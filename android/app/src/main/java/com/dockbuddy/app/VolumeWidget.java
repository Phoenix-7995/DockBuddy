package com.dockbuddy.app;

import android.view.View;

import org.json.JSONObject;

/** The PC volume fader (vertical, or horizontal when the widget is wider than tall). */
class VolumeWidget extends DeckWidget {
    private final VolumeTile tile;

    VolumeWidget(final DeckHost host) {
        super(host);
        tile = new VolumeTile(host.context());
        tile.setListener(new VolumeTile.Listener() {
            @Override public void onVolumeChosen(int level) {
                try { host.sendVolume(new JSONObject().put("level", level)); } catch (Exception ignored) { }
            }
        });
    }

    @Override public View view() { return tile; }

    @Override public void onConfigChanged(Deck.Widget config, int cellPx) {
        super.onConfigChanged(config, cellPx);
        tile.setAccent(color(0x1E88E5));
    }

    @Override public void onVolume(int level, boolean muted) {
        tile.setLevel(level);
        tile.setMuted(muted);
    }

    @Override public boolean isBusy() { return tile.isDragging(); }
}
