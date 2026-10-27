package com.dockbuddy.app;

import android.os.Handler;
import android.view.View;

import org.json.JSONObject;

/** Mutes and unmutes the PC's microphone; shows "Mic off" in red while muted. */
class MicWidget extends DeckWidget {
    private static final long POLL_MS = 2000;

    private final Handler handler = new Handler();
    private final ButtonView tile;
    private boolean muted;
    private boolean running;

    MicWidget(final DeckHost host) {
        super(host);
        tile = new ButtonView(host.context());
        tile.setOnClickListener(new View.OnClickListener() {
            @Override public void onClick(View v) { toggle(); }
        });
        bind();
    }

    @Override public View view() { return tile; }

    @Override public void onConfigChanged(Deck.Widget config, int cellPx) {
        super.onConfigChanged(config, cellPx);
        bind();
    }

    @Override public void onStart() {
        running = true;
        handler.post(poll);
    }

    @Override public void onStop() {
        running = false;
        handler.removeCallbacks(poll);
    }

    private final Runnable poll = new Runnable() {
        @Override public void run() {
            if (!running) return;
            host.api().get("/api/mic", new ApiClient.Callback() {
                @Override public void onResult(JSONObject j, String error) { show(j); }
            });
            handler.postDelayed(this, POLL_MS);
        }
    };

    private void toggle() {
        try {
            host.api().post("/api/mic", new JSONObject().put("mute", "toggle"), new ApiClient.Callback() {
                @Override public void onResult(JSONObject j, String error) {
                    if (error != null) host.toast(error); else show(j);
                }
            });
        } catch (Exception e) {
            host.toast(String.valueOf(e.getMessage()));
        }
    }

    private void show(JSONObject j) {
        if (j == null) return;
        muted = j.optBoolean("muted");
        bind();
    }

    private void bind() {
        tile.bind(muted ? DeckActivity.tile("Mic off", "mic_off", 0xE53935) : DeckActivity.tile("Mic on", "mic", color(0x37474F)));
    }
}
