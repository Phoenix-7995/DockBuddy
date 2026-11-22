package com.dockbuddy.app;

import android.content.Context;
import android.graphics.Canvas;
import android.graphics.Paint;
import android.graphics.Path;
import android.graphics.RectF;
import android.os.Handler;
import android.os.SystemClock;
import android.view.View;

import java.util.Random;

/**
 * An animated cat face in a rounded cube. The eyes wander and follow the finger, the cat blinks on its
 * own rhythm, winks when tapped and cheers when a deck button succeeds. The look comes from a {@link CatStyle}.
 */
public class CatTile extends View {
    private static final long FRAME_MS = 40;

    private final CatStyle style;
    private final Handler handler = new Handler();
    private final Random random = new Random();
    private final Paint bg = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final Paint fill = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final Paint dark = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final Paint line = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final Paint whisker = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final RectF rect = new RectF();
    private final RectF box = new RectF();
    private final Path path = new Path();
    private final float density;

    private float lookX, lookY, targetX, targetY; // -1..1, where the pupils are looking
    private boolean blinking;
    private long blinkEnd, nextBlink, nextWander, cheerUntil, winkUntil;
    private boolean running;
    private boolean holding; // a finger is down: keep looking at it instead of wandering

    public CatTile(Context c, CatStyle style) {
        super(c);
        this.style = style;
        density = c.getResources().getDisplayMetrics().density;
        bg.setColor(style.defaultBg);
        fill.setColor(style.fur);
        dark.setColor(0xFF102027);
        line.setColor(0xFF102027);
        line.setStyle(Paint.Style.STROKE);
        line.setStrokeCap(Paint.Cap.ROUND);
        whisker.setColor(0xCCFFFFFF);
        whisker.setStyle(Paint.Style.STROKE);
        whisker.setStrokeCap(Paint.Cap.ROUND);
        setOnClickListener(new OnClickListener() {
            @Override public void onClick(View v) {
                winkUntil = SystemClock.uptimeMillis() + 450;
                invalidate();
            }
        });
    }

    public void setColor(int color) {
        bg.setColor(color);
        invalidate();
    }

    public void start() {
        if (running) return;
        running = true;
        long now = SystemClock.uptimeMillis();
        nextBlink = now + style.blinkMin;
        nextWander = now + style.wanderMin;
        handler.post(tick);
    }

    public void stop() {
        running = false;
        handler.removeCallbacks(tick);
    }

    /** A happy reaction, e.g. after a button ran successfully. */
    public void cheer() {
        cheerUntil = SystemClock.uptimeMillis() + 1000;
        invalidate();
    }

    /** Looks toward a point in screen coordinates and pauses wandering for a while. */
    public void lookAt(float rawX, float rawY) {
        int[] loc = new int[2];
        getLocationOnScreen(loc);
        float dx = rawX - (loc[0] + getWidth() / 2f), dy = rawY - (loc[1] + getHeight() / 2f);
        float len = (float) Math.sqrt(dx * dx + dy * dy);
        if (len < 1f) {
            targetX = 0;
            targetY = 0;
        } else {
            float strength = Math.min(1f, len / (getWidth() * 1.5f));
            targetX = dx / len * strength;
            targetY = dy / len * strength;
        }
        holding = true;
    }

    /** The finger lifted: keep the last gaze for a moment, then resume wandering. */
    public void release() {
        holding = false;
        nextWander = SystemClock.uptimeMillis() + 2500;
    }

    private final Runnable tick = new Runnable() {
        @Override public void run() {
            if (!running) return;
            long now = SystemClock.uptimeMillis();
            if (!holding && now >= nextWander) {
                pickTarget();
                nextWander = now + style.wanderMin + random.nextInt((int) style.wanderSpan);
            }
            if (!blinking && now >= nextBlink) {
                blinking = true;
                blinkEnd = now + 140;
                nextBlink = now + style.blinkMin + random.nextInt((int) style.blinkSpan);
            } else if (blinking && now >= blinkEnd) {
                blinking = false;
            }
            lookX += (targetX - lookX) * 0.35f;
            lookY += (targetY - lookY) * 0.35f;
            invalidate();
            handler.postDelayed(this, FRAME_MS);
        }
    };

    private void pickTarget() {
        if (style.sideGlance) {
            // Caspar glances sideways, mostly without moving his head much.
            targetX = (random.nextBoolean() ? 1 : -1) * (0.6f + random.nextFloat() * 0.4f);
            targetY = (random.nextFloat() - 0.5f) * 0.4f;
        } else if (random.nextInt(10) < 3) {
            targetX = 0;
            targetY = 0;
        } else {
            double angle = random.nextDouble() * Math.PI * 2;
            float radius = 0.45f + random.nextFloat() * 0.55f;
            targetX = (float) Math.cos(angle) * radius;
            targetY = (float) Math.sin(angle) * radius;
        }
    }

    @Override protected void onAttachedToWindow() {
        super.onAttachedToWindow();
        start();
    }

    @Override protected void onDetachedFromWindow() {
        stop();
        super.onDetachedFromWindow();
    }

    @Override protected void onDraw(Canvas c) {
        float pad = Tile.pad(density), r = Tile.radius(density);
        rect.set(pad, pad, getWidth() - pad, getHeight() - pad);
        c.drawRoundRect(rect, r, r, bg);

        float w = Math.min(rect.width(), rect.height());
        float cx = rect.centerX(), cy = rect.centerY() + w * 0.05f;
        long now = SystemClock.uptimeMillis();
        boolean cheering = now < cheerUntil, winking = now < winkUntil;
        boolean tilted = style.tilt && cheering;

        if (tilted) {
            c.save();
            c.rotate(-7f, cx, cy);
        }

        // Ears, then the head on top of them.
        drawEar(c, cx, cy, w, -1);
        drawEar(c, cx, cy, w, 1);
        fill.setColor(style.fur);
        box.set(cx - 0.36f * w, cy - 0.30f * w, cx + 0.36f * w, cy + 0.30f * w);
        c.drawOval(box, fill);

        if (style.hasPatch) {
            fill.setColor(style.patch);
            box.set(cx - 0.22f * w, cy - 0.27f * w, cx + 0.22f * w, cy - 0.10f * w);
            c.drawOval(box, fill);
        }
        if (style.hasMuzzle) {
            fill.setColor(style.muzzle);
            box.set(cx - 0.15f * w, cy + 0.08f * w, cx + 0.15f * w, cy + 0.22f * w);
            c.drawOval(box, fill);
        }
        if (style.hasBlush) {
            fill.setColor(style.blush);
            float glow = cheering ? 0.09f : 0.075f;
            c.drawCircle(cx - 0.25f * w, cy + 0.07f * w, glow * w, fill);
            c.drawCircle(cx + 0.25f * w, cy + 0.07f * w, glow * w, fill);
        }

        drawEyes(c, cx, cy, w, cheering, winking);
        drawNoseAndMouth(c, cx, cy, w, cheering, winking);
        drawWhiskers(c, cx, cy, w);

        if (tilted) c.restore();
    }

    private void drawEar(Canvas c, float cx, float cy, float w, int side) {
        fill.setColor(style.fur);
        path.reset();
        path.moveTo(cx + side * 0.30f * w, cy - 0.04f * w);
        path.lineTo(cx + side * 0.32f * w, cy - 0.42f * w);
        path.lineTo(cx + side * 0.04f * w, cy - 0.22f * w);
        path.close();
        c.drawPath(path, fill);

        fill.setColor(style.inner);
        path.reset();
        path.moveTo(cx + side * 0.27f * w, cy - 0.08f * w);
        path.lineTo(cx + side * 0.30f * w, cy - 0.34f * w);
        path.lineTo(cx + side * 0.10f * w, cy - 0.21f * w);
        path.close();
        c.drawPath(path, fill);
    }

    private void drawEyes(Canvas c, float cx, float cy, float w, boolean cheering, boolean winking) {
        float eyeR = style.eyeR * w, eyeY = cy - 0.02f * w;
        line.setStrokeWidth(Math.max(1.5f * density, w * 0.05f));

        for (int side = -1; side <= 1; side += 2) {
            float ex = cx + side * style.eyeDx * w;
            boolean rightWink = side == 1 && winking;

            if (cheering && style.cheerMode == CatStyle.CHEER_SQUINT) {
                box.set(ex - eyeR, eyeY - eyeR * 0.2f, ex + eyeR, eyeY + eyeR * 1.6f);
                c.drawArc(box, 200, 140, false, line);
            } else if (cheering && style.cheerMode == CatStyle.CHEER_SOFT) {
                box.set(ex - eyeR, eyeY - eyeR * 0.6f, ex + eyeR, eyeY + eyeR * 1.0f);
                c.drawArc(box, 20, 140, false, line);
            } else if (blinking || rightWink || (cheering && style.cheerMode == CatStyle.CHEER_WINK && side == 1)) {
                c.drawLine(ex - eyeR, eyeY, ex + eyeR, eyeY, line);
            } else {
                fill.setColor(style.eye);
                c.drawCircle(ex, eyeY, eyeR, fill);

                float pr = eyeR * 0.45f;
                float px = ex + lookX * (eyeR - pr) * 0.8f, py = eyeY + lookY * (eyeR - pr) * 0.8f;
                box.set(px - pr * 0.55f, py - pr, px + pr * 0.55f, py + pr);
                c.drawOval(box, dark);
                fill.setColor(0xFFFFFFFF);
                c.drawCircle(px - pr * 0.3f, py - pr * 0.4f, pr * 0.28f, fill);

                if (style.lid > 0) {
                    // Heavy lid: fur covers the top part of the eye, with a dark lid line.
                    fill.setColor(style.fur);
                    float lidY = eyeY - eyeR + 2 * eyeR * style.lid;
                    box.set(ex - eyeR - 2, eyeY - eyeR - 2, ex + eyeR + 2, lidY);
                    c.drawRect(box, fill);
                    c.drawLine(ex - eyeR, lidY, ex + eyeR, lidY, line);
                }
            }
        }
    }

    private void drawNoseAndMouth(Canvas c, float cx, float cy, float w, boolean cheering, boolean winking) {
        // Small pink nose.
        fill.setColor(style.inner);
        path.reset();
        path.moveTo(cx - 0.035f * w, cy + 0.08f * w);
        path.lineTo(cx + 0.035f * w, cy + 0.08f * w);
        path.lineTo(cx, cy + 0.13f * w);
        path.close();
        c.drawPath(path, fill);

        if (style.smirk && !cheering) {
            // Caspar's one-sided smirk.
            box.set(cx - 0.02f * w, cy + 0.13f * w, cx + 0.12f * w, cy + 0.23f * w);
            c.drawArc(box, 10, 100, false, line);
            return;
        }
        float mouthW = (cheering || winking) ? 0.10f * w : 0.07f * w;
        box.set(cx - mouthW, cy + 0.12f * w, cx, cy + 0.12f * w + mouthW * 1.2f);
        c.drawArc(box, 0, 180, false, line);
        box.set(cx, cy + 0.12f * w, cx + mouthW, cy + 0.12f * w + mouthW * 1.2f);
        c.drawArc(box, 0, 180, false, line);
    }

    private void drawWhiskers(Canvas c, float cx, float cy, float w) {
        whisker.setStrokeWidth(Math.max(1f * density, w * 0.012f));
        for (int side = -1; side <= 1; side += 2) {
            float x0 = cx + side * 0.20f * w, y0 = cy + 0.10f * w;
            c.drawLine(x0, y0, cx + side * 0.46f * w, cy + 0.04f * w, whisker);
            c.drawLine(x0, y0 + 0.03f * w, cx + side * 0.46f * w, cy + 0.12f * w, whisker);
        }
    }
}
