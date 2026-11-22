package com.dockbuddy.app;

import android.view.MotionEvent;
import android.view.View;

/** A cat on the deck. Each cat is registered with its own {@link CatStyle}. */
class CatWidget extends DeckWidget {
    private final CatTile tile;
    private final int defaultBg;

    CatWidget(DeckHost host, CatStyle style) {
        super(host);
        tile = new CatTile(host.context(), style);
        defaultBg = style.defaultBg;
    }

    @Override public View view() { return tile; }

    @Override public void onConfigChanged(Deck.Widget config, int cellPx) {
        super.onConfigChanged(config, cellPx);
        tile.setColor(color(defaultBg));
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
