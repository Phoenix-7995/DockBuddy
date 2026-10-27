package com.dockbuddy.app;

import android.view.MotionEvent;
import android.view.View;

/** The face: eyes follow the finger, and it cheers when a button succeeds. */
class MascotWidget extends DeckWidget {
    private final MascotTile tile;

    MascotWidget(DeckHost host) {
        super(host);
        tile = new MascotTile(host.context());
    }

    @Override public View view() { return tile; }

    @Override public void onConfigChanged(Deck.Widget config, int cellPx) {
        super.onConfigChanged(config, cellPx);
        tile.setColor(color(0x26A69A));
    }

    @Override public void onStart() { tile.start(); }

    @Override public void onStop() { tile.stop(); }

    @Override public void onButtonResult(Deck.Button button, boolean ok) { if (ok) tile.cheer(); }

    @Override public void onScreenTouch(MotionEvent e) {
        int action = e.getAction();
        if (action == MotionEvent.ACTION_DOWN || action == MotionEvent.ACTION_MOVE) tile.lookAt(e.getRawX(), e.getRawY());
        else if (action == MotionEvent.ACTION_UP || action == MotionEvent.ACTION_CANCEL) tile.release();
    }
}
