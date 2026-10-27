package com.dockbuddy.app;

import android.view.View;

import org.json.JSONObject;

/** Toggles the PC's mute; turns red while muted. */
class MuteWidget extends DeckWidget {
    private final ButtonView tile;
    private boolean muted;

    MuteWidget(final DeckHost host) {
        super(host);
        tile = new ButtonView(host.context());
        tile.setOnClickListener(new View.OnClickListener() {
            @Override public void onClick(View v) {
                try { host.sendVolume(new JSONObject().put("mute", "toggle")); } catch (Exception ignored) { }
            }
        });
        bind();
    }

    @Override public View view() { return tile; }

    @Override public void onConfigChanged(Deck.Widget config, int cellPx) {
        super.onConfigChanged(config, cellPx);
        bind();
    }

    @Override public void onVolume(int level, boolean muted) {
        this.muted = muted;
        bind();
    }

    private void bind() {
        tile.bind(muted ? DeckActivity.tile("Unmute", "mute", 0xE53935) : DeckActivity.tile("Mute", "volume_up", color(0x37474F)));
    }
}
