package com.dockbuddy.app;

import android.view.View;

/** Opens the app menu (Popcorn mode, PC monitor, Change PC). */
class MenuWidget extends DeckWidget {
    private final ButtonView tile;

    MenuWidget(final DeckHost host) {
        super(host);
        tile = new ButtonView(host.context());
        tile.setOnClickListener(new View.OnClickListener() {
            @Override public void onClick(View v) { host.showMenu(); }
        });
        bind();
    }

    @Override public View view() { return tile; }

    @Override public void onConfigChanged(Deck.Widget config, int cellPx) {
        super.onConfigChanged(config, cellPx);
        bind();
    }

    private void bind() { tile.bind(DeckActivity.tile("Menu", "more", color(0x263238))); }
}
