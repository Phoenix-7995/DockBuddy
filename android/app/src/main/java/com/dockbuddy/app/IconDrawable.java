package com.dockbuddy.app;

import android.graphics.Canvas;
import android.graphics.ColorFilter;
import android.graphics.Matrix;
import android.graphics.Paint;
import android.graphics.Path;
import android.graphics.PixelFormat;
import android.graphics.RectF;
import android.graphics.drawable.Drawable;

/**
 * Simple white line icons drawn with Canvas, so they work on every Android version.
 * Shapes are described in a 1x1 unit square but converted to pixels before drawing:
 * scaling the canvas up instead crashes the hardware renderer on Android 4.4.
 */
public class IconDrawable extends Drawable {

    public static final String[] NAMES = {
            "volume_up", "volume_down", "mute", "play", "pause", "playpause", "next", "prev", "lock", "power", "restart",
            "sleep", "app", "globe", "folder", "terminal", "desktop", "screen", "chip", "mic", "mic_off"
    };

    private final String name;
    private final String fallback;
    private final Paint stroke = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final Paint fill = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final Path path = new Path();
    private final Path scaled = new Path();
    private final Matrix matrix = new Matrix();
    private final RectF rect = new RectF();
    private Canvas cv;
    private float ox, oy, u; // pixel origin and size of the unit square

    public IconDrawable(String name, String fallbackLabel) {
        this.name = name == null ? "" : name;
        this.fallback = fallbackLabel == null || fallbackLabel.length() == 0 ? "?" : fallbackLabel.substring(0, 1).toUpperCase();
        stroke.setColor(0xFFFFFFFF);
        stroke.setStyle(Paint.Style.STROKE);
        stroke.setStrokeCap(Paint.Cap.ROUND);
        stroke.setStrokeJoin(Paint.Join.ROUND);
        fill.setColor(0xFFFFFFFF);
        fill.setStyle(Paint.Style.FILL);
        fill.setTextAlign(Paint.Align.CENTER);
    }

    @Override public void draw(Canvas c) {
        float w = getBounds().width(), h = getBounds().height();
        u = Math.min(w, h);
        ox = getBounds().left + (w - u) / 2f;
        oy = getBounds().top + (h - u) / 2f;
        cv = c;
        stroke.setStrokeWidth(0.07f * u);
        drawIcon();
        cv = null;
    }

    // ---- unit-square -> pixel helpers ----
    private float x(float v) { return ox + v * u; }
    private float y(float v) { return oy + v * u; }

    private void line(float x1, float y1, float x2, float y2) { cv.drawLine(x(x1), y(y1), x(x2), y(y2), stroke); }

    private void setRect(float l, float t, float r, float b) { rect.set(x(l), y(t), x(r), y(b)); }

    private void arc(float l, float t, float r, float b, float start, float sweep) {
        setRect(l, t, r, b);
        cv.drawArc(rect, start, sweep, false, stroke);
    }

    private void rrect(float l, float t, float r, float b, float radius, Paint p) {
        setRect(l, t, r, b);
        cv.drawRoundRect(rect, radius * u, radius * u, p);
    }

    private void oval(float l, float t, float r, float b) {
        setRect(l, t, r, b);
        cv.drawOval(rect, stroke);
    }

    /** Draws the path (already built in pixel coordinates), filled. */
    private void drawPath() {
        cv.drawPath(path, fill);
    }

    private void text(String t, float tx, float ty, float size) {
        fill.setTextSize(size * u);
        fill.setFakeBoldText(true);
        cv.drawText(t, x(tx), y(ty), fill);
    }

    private void speaker() {
        path.reset();
        path.moveTo(x(0.12f), y(0.40f)); path.lineTo(x(0.28f), y(0.40f)); path.lineTo(x(0.48f), y(0.22f));
        path.lineTo(x(0.48f), y(0.78f)); path.lineTo(x(0.28f), y(0.60f)); path.lineTo(x(0.12f), y(0.60f)); path.close();
        drawPath();
    }

    private void wave(float r) { arc(0.48f - r, 0.5f - r, 0.48f + r, 0.5f + r, -45, 90); }

    private void drawIcon() {
        if (name.equals("volume_up")) { speaker(); wave(0.2f); wave(0.36f);
        } else if (name.equals("volume_down")) { speaker(); wave(0.2f);
        } else if (name.equals("mute")) {
            speaker();
            line(0.62f, 0.38f, 0.86f, 0.62f);
            line(0.86f, 0.38f, 0.62f, 0.62f);
        } else if (name.equals("mic") || name.equals("mic_off")) {
            rrect(0.38f, 0.12f, 0.62f, 0.56f, 0.12f, fill);       // capsule
            arc(0.26f, 0.30f, 0.74f, 0.70f, 0, 180);               // holder under it
            line(0.5f, 0.70f, 0.5f, 0.86f);                         // stem
            line(0.36f, 0.86f, 0.64f, 0.86f);                       // base
            if (name.equals("mic_off")) line(0.2f, 0.84f, 0.8f, 0.16f); // slash
        } else if (name.equals("play")) {
            path.reset(); path.moveTo(x(0.28f), y(0.18f)); path.lineTo(x(0.82f), y(0.5f)); path.lineTo(x(0.28f), y(0.82f)); path.close();
            drawPath();
        } else if (name.equals("next") || name.equals("prev")) {
            boolean next = name.equals("next");
            path.reset();
            if (next) { path.moveTo(x(0.17f), y(0.2f)); path.lineTo(x(0.17f), y(0.8f)); path.lineTo(x(0.66f), y(0.5f)); }
            else { path.moveTo(x(0.83f), y(0.2f)); path.lineTo(x(0.83f), y(0.8f)); path.lineTo(x(0.34f), y(0.5f)); }
            path.close();
            drawPath();
            if (next) rrect(0.68f, 0.2f, 0.83f, 0.8f, 0.03f, fill);
            else rrect(0.17f, 0.2f, 0.32f, 0.8f, 0.03f, fill);
        } else if (name.equals("playpause")) {
            path.reset(); path.moveTo(x(0.12f), y(0.2f)); path.lineTo(x(0.12f), y(0.8f)); path.lineTo(x(0.46f), y(0.5f)); path.close();
            drawPath();
            rrect(0.60f, 0.2f, 0.70f, 0.8f, 0.03f, fill);
            rrect(0.76f, 0.2f, 0.86f, 0.8f, 0.03f, fill);
        } else if (name.equals("lock")) {
            rrect(0.22f, 0.45f, 0.78f, 0.85f, 0.06f, fill);
            arc(0.32f, 0.15f, 0.68f, 0.7f, 180, 180);
            line(0.32f, 0.42f, 0.32f, 0.45f);
            line(0.68f, 0.42f, 0.68f, 0.45f);
        } else if (name.equals("power")) {
            arc(0.2f, 0.2f, 0.8f, 0.8f, -60, 300);
            line(0.5f, 0.12f, 0.5f, 0.48f);
        } else if (name.equals("restart")) {
            arc(0.2f, 0.2f, 0.8f, 0.8f, -50, 290);
            path.reset(); path.moveTo(x(0.62f), y(0.1f)); path.lineTo(x(0.86f), y(0.3f)); path.lineTo(x(0.56f), y(0.34f)); path.close();
            drawPath();
        } else if (name.equals("sleep")) {
            text("Zz", 0.5f, 0.66f, 0.5f);
        } else if (name.equals("app")) {
            for (int i = 0; i < 2; i++) for (int k = 0; k < 2; k++) {
                float l = 0.18f + i * 0.34f, t = 0.18f + k * 0.34f;
                rrect(l, t, l + 0.28f, t + 0.28f, 0.05f, fill);
            }
        } else if (name.equals("globe")) {
            cv.drawCircle(x(0.5f), y(0.5f), 0.34f * u, stroke);
            oval(0.34f, 0.16f, 0.66f, 0.84f);
            line(0.16f, 0.5f, 0.84f, 0.5f);
        } else if (name.equals("folder")) {
            path.reset();
            path.moveTo(x(0.12f), y(0.24f)); path.lineTo(x(0.40f), y(0.24f)); path.lineTo(x(0.48f), y(0.34f)); path.lineTo(x(0.88f), y(0.34f));
            path.lineTo(x(0.88f), y(0.78f)); path.lineTo(x(0.12f), y(0.78f)); path.close();
            int alpha = fill.getAlpha();
            fill.setAlpha(alpha * 150 / 255);     // back panel is translucent so the folder has depth
            drawPath();
            fill.setAlpha(alpha);
            rrect(0.12f, 0.42f, 0.88f, 0.78f, 0.04f, fill);   // solid front panel
        } else if (name.equals("terminal")) {
            rrect(0.12f, 0.22f, 0.88f, 0.78f, 0.06f, stroke);
            text(">_", 0.5f, 0.6f, 0.3f);
        } else if (name.equals("desktop") || name.equals("screen")) {
            rrect(0.12f, 0.18f, 0.88f, 0.64f, 0.04f, stroke);
            line(0.5f, 0.64f, 0.5f, 0.8f);
            line(0.3f, 0.82f, 0.7f, 0.82f);
            if (name.equals("screen")) line(0.2f, 0.72f, 0.8f, 0.1f);
        } else if (name.equals("chip")) {
            setRect(0.28f, 0.28f, 0.72f, 0.72f);
            cv.drawRect(rect, stroke);
            for (int i = 0; i < 3; i++) {
                float p = 0.38f + i * 0.12f;
                line(p, 0.14f, p, 0.28f); line(p, 0.72f, p, 0.86f);
                line(0.14f, p, 0.28f, p); line(0.72f, p, 0.86f, p);
            }
        } else if (name.equals("back10") || name.equals("fwd10")) {
            boolean fwd = name.equals("fwd10");
            arc(0.16f, 0.16f, 0.84f, 0.84f, fwd ? -50 : -60, 290);
            path.reset();
            if (fwd) { path.moveTo(x(0.62f), y(0.04f)); path.lineTo(x(0.88f), y(0.26f)); path.lineTo(x(0.56f), y(0.32f)); }
            else { path.moveTo(x(0.38f), y(0.04f)); path.lineTo(x(0.12f), y(0.26f)); path.lineTo(x(0.44f), y(0.32f)); }
            path.close();
            drawPath();
            text("10", 0.5f, 0.62f, 0.3f);
        } else if (name.equals("pause")) {
            rrect(0.24f, 0.18f, 0.42f, 0.82f, 0.04f, fill);
            rrect(0.58f, 0.18f, 0.76f, 0.82f, 0.04f, fill);
        } else if (name.equals("rewind") || name.equals("forward")) {
            boolean fwd = name.equals("forward");
            path.reset();
            for (int i = 0; i < 2; i++) {
                float o = i * 0.34f;
                if (fwd) { path.moveTo(x(0.14f + o), y(0.22f)); path.lineTo(x(0.48f + o), y(0.5f)); path.lineTo(x(0.14f + o), y(0.78f)); }
                else { path.moveTo(x(0.86f - o), y(0.22f)); path.lineTo(x(0.52f - o), y(0.5f)); path.lineTo(x(0.86f - o), y(0.78f)); }
                path.close();
            }
            drawPath();
        } else if (name.equals("fullscreen")) {
            float[][] c = {{0.18f, 0.18f, 1, 1}, {0.82f, 0.18f, -1, 1}, {0.18f, 0.82f, 1, -1}, {0.82f, 0.82f, -1, -1}};
            for (float[] k : c) {
                line(k[0], k[1], k[0] + 0.2f * k[2], k[1]);
                line(k[0], k[1], k[0], k[1] + 0.2f * k[3]);
            }
        } else if (name.equals("close")) {
            line(0.24f, 0.24f, 0.76f, 0.76f);
            line(0.76f, 0.24f, 0.24f, 0.76f);
        } else if (name.equals("pages")) {
            rrect(0.30f, 0.14f, 0.84f, 0.66f, 0.06f, stroke);
            rrect(0.16f, 0.34f, 0.70f, 0.86f, 0.06f, fill);
        } else if (name.equals("more")) {
            for (int i = 0; i < 3; i++) cv.drawCircle(x(0.22f + i * 0.28f), y(0.5f), 0.075f * u, fill);
        } else if (name.equals("plus")) {
            line(0.5f, 0.22f, 0.5f, 0.78f);
            line(0.22f, 0.5f, 0.78f, 0.5f);
        } else {
            text(fallback, 0.5f, 0.68f, 0.7f);
        }
    }

    @Override public void setAlpha(int alpha) { stroke.setAlpha(alpha); fill.setAlpha(alpha); }
    @Override public void setColorFilter(ColorFilter cf) { stroke.setColorFilter(cf); fill.setColorFilter(cf); }
    @Override public int getOpacity() { return PixelFormat.TRANSLUCENT; }
}
