package com.dockbuddy.app;

import android.content.Context;
import android.graphics.Canvas;
import android.graphics.Paint;
import android.graphics.RectF;
import android.os.SystemClock;
import android.view.MotionEvent;
import android.view.View;

import org.json.JSONObject;

/**
 * One tile for the PC's media: previous, play/pause and next. Three segments in a row when the widget
 * is wider than tall, stacked otherwise. The middle icon follows what the PC is doing (pause while playing).
 */
class MediaWidget extends DeckWidget {
    private static final String[] COMMANDS = {"prev", "playpause", "next"};

    private final Strip strip;
    private boolean playing;
    private long optimisticUntil; // until then polled state is ignored
    private int taps;

    MediaWidget(DeckHost host) {
        super(host);
        strip = new Strip(host.context());
    }

    @Override public View view() { return strip; }

    @Override public void onConfigChanged(Deck.Widget config, int cellPx) {
        super.onConfigChanged(config, cellPx);
        strip.setTint(color(0x37474F));
    }

    @Override public void onNowPlaying(JSONObject j) {
        boolean now = j != null && "playing".equals(j.optString("status"));
        if (SystemClock.uptimeMillis() < optimisticUntil && now != playing) return; // the PC hasn't caught up with the tap yet
        optimisticUntil = 0;
        if (now == playing) return;
        playing = now;
        strip.setPlaying(now);
    }

    /** Play/pause flips its icon at once and goes back if the PC refuses; previous/next just send. */
    private void send(int segment) {
        final boolean toggles = segment == 1;
        final boolean before = playing;
        final int tap = ++taps;
        if (toggles) {
            playing = !before;
            optimisticUntil = SystemClock.uptimeMillis() + 2500;
            strip.setPlaying(playing);
        }
        try {
            host.api().post("/api/action", new JSONObject().put("type", "media").put("value", COMMANDS[segment]), new ApiClient.Callback() {
                @Override public void onResult(JSONObject j, String error) {
                    boolean ok = error == null && j != null && j.optBoolean("ok");
                    if (ok) return;
                    host.toast(error != null ? error : j.optString("error", "failed"));
                    if (toggles && tap == taps) undo(before);
                }
            });
        } catch (Exception e) {
            host.toast(String.valueOf(e.getMessage()));
            if (toggles) undo(before);
        }
    }

    private void undo(boolean before) {
        playing = before;
        optimisticUntil = 0;
        strip.setPlaying(before);
    }

    private class Strip extends View {
        private final Paint bg = new Paint(Paint.ANTI_ALIAS_FLAG);
        private final Paint press = new Paint(Paint.ANTI_ALIAS_FLAG);
        private final Paint divider = new Paint(Paint.ANTI_ALIAS_FLAG);
        private final RectF rect = new RectF();
        private final RectF seg = new RectF();
        private final float density;
        private final IconDrawable prev = new IconDrawable("prev", "");
        private final IconDrawable play = new IconDrawable("play", "");
        private final IconDrawable pause = new IconDrawable("pause", "");
        private final IconDrawable next = new IconDrawable("next", "");
        private boolean isPlaying;
        private int down = -1;

        Strip(Context c) {
            super(c);
            density = c.getResources().getDisplayMetrics().density;
            press.setColor(0x55000000);
            divider.setColor(0x33FFFFFF);
        }

        void setTint(int color) { bg.setColor(color); invalidate(); }

        void setPlaying(boolean p) { isPlaying = p; invalidate(); }

        private boolean horizontal() { return getWidth() >= getHeight(); }

        /** The bounds of one of the three segments inside the tile. */
        private void segment(int i, RectF out) {
            if (horizontal()) {
                float w = rect.width() / 3f;
                out.set(rect.left + i * w, rect.top, rect.left + (i + 1) * w, rect.bottom);
            } else {
                float h = rect.height() / 3f;
                out.set(rect.left, rect.top + i * h, rect.right, rect.top + (i + 1) * h);
            }
        }

        @Override protected void onDraw(Canvas c) {
            float pad = Tile.pad(density), r = Tile.radius(density);
            rect.set(pad, pad, getWidth() - pad, getHeight() - pad);
            c.drawRoundRect(rect, r, r, bg);
            IconDrawable[] icons = {prev, isPlaying ? pause : play, next};
            for (int i = 0; i < 3; i++) {
                segment(i, seg);
                if (i == down) c.drawRoundRect(seg, r, r, press);
                float size = Math.min(seg.width(), seg.height()) * 0.5f;
                int l = (int) (seg.centerX() - size / 2f), t = (int) (seg.centerY() - size / 2f);
                icons[i].setBounds(l, t, (int) (l + size), (int) (t + size));
                icons[i].draw(c);
                if (i > 0) {
                    float inset = Math.min(seg.width(), seg.height()) * 0.2f;
                    if (horizontal()) c.drawRect(seg.left - density / 2f, seg.top + inset, seg.left + density / 2f, seg.bottom - inset, divider);
                    else c.drawRect(seg.left + inset, seg.top - density / 2f, seg.right - inset, seg.top + density / 2f, divider);
                }
            }
        }

        private int segmentAt(float x, float y) {
            if (!rect.contains(x, y)) return -1;
            float f = horizontal() ? (x - rect.left) / rect.width() : (y - rect.top) / rect.height();
            return Math.min(2, (int) (f * 3));
        }

        @Override public boolean onTouchEvent(MotionEvent e) {
            switch (e.getAction()) {
                case MotionEvent.ACTION_DOWN:
                    down = segmentAt(e.getX(), e.getY());
                    invalidate();
                    return down >= 0;
                case MotionEvent.ACTION_MOVE:
                    if (down >= 0 && segmentAt(e.getX(), e.getY()) != down) { down = -1; invalidate(); }
                    return true;
                case MotionEvent.ACTION_UP:
                    int hit = down;
                    down = -1;
                    invalidate();
                    if (hit >= 0 && segmentAt(e.getX(), e.getY()) == hit) send(hit);
                    return true;
                case MotionEvent.ACTION_CANCEL:
                    down = -1;
                    invalidate();
                    return true;
            }
            return false;
        }
    }
}
