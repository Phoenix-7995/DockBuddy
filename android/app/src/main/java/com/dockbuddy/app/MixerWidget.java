package com.dockbuddy.app;

import android.content.Context;
import android.graphics.Canvas;
import android.graphics.Paint;
import android.graphics.RectF;
import android.os.Handler;
import android.os.SystemClock;
import android.view.MotionEvent;
import android.view.View;
import android.view.ViewConfiguration;

import org.json.JSONArray;
import org.json.JSONObject;

import java.util.ArrayList;
import java.util.List;

/**
 * Per-application volume, like the Windows mixer: one fader per program that has audio. Drag a fader to
 * set that program's volume, tap it to mute or unmute it. Faders stand side by side when the widget is
 * wider than tall, and lie in rows when it is taller.
 */
class MixerWidget extends DeckWidget {
    private static final long POLL_MS = 2000, SEND_EVERY_MS = 150;

    private static class App {
        String name;
        int level;
        boolean muted;
    }

    private final Handler handler = new Handler();
    private final Strip strip;
    private final List<App> apps = new ArrayList<App>();
    private boolean running;
    private int accent = Tile.ACCENT;
    private String status = "Connecting…";

    MixerWidget(DeckHost host) {
        super(host);
        strip = new Strip(host.context());
    }

    @Override public View view() { return strip; }

    @Override public void onConfigChanged(Deck.Widget config, int cellPx) {
        super.onConfigChanged(config, cellPx);
        accent = color(0x1E88E5);
        strip.invalidate();
    }

    @Override public boolean isBusy() { return strip.dragging >= 0; }

    @Override public void onStart() {
        running = true;
        handler.post(poll);
    }

    @Override public void onStop() {
        running = false;
        handler.removeCallbacks(poll);
    }

    private final Runnable poll = new Runnable() {
        @Override public void run() {
            if (!running) return;
            if (!isBusy()) {
                host.api().get("/api/mixer", new ApiClient.Callback() {
                    @Override public void onResult(JSONObject j, String error) {
                        if (isBusy()) return;
                        if (error != null) {
                            // A reply with an error means the PC is there but the mixer failed; no reply means it is unreachable.
                            status = error.startsWith("Server error") ? "Mixer problem on the PC\n" + error.replace("Server error ", "") : "PC offline";
                            apps.clear();
                            strip.invalidate();
                            return;
                        }
                        JSONArray arr = j == null ? null : j.optJSONArray("items");
                        read(arr);
                    }
                });
            }
            handler.postDelayed(this, POLL_MS);
        }
    };

    /** Replaces the list with the server's apps (a JSON array, delivered by ApiClient as {"items":[...]}). */
    private void read(JSONArray arr) {
        apps.clear();
        for (int i = 0; arr != null && i < arr.length(); i++) {
            JSONObject o = arr.optJSONObject(i);
            if (o == null) continue;
            App a = new App();
            a.name = o.optString("name");
            a.level = o.optInt("volume");
            a.muted = o.optBoolean("muted");
            apps.add(a);
        }
        status = "No app is playing audio";
        strip.invalidate();
    }

    private void send(App a, Integer level, Boolean mute) {
        try {
            JSONObject body = new JSONObject().put("name", a.name);
            if (level != null) body.put("level", level.intValue());
            if (mute != null) body.put("mute", mute.booleanValue());
            host.api().post("/api/mixer", body, new ApiClient.Callback() {
                @Override public void onResult(JSONObject j, String error) {
                    if (error != null) { host.toast(error); return; }
                    if (!isBusy() && j != null) read(j.optJSONArray("items"));
                }
            });
        } catch (Exception e) {
            host.toast(String.valueOf(e.getMessage()));
        }
    }

    private class Strip extends View {
        private final Paint bg = new Paint(Paint.ANTI_ALIAS_FLAG);
        private final Paint track = new Paint(Paint.ANTI_ALIAS_FLAG);
        private final Paint fill = new Paint(Paint.ANTI_ALIAS_FLAG);
        private final Paint name = new Paint(Paint.ANTI_ALIAS_FLAG);
        private final Paint value = new Paint(Paint.ANTI_ALIAS_FLAG);
        private final RectF rect = new RectF();
        private final RectF bar = new RectF();
        private final RectF[] tracks = new RectF[16];
        private final IconDrawable muteIcon = new IconDrawable("mute", "");
        private final float density;
        private final int slop;
        private int count;           // faders currently shown
        private int dragging = -1;   // fader being dragged
        private int down = -1;
        private float downX, downY;
        private long lastSend;

        Strip(Context c) {
            super(c);
            density = c.getResources().getDisplayMetrics().density;
            slop = ViewConfiguration.get(c).getScaledTouchSlop();
            bg.setColor(Tile.BG);
            track.setColor(Tile.TRACK);
            name.setColor(Tile.TEXT);
            name.setTextAlign(Paint.Align.CENTER);
            value.setColor(Tile.TEXT);
            value.setFakeBoldText(true);
            for (int i = 0; i < tracks.length; i++) tracks[i] = new RectF();
        }

        private boolean columns() { return getWidth() >= getHeight(); }

        /** Works out where each fader track goes (also used to hit-test touches). */
        private void layoutTracks() {
            float pad = Tile.pad(density);
            rect.set(pad, pad, getWidth() - pad, getHeight() - pad);
            float inner = 8 * density;
            float left = rect.left + inner, top = rect.top + inner, right = rect.right - inner, bottom = rect.bottom - inner;
            int total = Math.min(apps.size(), tracks.length);
            if (columns()) {
                float label = 20 * density, minCol = 56 * density, maxCol = 110 * density;
                int cap = Math.max(1, (int) ((right - left) / minCol));
                count = Math.min(total, cap);
                float colW = count == 0 ? 0 : Math.min((right - left) / count, maxCol);
                float x0 = left + ((right - left) - colW * count) / 2f;
                for (int i = 0; i < count; i++) {
                    float x = x0 + i * colW;
                    tracks[i].set(x + 4 * density, top, x + colW - 4 * density, bottom - label);
                }
            } else {
                float rowH = 44 * density;
                int cap = Math.max(1, (int) ((bottom - top) / rowH));
                count = Math.min(total, cap);
                float h = count == 0 ? 0 : Math.min((bottom - top) / count, rowH);
                for (int i = 0; i < count; i++) {
                    float y = top + i * h;
                    tracks[i].set(left, y + 3 * density, right, y + h - 3 * density);
                }
            }
        }

        @Override protected void onDraw(Canvas c) {
            layoutTracks();
            float r = Tile.radius(density);
            c.drawRoundRect(rect, r, r, bg);
            if (count == 0) {
                name.setTextSize(13 * density);
                name.setColor(Tile.MUTED);
                String[] lines = status.split("\n");
                float y = rect.centerY() - (lines.length - 1) * 9 * density;
                for (String line : lines) {
                    c.drawText(fitText(name, line, rect.width() - 16 * density), rect.centerX(), y, name);
                    y += 18 * density;
                }
                name.setColor(Tile.TEXT);
                return;
            }
            boolean cols = columns();
            float fr = 6 * density;
            for (int i = 0; i < count; i++) {
                App a = apps.get(i);
                RectF t = tracks[i];
                c.drawRoundRect(t, fr, fr, track);
                float frac = a.level / 100f;
                if (frac > 0) {
                    if (cols) bar.set(t.left, t.bottom - t.height() * frac, t.right, t.bottom);
                    else bar.set(t.left, t.top, t.left + t.width() * frac, t.bottom);
                    fill.setColor(a.muted ? 0xFF546E7A : accent);
                    c.drawRoundRect(bar, fr, fr, fill);
                }
                String label = pretty(a.name);
                if (cols) {
                    name.setTextSize(11 * density);
                    c.drawText(fitText(name, label, t.width() + 6 * density), t.centerX(), t.bottom + 15 * density, name);
                    drawLevel(c, a, t.centerX(), t.top + 8 * density, Paint.Align.CENTER, t.width());
                } else {
                    name.setTextSize(13 * density);
                    name.setTextAlign(Paint.Align.LEFT);
                    c.drawText(fitText(name, label, t.width() * 0.6f), t.left + 10 * density, t.centerY() + name.getTextSize() * 0.35f, name);
                    name.setTextAlign(Paint.Align.CENTER);
                    drawLevel(c, a, t.right - 10 * density, t.centerY() - 9 * density, Paint.Align.RIGHT, t.width());
                }
            }
        }

        /** The percentage, or a speaker-off icon while muted. */
        private void drawLevel(Canvas c, App a, float x, float top, Paint.Align align, float room) {
            if (a.muted) {
                int s = (int) (18 * density);
                int l = align == Paint.Align.RIGHT ? (int) (x - s) : (int) (x - s / 2f);
                muteIcon.setBounds(l, (int) top, l + s, (int) top + s);
                muteIcon.draw(c);
                return;
            }
            value.setTextAlign(align);
            value.setTextSize(Math.min(14 * density, room * 0.3f));
            c.drawText(a.level + "%", x, top + value.getTextSize() * 0.9f, value);
        }

        private String pretty(String s) {
            return s.length() == 0 ? "?" : Character.toUpperCase(s.charAt(0)) + s.substring(1);
        }

        private String fitText(Paint p, String s, float maxWidth) {
            if (p.measureText(s) <= maxWidth) return s;
            String t = s;
            while (t.length() > 1 && p.measureText(t + "…") > maxWidth) t = t.substring(0, t.length() - 1);
            return t + "…";
        }

        private int indexAt(float x, float y) {
            for (int i = 0; i < count; i++) {
                RectF t = tracks[i];
                if (columns() ? (x >= t.left - 4 * density && x <= t.right + 4 * density && y >= t.top && y <= t.bottom + 20 * density)
                        : t.contains(x, y)) return i;
            }
            return -1;
        }

        private int levelAt(int i, float x, float y) {
            RectF t = tracks[i];
            float frac = columns() ? (t.bottom - y) / t.height() : (x - t.left) / t.width();
            return Math.max(0, Math.min(100, Math.round(100f * frac)));
        }

        @Override public boolean onTouchEvent(MotionEvent e) {
            layoutTracks();
            switch (e.getAction()) {
                case MotionEvent.ACTION_DOWN:
                    down = indexAt(e.getX(), e.getY());
                    downX = e.getX();
                    downY = e.getY();
                    dragging = -1;
                    return down >= 0;
                case MotionEvent.ACTION_MOVE:
                    if (down < 0 || down >= count) return true;
                    if (dragging < 0 && (Math.abs(e.getX() - downX) > slop || Math.abs(e.getY() - downY) > slop)) {
                        dragging = down;
                        if (getParent() != null) getParent().requestDisallowInterceptTouchEvent(true);
                    }
                    if (dragging >= 0) {
                        App a = apps.get(dragging);
                        a.level = levelAt(dragging, e.getX(), e.getY());
                        a.muted = false;
                        invalidate();
                        long now = SystemClock.uptimeMillis();
                        if (now - lastSend >= SEND_EVERY_MS) { lastSend = now; send(a, a.level, null); }
                    }
                    return true;
                case MotionEvent.ACTION_UP:
                    if (dragging >= 0 && dragging < count) {
                        App a = apps.get(dragging);
                        a.level = levelAt(dragging, e.getX(), e.getY());
                        dragging = -1;
                        send(a, a.level, null);
                    } else if (down >= 0 && down < count) {
                        App a = apps.get(down);
                        a.muted = !a.muted;
                        send(a, null, a.muted);
                        invalidate();
                    }
                    down = -1;
                    dragging = -1;
                    return true;
                case MotionEvent.ACTION_CANCEL:
                    down = -1;
                    dragging = -1;
                    return true;
            }
            return false;
        }
    }
}
