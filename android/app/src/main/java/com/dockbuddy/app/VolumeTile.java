package com.dockbuddy.app;

import android.content.Context;
import android.graphics.Canvas;
import android.graphics.Paint;
import android.graphics.RectF;
import android.view.MotionEvent;
import android.view.View;

/** A cube-styled vertical fader: drag up and down to set the PC volume. */
public class VolumeTile extends View {

    public interface Listener {
        void onVolumeChosen(int level);
    }

    private final Paint bg = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final Paint fill = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final Paint text = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final RectF rect = new RectF();
    private final RectF bar = new RectF();
    private final float density;
    private final IconDrawable speaker = new IconDrawable("volume_up", "");
    private final IconDrawable muteIcon = new IconDrawable("mute", "");
    private int accent = Tile.ACCENT;
    private int level;
    private boolean muted;
    private boolean dragging;
    private Listener listener;

    public VolumeTile(Context c) {
        super(c);
        density = c.getResources().getDisplayMetrics().density;
        bg.setColor(Tile.BG);
        text.setColor(Tile.TEXT);
        text.setTextAlign(Paint.Align.CENTER);
        text.setFakeBoldText(true);
        text.setTextSize(20 * density);
    }

    public void setListener(Listener l) { listener = l; }

    /** The color of the filled part of the fader. */
    public void setAccent(int color) {
        accent = color;
        invalidate();
    }

    public boolean isDragging() { return dragging; }

    public void setLevel(int level) {
        if (dragging) return;
        this.level = Math.max(0, Math.min(100, level));
        invalidate();
    }

    public void setMuted(boolean muted) {
        this.muted = muted;
        invalidate();
    }

    /** A widget wider than tall becomes a horizontal fader. */
    private boolean isHorizontal() { return getWidth() > getHeight(); }

    @Override protected void onDraw(Canvas c) {
        float pad = Tile.pad(density), r = Tile.radius(density);
        rect.set(pad, pad, getWidth() - pad, getHeight() - pad);
        c.drawRoundRect(rect, r, r, bg);

        boolean horiz = isHorizontal();
        float fh = (horiz ? rect.width() : rect.height()) * level / 100f;
        if (fh > 0) {
            if (horiz) bar.set(rect.left, rect.top, rect.left + fh, rect.bottom);
            else bar.set(rect.left, rect.bottom - fh, rect.right, rect.bottom);
            fill.setColor(muted ? 0xFF546E7A : accent);
            float fr = Math.min(r, fh / 2f);
            c.drawRoundRect(bar, fr, fr, fill);
        }

        float icon = Math.min(getWidth(), getHeight()) * 0.418f;
        IconDrawable d = muted ? muteIcon : speaker;
        if (horiz) {
            float left = rect.left + 12 * density, top = rect.centerY() - icon / 2f;
            d.setBounds((int) left, (int) top, (int) (left + icon), (int) (top + icon));
            d.draw(c);
            text.setTextAlign(Paint.Align.RIGHT);
            c.drawText(level + "%", rect.right - 14 * density, rect.centerY() + text.getTextSize() * 0.35f, text);
        } else {
            float left = (getWidth() - icon) / 2f, top = rect.top + 12 * density;
            d.setBounds((int) left, (int) top, (int) (left + icon), (int) (top + icon));
            d.draw(c);
            text.setTextAlign(Paint.Align.CENTER);
            c.drawText(level + "%", getWidth() / 2f, rect.bottom - 14 * density, text);
        }
    }

    @Override public boolean onTouchEvent(MotionEvent e) {
        switch (e.getAction()) {
            case MotionEvent.ACTION_DOWN:
                dragging = true;
                if (getParent() != null) getParent().requestDisallowInterceptTouchEvent(true);
                moveTo(e);
                return true;
            case MotionEvent.ACTION_MOVE:
                moveTo(e);
                return true;
            case MotionEvent.ACTION_UP:
                moveTo(e);
                dragging = false;
                if (listener != null) listener.onVolumeChosen(level);
                return true;
            case MotionEvent.ACTION_CANCEL:
                dragging = false;
                return true;
        }
        return super.onTouchEvent(e);
    }

    private void moveTo(MotionEvent e) {
        float pad = Tile.pad(density);
        float frac;
        if (isHorizontal()) frac = (e.getX() - pad) / (getWidth() - 2 * pad);
        else frac = (getHeight() - pad - e.getY()) / (getHeight() - 2 * pad);
        level = Math.max(0, Math.min(100, Math.round(100f * frac)));
        invalidate();
    }
}
