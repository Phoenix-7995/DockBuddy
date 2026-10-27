package com.dockbuddy.app;

import android.content.Context;
import android.graphics.Canvas;
import android.graphics.Paint;
import android.view.View;

/** A plain white icon (optionally with a small caption) that dims while pressed. No background cube. */
public class IconButton extends View {
    private final Paint text = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final Paint press = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final float density;
    private IconDrawable icon;
    private String label;

    public IconButton(Context c, String iconName, String label) {
        super(c);
        density = c.getResources().getDisplayMetrics().density;
        this.label = label == null ? "" : label;
        text.setColor(0xFFFFFFFF);
        text.setTextAlign(Paint.Align.CENTER);
        text.setTextSize(11 * density);
        press.setColor(0x33FFFFFF);
        setIcon(iconName);
    }

    public void setIcon(String name) {
        icon = new IconDrawable(name, "");
        invalidate();
    }

    @Override protected void drawableStateChanged() {
        super.drawableStateChanged();
        invalidate();
    }

    @Override protected void onDraw(Canvas c) {
        float w = getWidth(), h = getHeight();
        float captionH = label.length() > 0 ? 16 * density : 0;
        float size = Math.min(w, h - captionH);
        float cx = w / 2f, cy = (h - captionH) / 2f;
        if (isPressed()) c.drawCircle(cx, cy, size * 0.62f, press);
        icon.setBounds((int) (cx - size / 2), (int) (cy - size / 2), (int) (cx + size / 2), (int) (cy + size / 2));
        icon.draw(c);
        if (captionH > 0) c.drawText(label, cx, h - 3 * density, text);
    }
}
