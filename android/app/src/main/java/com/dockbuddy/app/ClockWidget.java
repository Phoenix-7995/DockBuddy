package com.dockbuddy.app;

import android.content.Context;
import android.graphics.Canvas;
import android.graphics.Paint;
import android.graphics.RectF;
import android.os.Handler;
import android.view.View;

import java.text.SimpleDateFormat;
import java.util.Date;
import java.util.Locale;

/**
 * Example of a custom widget: the phone's clock and date. Hidden until you add it in the PC editor.
 * Options (declared in the server's Widgets.cs): "format" = 24h | 12h, "seconds" = off | on.
 *
 * <p>It shows the whole pattern: a view built in the constructor, config and options read through
 * {@link #color} / {@link #option}, and a timer that runs only between onStart and onStop.
 */
class ClockWidget extends DeckWidget {
    private final Handler handler = new Handler();
    private final Face face;
    private boolean running;

    ClockWidget(DeckHost host) {
        super(host);
        face = new Face(host.context());
    }

    @Override public View view() { return face; }

    @Override public void onConfigChanged(Deck.Widget config, int cellPx) {
        super.onConfigChanged(config, cellPx);
        face.setBackgroundTint(color(0x1B2630));
        face.setPatterns(
                (option("format", "24h").equals("12h") ? "h:mm" : "HH:mm") + (option("seconds", "off").equals("on") ? ":ss" : ""),
                "EEE d MMM");
    }

    @Override public void onStart() {
        running = true;
        handler.post(tick);
    }

    @Override public void onStop() {
        running = false;
        handler.removeCallbacks(tick);
    }

    private final Runnable tick = new Runnable() {
        @Override public void run() {
            if (!running) return;
            face.invalidate();
            handler.postDelayed(this, 1000);
        }
    };

    private static class Face extends View {
        private final Paint bg = new Paint(Paint.ANTI_ALIAS_FLAG);
        private final Paint time = new Paint(Paint.ANTI_ALIAS_FLAG);
        private final Paint date = new Paint(Paint.ANTI_ALIAS_FLAG);
        private final RectF rect = new RectF();
        private final float density;
        private SimpleDateFormat timeFormat = new SimpleDateFormat("HH:mm", Locale.getDefault());
        private SimpleDateFormat dateFormat = new SimpleDateFormat("EEE d MMM", Locale.getDefault());

        Face(Context c) {
            super(c);
            density = c.getResources().getDisplayMetrics().density;
            time.setColor(Tile.TEXT);
            time.setFakeBoldText(true);
            time.setTextAlign(Paint.Align.CENTER);
            date.setColor(Tile.MUTED);
            date.setTextAlign(Paint.Align.CENTER);
        }

        void setBackgroundTint(int color) { bg.setColor(color); invalidate(); }

        void setPatterns(String timePattern, String datePattern) {
            timeFormat = new SimpleDateFormat(timePattern, Locale.getDefault());
            dateFormat = new SimpleDateFormat(datePattern, Locale.getDefault());
            invalidate();
        }

        @Override protected void onDraw(Canvas c) {
            float pad = Tile.pad(density), r = Tile.radius(density);
            rect.set(pad, pad, getWidth() - pad, getHeight() - pad);
            c.drawRoundRect(rect, r, r, bg);
            Date now = new Date();
            String t = timeFormat.format(now);
            float size = Math.min(rect.height() * 0.38f, 40 * density);
            time.setTextSize(size);
            while (time.measureText(t) > rect.width() - 8 * density && time.getTextSize() > 10 * density) {
                time.setTextSize(time.getTextSize() - 1);
            }
            c.drawText(t, rect.centerX(), rect.centerY() + time.getTextSize() * 0.1f, time);
            date.setTextSize(Math.min(time.getTextSize() * 0.4f, 14 * density));
            c.drawText(dateFormat.format(now), rect.centerX(), rect.centerY() + time.getTextSize() * 0.1f + date.getTextSize() * 1.6f, date);
        }
    }
}
