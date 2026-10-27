package com.dockbuddy.app;

import android.content.Context;
import android.graphics.Canvas;
import android.graphics.Paint;
import android.graphics.RectF;
import android.view.View;

/**
 * A cube showing live numbers. Each row has a label, a value and an optional level bar
 * (percent < 0 means no bar). A tile with a single row is drawn large.
 */
public class StatTile extends View {
    private final Paint bg = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final Paint track = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final Paint fill = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final Paint label = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final Paint value = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final RectF rect = new RectF();
    private final RectF bar = new RectF();
    private final float density;
    private String[] labels = new String[0], values = new String[0];
    private int[] percents = new int[0];

    public StatTile(Context c) {
        super(c);
        density = c.getResources().getDisplayMetrics().density;
        bg.setColor(Tile.BG);
        track.setColor(Tile.TRACK);
        label.setColor(Tile.MUTED);
        value.setColor(Tile.TEXT);
        value.setFakeBoldText(true);
    }

    public void setBackgroundTint(int color) {
        bg.setColor(color);
        invalidate();
    }

    public void setRows(String[] labels, String[] values, int[] percents) {
        this.labels = labels;
        this.values = values;
        this.percents = percents;
        invalidate();
    }

    @Override protected void onDraw(Canvas c) {
        float pad = Tile.pad(density), r = Tile.radius(density);
        rect.set(pad, pad, getWidth() - pad, getHeight() - pad);
        c.drawRoundRect(rect, r, r, bg);
        int n = labels.length;
        if (n == 0) return;
        float inner = 8 * density;
        float left = rect.left + inner, right = rect.right - inner, width = right - left;

        if (n == 1) { drawBig(c, left, right, width); return; }

        float top = rect.top + inner, rowH = Math.min((rect.height() - 2 * inner) / n, 36 * density);
        float size = Math.min(rowH * 0.36f, 13 * density);
        label.setTextSize(size);
        value.setTextSize(size);
        for (int i = 0; i < n; i++) {
            float base = top + i * rowH + size * 1.1f;
            label.setTextAlign(Paint.Align.LEFT);
            value.setTextAlign(Paint.Align.RIGHT);
            label.setTextSize(size);
            value.setTextSize(size);
            String l = fit(label, labels[i], width * 0.5f, size * 0.6f);
            String v = fit(value, values[i], width - label.measureText(l) - 6 * density, size * 0.6f);
            c.drawText(l, left, base, label);
            c.drawText(v, right, base, value);
            if (percents[i] >= 0) drawBar(c, left, right, base + size * 0.45f, 4 * density, percents[i]);
        }
    }

    private void drawBig(Canvas c, float left, float right, float width) {
        label.setTextAlign(Paint.Align.LEFT);
        label.setTextSize(12 * density);
        String l = fit(label, labels[0], width, 7 * density);
        c.drawText(l, left, rect.top + 8 * density + label.getTextSize(), label);

        value.setTextAlign(Paint.Align.CENTER);
        value.setTextSize(Math.min(rect.height() * 0.28f, 30 * density));
        String v = fit(value, values[0], width, 8 * density);
        c.drawText(v, (left + right) / 2f, rect.centerY() + value.getTextSize() * 0.35f, value);
        if (percents[0] >= 0) drawBar(c, left, right, rect.bottom - 14 * density, 6 * density, percents[0]);
    }

    /** Shrinks the paint's text size down to minSize to fit maxWidth, then cuts the text with an ellipsis. */
    private static String fit(Paint p, String s, float maxWidth, float minSize) {
        while (p.measureText(s) > maxWidth && p.getTextSize() > minSize) p.setTextSize(p.getTextSize() - 1);
        if (p.measureText(s) <= maxWidth) return s;
        String t = s;
        while (t.length() > 1 && p.measureText(t + "\u2026") > maxWidth) t = t.substring(0, t.length() - 1);
        return t + "\u2026";
    }

    private void drawBar(Canvas c, float left, float right, float top, float h, int percent) {
        bar.set(left, top, right, top + h);
        c.drawRoundRect(bar, h / 2f, h / 2f, track);
        float w = (right - left) * Math.max(0, Math.min(100, percent)) / 100f;
        if (w <= 0) return;
        bar.set(left, top, left + Math.max(w, h), top + h);
        fill.setColor(Tile.levelColor(percent));
        c.drawRoundRect(bar, h / 2f, h / 2f, fill);
    }
}
