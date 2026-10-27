package com.dockbuddy.app;

import android.content.Context;
import android.graphics.Canvas;
import android.graphics.Paint;
import android.graphics.RectF;
import android.os.Handler;
import android.os.SystemClock;
import android.view.View;

import java.util.Random;

/**
 * A small cube with a face. The eyes wander, blink, follow the finger, and the face winks when
 * tapped and squints happily when {@link #cheer()} is called.
 */
public class MascotTile extends View {
    private static final long FRAME_MS = 40;

    private final Handler handler = new Handler();
    private final Random random = new Random();
    private final Paint bg = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final Paint white = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final Paint dark = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final Paint line = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final RectF rect = new RectF();
    private final RectF arc = new RectF();
    private final float density;

    private float lookX, lookY, targetX, targetY; // -1..1, where the pupils are looking
    private boolean blinking;
    private long blinkEnd, nextBlink, nextWander, cheerUntil, winkUntil;
    private boolean running;
    private boolean holding; // a finger is down: keep looking at it instead of wandering

    public MascotTile(Context c) {
        super(c);
        density = c.getResources().getDisplayMetrics().density;
        bg.setColor(0xFF26A69A);
        white.setColor(0xFFFFFFFF);
        dark.setColor(0xFF102027);
        line.setColor(0xFF102027);
        line.setStyle(Paint.Style.STROKE);
        line.setStrokeCap(Paint.Cap.ROUND);
        setOnClickListener(new OnClickListener() {
            @Override public void onClick(View v) {
                winkUntil = SystemClock.uptimeMillis() + 500;
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
        nextBlink = now + 2000;
        nextWander = now + 800;
        handler.post(tick);
    }

    public void stop() {
        running = false;
        handler.removeCallbacks(tick);
    }

    /** Happy squint for a moment, e.g. after a button ran successfully. */
    public void cheer() {
        cheerUntil = SystemClock.uptimeMillis() + 900;
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
                if (random.nextInt(10) < 3) {
                    targetX = 0;
                    targetY = 0;
                } else {
                    double angle = random.nextDouble() * Math.PI * 2;
                    float radius = 0.45f + random.nextFloat() * 0.55f;
                    targetX = (float) Math.cos(angle) * radius;
                    targetY = (float) Math.sin(angle) * radius;
                }
                nextWander = now + 1200 + random.nextInt(2600);
            }
            if (!blinking && now >= nextBlink) {
                blinking = true;
                blinkEnd = now + 140;
                nextBlink = now + 2500 + random.nextInt(3500);
            } else if (blinking && now >= blinkEnd) {
                blinking = false;
            }
            lookX += (targetX - lookX) * 0.35f;
            lookY += (targetY - lookY) * 0.35f;
            invalidate();
            handler.postDelayed(this, FRAME_MS);
        }
    };

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
        float cx = rect.centerX(), cy = rect.centerY() - w * 0.07f;
        float eyeR = w * 0.18f, eyeDx = w * 0.22f;
        line.setStrokeWidth(Math.max(1.5f * density, w * 0.06f));
        long now = SystemClock.uptimeMillis();
        boolean cheering = now < cheerUntil, winking = now < winkUntil;

        for (int side = -1; side <= 1; side += 2) {
            float ex = cx + side * eyeDx;
            if (cheering) {
                arc.set(ex - eyeR, cy - eyeR * 0.2f, ex + eyeR, cy + eyeR * 1.6f);
                c.drawArc(arc, 200, 140, false, line);   // happy "^" squint
            } else if (blinking || (winking && side == 1)) {
                c.drawLine(ex - eyeR, cy, ex + eyeR, cy, line);
            } else {
                c.drawCircle(ex, cy, eyeR, white);
                float pr = eyeR * 0.52f;
                float px = ex + lookX * (eyeR - pr) * 0.95f, py = cy + lookY * (eyeR - pr) * 0.95f;
                c.drawCircle(px, py, pr, dark);
                c.drawCircle(px - pr * 0.3f, py - pr * 0.3f, pr * 0.28f, white);
            }
        }

        float mx = cx + lookX * w * 0.04f;
        float mouthW = cheering || winking ? w * 0.22f : w * 0.16f;
        arc.set(mx - mouthW, cy + w * 0.12f, mx + mouthW, cy + w * 0.34f);
        c.drawArc(arc, 20, 140, false, line);
    }
}
