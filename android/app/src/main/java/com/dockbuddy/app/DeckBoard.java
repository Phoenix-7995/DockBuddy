package com.dockbuddy.app;

import android.content.Context;
import android.view.MotionEvent;
import android.view.ViewConfiguration;
import android.view.ViewGroup;

/**
 * The button board: a fixed COLS x ROWS grid of cells. Tap runs a button. The layout itself is
 * edited on the PC, so the board is read-only.
 */
public class DeckBoard extends ViewGroup {
    public static final int COLS = 7, ROWS = 4, CELLS = COLS * ROWS;

    public interface Listener {
        void onTap(int slot);
    }

    private final ButtonView[] views = new ButtonView[CELLS];
    private final boolean[] filled = new boolean[CELLS];
    private final int slop;
    private Listener listener;
    private int cube;
    private int down = -1;
    private float downX, downY;

    public DeckBoard(Context c) {
        super(c);
        slop = ViewConfiguration.get(c).getScaledTouchSlop();
        for (int i = 0; i < CELLS; i++) {
            views[i] = new ButtonView(c);
            addView(views[i]);
        }
    }

    public void setListener(Listener l) { listener = l; }

    public void setCube(int cube) {
        this.cube = cube;
        requestLayout();
    }

    /** Shows the given buttons by slot; null entries are empty cells. */
    public void setButtons(Deck.Button[] bySlot) {
        for (int i = 0; i < CELLS; i++) {
            filled[i] = bySlot[i] != null;
            if (filled[i]) views[i].bind(bySlot[i]); else views[i].bindEmpty();
            views[i].setPressed(false);
        }
    }

    public ButtonView viewAt(int slot) { return views[slot]; }

    @Override protected void onMeasure(int w, int h) {
        int cs = MeasureSpec.makeMeasureSpec(cube, MeasureSpec.EXACTLY);
        for (ButtonView v : views) v.measure(cs, cs);
        setMeasuredDimension(cube * COLS, cube * ROWS);
    }

    @Override protected void onLayout(boolean changed, int l, int t, int r, int b) {
        for (int i = 0; i < CELLS; i++) {
            int x = (i % COLS) * cube, y = (i / COLS) * cube;
            views[i].layout(x, y, x + cube, y + cube);
        }
    }

    private int cellAt(float x, float y) {
        if (cube <= 0 || x < 0 || y < 0 || x >= cube * COLS || y >= cube * ROWS) return -1;
        return (int) (y / cube) * COLS + (int) (x / cube);
    }

    @Override public boolean onTouchEvent(MotionEvent e) {
        switch (e.getAction()) {
            case MotionEvent.ACTION_DOWN:
                down = cellAt(e.getX(), e.getY());
                if (down < 0 || !filled[down]) { down = -1; return false; }
                downX = e.getX();
                downY = e.getY();
                views[down].setPressed(true);
                return true;
            case MotionEvent.ACTION_MOVE:
                if (down >= 0 && (Math.abs(e.getX() - downX) > slop || Math.abs(e.getY() - downY) > slop)) {
                    views[down].setPressed(false);
                    down = -1;
                }
                return true;
            case MotionEvent.ACTION_UP:
                if (down >= 0) {
                    int tapped = down;
                    views[tapped].setPressed(false);
                    down = -1;
                    if (listener != null && cellAt(e.getX(), e.getY()) == tapped) listener.onTap(tapped);
                }
                return true;
            case MotionEvent.ACTION_CANCEL:
                if (down >= 0) views[down].setPressed(false);
                down = -1;
                return true;
        }
        return true;
    }
}
