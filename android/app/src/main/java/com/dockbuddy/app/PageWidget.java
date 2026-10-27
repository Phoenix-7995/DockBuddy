package com.dockbuddy.app;

import android.view.View;

/** Shows the current page ("Main 1/2"); tap for the next one. */
class PageWidget extends DeckWidget {
    private final ButtonView tile;
    private Deck deck;
    private int pageIndex;

    PageWidget(final DeckHost host) {
        super(host);
        tile = new ButtonView(host.context());
        tile.setOnClickListener(new View.OnClickListener() {
            @Override public void onClick(View v) { host.nextPage(); }
        });
        bind();
    }

    @Override public View view() { return tile; }

    @Override public void onConfigChanged(Deck.Widget config, int cellPx) {
        super.onConfigChanged(config, cellPx);
        bind();
    }

    @Override public void onPage(Deck deck, int pageIndex) {
        this.deck = deck;
        this.pageIndex = pageIndex;
        bind();
    }

    private void bind() {
        String name = deck == null ? "Page" : deck.pages.get(pageIndex).name + " " + (pageIndex + 1) + "/" + deck.pages.size();
        tile.bind(DeckActivity.tile(name, "pages", color(0x37474F)));
    }
}
