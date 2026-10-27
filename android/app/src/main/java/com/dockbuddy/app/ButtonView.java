package com.dockbuddy.app;

import android.content.Context;
import android.graphics.Bitmap;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.Paint;
import android.graphics.Rect;
import android.graphics.RectF;
import android.view.View;

/** One square deck tile: rounded colored background, icon, and label. */
public class ButtonView extends View {
    private final Paint bg = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final Paint overlay = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final Paint text = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final RectF rect = new RectF();
    private final float density;
    private Deck.Button button;
    private IconDrawable icon;
    private Bitmap image;   // custom image icon, once downloaded
    private final Rect src = new Rect();
    private final Rect dst = new Rect();
    private final Paint bitmapPaint = new Paint(Paint.ANTI_ALIAS_FLAG | Paint.FILTER_BITMAP_FLAG);
    private boolean busy;
    private boolean empty;

    public ButtonView(Context c) {
        super(c);
        density = c.getResources().getDisplayMetrics().density;
        text.setColor(Color.WHITE);
        text.setTextAlign(Paint.Align.CENTER);
        text.setFakeBoldText(true);
        text.setTextSize(13 * density);
        overlay.setColor(0x55000000);
    }

    /** An unused board cell: draws nothing (the layout is edited on the PC). */
    public void bindEmpty() {
        button = null;
        empty = true;
        bg.setColor(0xFF0E1317);
        invalidate();
    }

    public void bind(Deck.Button b) {
        empty = false;
        button = b;
        icon = new IconDrawable(IconCache.isCustom(b.icon) ? "" : b.icon, b.label);
        image = null;
        if (IconCache.isCustom(b.icon)) {
            final String key = b.icon;
            image = IconCache.get(getContext(), key, new IconCache.Ready() {
                @Override public void onReady() {
                    if (button != null && key.equals(button.icon)) {
                        image = IconCache.get(getContext(), key, null);
                        invalidate();
                    }
                }
            });
        }
        try { bg.setColor(Color.parseColor(b.color)); } catch (Exception e) { bg.setColor(0xFF37474F); }
        invalidate();
    }

    public void setBusy(boolean busy) {
        this.busy = busy;
        invalidate();
    }

    @Override protected void onMeasure(int w, int h) {
        int width = MeasureSpec.getSize(w);
        boolean exact = MeasureSpec.getMode(w) == MeasureSpec.EXACTLY && MeasureSpec.getMode(h) == MeasureSpec.EXACTLY;
        setMeasuredDimension(width, exact ? MeasureSpec.getSize(h) : width); // widgets can be wider/taller than one cube
    }

    @Override protected void drawableStateChanged() {
        super.drawableStateChanged();
        invalidate();
    }

    @Override protected void onDraw(Canvas c) {
        if (button == null) return;
        float pad = Tile.pad(density);
        rect.set(pad, pad, getWidth() - pad, getHeight() - pad);
        float r = Tile.radius(density);
        c.drawRoundRect(rect, r, r, bg);
        if (isPressed() || busy) c.drawRoundRect(rect, r, r, overlay);

        float s = Math.min(getWidth(), getHeight()); // a stretched widget is sized by its shorter side
        boolean compact = s < 64 * density; // small cubes: icon only
        float iconSize = s * (compact ? 0.66f : 0.462f);
        float yOff = (getHeight() - s) / 2f;
        float top = compact ? (getHeight() - iconSize) / 2f : yOff + s * 0.16f;
        float left = (getWidth() - iconSize) / 2f;
        if (image != null) {
            // Fit the picture inside the icon square, keeping its proportions.
            float k = Math.min(iconSize / image.getWidth(), iconSize / image.getHeight());
            int w = Math.round(image.getWidth() * k), h = Math.round(image.getHeight() * k);
            int l = (int) (left + (iconSize - w) / 2f), t = (int) (top + (iconSize - h) / 2f);
            src.set(0, 0, image.getWidth(), image.getHeight());
            dst.set(l, t, l + w, t + h);
            c.drawBitmap(image, src, dst, bitmapPaint);
        } else {
            icon.setBounds((int) left, (int) top, (int) (left + iconSize), (int) (top + iconSize));
            icon.draw(c);
        }

        if (compact) return;
        String label = button.label;
        text.setTextSize(Math.min(14 * density, s * 0.13f));
        while (label.length() > 3 && text.measureText(label) > getWidth() - 2 * pad - 4 * density) {
            label = label.substring(0, label.length() - 2) + "…";
        }
        c.drawText(label, getWidth() / 2f, yOff + s - 14 * density, text);
    }
}
