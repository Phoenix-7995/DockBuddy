package com.dockbuddy.app;

import android.content.Context;
import android.graphics.Canvas;
import android.graphics.Paint;
import android.graphics.RectF;
import android.view.MotionEvent;
import android.view.View;

/** A thin horizontal slider with a round knob: used for the playback position and the volume. */
public class LineSlider extends View {

    public interface Listener {
        /** While dragging. */
        void onPreview(int value);
        /** When the finger lifts. */
        void onCommit(int value);
    }

    private final Paint track = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final Paint fill = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final RectF bar = new RectF();
    private final float density;
    private int max = 100, value;
    private boolean dragging, enabledSeek = true;
    private Listener listener;

    public LineSlider(Context c) {
        super(c);
        density = c.getResources().getDisplayMetrics().density;
        track.setColor(0xFF4D4D4D);
        fill.setColor(0xFFFFFFFF);
    }

    public void setListener(Listener l) { listener = l; }

    public void setFillColor(int color) { fill.setColor(color); invalidate(); }

    public void setMax(int max) { this.max = Math.max(1, max); invalidate(); }

    /** A slider with nothing to control (e.g. unknown duration) just draws an empty track. */
    public void setSeekable(boolean seekable) { enabledSeek = seekable; invalidate(); }

    public boolean isDragging() { return dragging; }

    public int getValue() { return value; }

    public void setValue(int v) {
        if (dragging) return;
        value = Math.max(0, Math.min(max, v));
        invalidate();
    }

    @Override protected void onDraw(Canvas c) {
        float side = 10 * density, h = (dragging ? 6 : 4) * density;
        float cy = getHeight() / 2f;
        bar.set(side, cy - h / 2, getWidth() - side, cy + h / 2);
        c.drawRoundRect(bar, h / 2, h / 2, track);
        float x = side + (getWidth() - 2 * side) * value / max;
        if (!enabledSeek) return;
        bar.set(side, cy - h / 2, Math.max(x, side + h), cy + h / 2);
        c.drawRoundRect(bar, h / 2, h / 2, fill);
        c.drawCircle(x, cy, (dragging ? 10 : 7) * density, fill);
    }

    @Override public boolean onTouchEvent(MotionEvent e) {
        if (!enabledSeek) return false;
        switch (e.getAction()) {
            case MotionEvent.ACTION_DOWN:
                dragging = true;
                if (getParent() != null) getParent().requestDisallowInterceptTouchEvent(true);
                moveTo(e.getX());
                return true;
            case MotionEvent.ACTION_MOVE:
                moveTo(e.getX());
                return true;
            case MotionEvent.ACTION_UP:
                moveTo(e.getX());
                dragging = false;
                invalidate();
                if (listener != null) listener.onCommit(value);
                return true;
            case MotionEvent.ACTION_CANCEL:
                dragging = false;
                invalidate();
                return true;
        }
        return super.onTouchEvent(e);
    }

    private void moveTo(float x) {
        float side = 10 * density;
        float f = (x - side) / Math.max(1f, getWidth() - 2 * side);
        value = Math.max(0, Math.min(max, Math.round(f * max)));
        invalidate();
        if (listener != null) listener.onPreview(value);
    }
}
