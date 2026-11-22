package com.dockbuddy.app;

import android.app.Activity;
import android.graphics.Color;
import android.graphics.Typeface;
import android.os.Build;
import android.os.Bundle;
import android.os.Handler;
import android.os.SystemClock;
import android.text.TextUtils;
import android.view.Gravity;
import android.view.MotionEvent;
import android.view.View;
import android.view.ViewGroup;
import android.view.WindowManager;
import android.widget.LinearLayout;
import android.widget.TextView;
import android.widget.Toast;

import org.json.JSONObject;

/**
 * Popcorn mode: a dark, minimal player remote in the style of a streaming app. Big title on top,
 * big play/pause with 10-second skips in the middle, a scrubbable red progress bar and volume below.
 */
public class PopcornActivity extends Activity {
    private static final int POLL_MS = 1500;
    private static final int RED = 0xFFE50914;
    private static final int MUTED_TEXT = 0xFFB3B3B3;

    private final Handler handler = new Handler();
    private ApiClient api;
    private boolean polling;
    private boolean playing, muted;
    private long optimisticUntil;   // until then a polled play state is ignored (the tap's own result is shown instead)
    private int playTaps;           // numbers the taps so only the latest one may undo itself
    private String appName = "";
    private int duration;

    private LinearLayout root, centerRow;
    private TextView title, subtitle, elapsed, remaining;
    private IconButton closeButton, backButton, playButton, forwardButton, muteButton;
    private IconButton previousButton, nextButton, fullscreenButton;
    private LineSlider progress, volume;
    private CatTile mascot;

    @Override protected void onCreate(Bundle b) {
        super.onCreate(b);
        getWindow().addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON
                | WindowManager.LayoutParams.FLAG_FULLSCREEN);
        api = new ApiClient(new Prefs(this));

        root = new LinearLayout(this) {
            @Override protected void onSizeChanged(final int w, final int h, int ow, int oh) {
                super.onSizeChanged(w, h, ow, oh);
                post(new Runnable() {
                    @Override public void run() { layoutControls(h); }
                });
            }
        };
        root.setOrientation(LinearLayout.VERTICAL);
        root.setBackgroundColor(Color.BLACK);
        int side = dp(28);
        root.setPadding(side, dp(16), side, dp(12));

        root.addView(buildTopBar(), new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT));

        centerRow = new LinearLayout(this);
        centerRow.setOrientation(LinearLayout.HORIZONTAL);
        centerRow.setGravity(Gravity.CENTER);
        backButton = button("back10", null, new Runnable() { @Override public void run() { media("seek:-10"); } });
        playButton = button("play", null, new Runnable() { @Override public void run() { togglePlay(); } });
        forwardButton = button("fwd10", null, new Runnable() { @Override public void run() { media("seek:+10"); } });
        centerRow.addView(backButton);
        centerRow.addView(playButton);
        centerRow.addView(forwardButton);
        LinearLayout.LayoutParams grow = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, 0);
        grow.weight = 1;
        root.addView(centerRow, grow);

        root.addView(buildProgressRow(), new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT));
        root.addView(buildBottomRow(), new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT));
        setContentView(root);
        showNothing("Connecting…", "");
    }

    private int dp(int v) { return Ui.dp(this, v); }

    private IconButton button(String icon, String label, final Runnable onClick) {
        IconButton b = new IconButton(this, icon, label);
        b.setOnClickListener(new View.OnClickListener() {
            @Override public void onClick(View v) { onClick.run(); }
        });
        return b;
    }

    private View buildTopBar() {
        LinearLayout bar = new LinearLayout(this);
        bar.setOrientation(LinearLayout.HORIZONTAL);
        bar.setGravity(Gravity.CENTER_VERTICAL);

        closeButton = button("close", null, new Runnable() { @Override public void run() { finish(); } });
        bar.addView(closeButton, new LinearLayout.LayoutParams(dp(44), dp(44)));

        LinearLayout texts = new LinearLayout(this);
        texts.setOrientation(LinearLayout.VERTICAL);
        texts.setPadding(dp(16), 0, dp(16), 0);
        title = new TextView(this);
        title.setTextColor(Color.WHITE);
        title.setTextSize(22);
        title.setTypeface(Typeface.DEFAULT_BOLD);
        title.setSingleLine(true);
        title.setEllipsize(TextUtils.TruncateAt.END);
        subtitle = new TextView(this);
        subtitle.setTextColor(MUTED_TEXT);
        subtitle.setTextSize(14);
        subtitle.setSingleLine(true);
        subtitle.setEllipsize(TextUtils.TruncateAt.END);
        texts.addView(title);
        texts.addView(subtitle);
        LinearLayout.LayoutParams tp = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT);
        tp.weight = 1;
        bar.addView(texts, tp);

        mascot = new CatTile(this, CatStyle.GINNY);
        bar.addView(mascot, new LinearLayout.LayoutParams(dp(44), dp(44)));
        return bar;
    }

    private View buildProgressRow() {
        LinearLayout row = new LinearLayout(this);
        row.setOrientation(LinearLayout.HORIZONTAL);
        row.setGravity(Gravity.CENTER_VERTICAL);

        elapsed = timeText();
        remaining = timeText();
        progress = new LineSlider(this);
        progress.setFillColor(RED);
        progress.setListener(new LineSlider.Listener() {
            @Override public void onPreview(int value) {
                elapsed.setText(clock(value));
                remaining.setText("-" + clock(duration - value));
            }
            @Override public void onCommit(int value) { media("seekto:" + value); }
        });
        row.addView(elapsed, new LinearLayout.LayoutParams(dp(60), ViewGroup.LayoutParams.WRAP_CONTENT));
        LinearLayout.LayoutParams sp = new LinearLayout.LayoutParams(0, dp(40));
        sp.weight = 1;
        row.addView(progress, sp);
        row.addView(remaining, new LinearLayout.LayoutParams(dp(68), ViewGroup.LayoutParams.WRAP_CONTENT));
        return row;
    }

    private TextView timeText() {
        TextView t = new TextView(this);
        t.setTextColor(Color.WHITE);
        t.setTextSize(13);
        t.setGravity(Gravity.CENTER);
        return t;
    }

    private View buildBottomRow() {
        LinearLayout row = new LinearLayout(this);
        row.setOrientation(LinearLayout.HORIZONTAL);
        row.setGravity(Gravity.CENTER_VERTICAL);

        muteButton = button("volume_up", null, new Runnable() { @Override public void run() { volumeCall("{\"mute\":\"toggle\"}"); } });
        row.addView(muteButton, new LinearLayout.LayoutParams(dp(40), dp(40)));
        volume = new LineSlider(this);
        volume.setListener(new LineSlider.Listener() {
            @Override public void onPreview(int value) {}
            @Override public void onCommit(int value) { volumeCall("{\"level\":" + value + "}"); }
        });
        row.addView(volume, new LinearLayout.LayoutParams(dp(180), dp(40)));

        View spacer = new View(this);
        LinearLayout.LayoutParams gp = new LinearLayout.LayoutParams(0, 1);
        gp.weight = 1;
        row.addView(spacer, gp);

        previousButton = button("prev", "Previous", new Runnable() { @Override public void run() { media("prev"); } });
        nextButton = button("next", "Next", new Runnable() { @Override public void run() { media("next"); } });
        fullscreenButton = button("fullscreen", "Fullscreen", new Runnable() { @Override public void run() { action("keys", "f"); } });
        for (IconButton b : new IconButton[]{previousButton, nextButton, fullscreenButton}) {
            row.addView(b, new LinearLayout.LayoutParams(dp(76), dp(52)));
        }
        return row;
    }

    /** Scales the big controls with the screen height. */
    private void layoutControls(int h) {
        float d = getResources().getDisplayMetrics().density;
        float s = Math.max(0.75f, Math.min(1.6f, h / (360 * d)));
        int seek = (int) (dp(64) * s), play = (int) (dp(112) * s), gap = (int) (dp(40) * s);
        backButton.setLayoutParams(controlParams(seek, gap));
        forwardButton.setLayoutParams(controlParams(seek, gap));
        playButton.setLayoutParams(controlParams(play, gap));
        centerRow.requestLayout();
    }

    private LinearLayout.LayoutParams controlParams(int size, int gap) {
        LinearLayout.LayoutParams p = new LinearLayout.LayoutParams(size, size);
        p.setMargins(gap, 0, gap, 0);
        return p;
    }

    @Override public boolean dispatchTouchEvent(MotionEvent ev) {
        int action = ev.getAction();
        if (action == MotionEvent.ACTION_DOWN || action == MotionEvent.ACTION_MOVE) {
            mascot.lookAt(ev.getRawX(), ev.getRawY());
        } else if (action == MotionEvent.ACTION_UP || action == MotionEvent.ACTION_CANCEL) {
            mascot.release();
        }
        return super.dispatchTouchEvent(ev);
    }

    @Override public void onWindowFocusChanged(boolean hasFocus) {
        super.onWindowFocusChanged(hasFocus);
        if (!hasFocus) return;
        int flags = View.SYSTEM_UI_FLAG_HIDE_NAVIGATION;
        if (Build.VERSION.SDK_INT >= 19) {
            flags |= View.SYSTEM_UI_FLAG_IMMERSIVE_STICKY | View.SYSTEM_UI_FLAG_FULLSCREEN;
        }
        root.setSystemUiVisibility(flags);
    }

    @Override protected void onResume() {
        super.onResume();
        polling = true;
        handler.post(poll);
        mascot.start();
    }

    @Override protected void onPause() {
        super.onPause();
        polling = false;
        handler.removeCallbacks(poll);
        mascot.stop();
    }

    // ---------- live state ----------
    private final Runnable poll = new Runnable() {
        @Override public void run() {
            if (!polling) return;
            refresh();
            handler.postDelayed(this, POLL_MS);
        }
    };

    private void refresh() {
        api.get("/api/nowplaying", new ApiClient.Callback() {
            @Override public void onResult(JSONObject j, String error) { showNowPlaying(j); }
        });
        if (!volume.isDragging()) {
            api.get("/api/volume", new ApiClient.Callback() {
                @Override public void onResult(JSONObject j, String error) { showVolume(j); }
            });
        }
    }

    private void showNothing(String line1, String line2) {
        title.setText(line1);
        subtitle.setText(line2);
        duration = 0;
        progress.setSeekable(false);
        elapsed.setText("");
        remaining.setText("");
        playing = false;
        optimisticUntil = 0;
        playButton.setIcon("play");
    }

    private void showNowPlaying(JSONObject j) {
        if (j == null) { showNothing("Can't reach the PC", "Check the connection"); return; }
        String status = j.optString("status", "none");
        if (status.equals("none")) { showNothing("Nothing playing", "Start a video on your PC"); return; }

        boolean polledPlaying = status.equals("playing");
        if (SystemClock.uptimeMillis() < optimisticUntil && polledPlaying != playing) {
            // The PC hasn't caught up with the tap yet: keep what the tap showed instead of flickering back.
        } else {
            optimisticUntil = 0;
            playing = polledPlaying;
        }
        String name = j.optString("title", "");
        title.setText(name.length() > 0 ? name : "(no title)");
        appName = j.optString("app", "");
        renderPlayState();

        duration = j.optInt("durationSeconds");
        boolean seekable = duration > 0 && j.optBoolean("canSeek");
        progress.setSeekable(seekable);
        if (duration > 0) {
            progress.setMax(duration);
            if (!progress.isDragging()) {
                int pos = j.optInt("positionSeconds");
                progress.setValue(pos);
                elapsed.setText(clock(pos));
                remaining.setText("-" + clock(duration - pos));
            }
        } else {
            elapsed.setText("");
            remaining.setText("");
        }
    }

    /** The play/pause icon and the "Paused" note under the title, from the current state. */
    private void renderPlayState() {
        playButton.setIcon(playing ? "pause" : "play");
        subtitle.setText(appName.length() > 0 ? appName + (playing ? "" : "  -  Paused") : (playing ? "" : "Paused"));
    }

    /**
     * Flips the icon right away, then tells the PC. If the PC refuses or can't be reached the icon goes back to
     * how it was (unless a newer tap has changed it since).
     */
    private void togglePlay() {
        final boolean before = playing;
        final int tap = ++playTaps;
        playing = !before;
        optimisticUntil = SystemClock.uptimeMillis() + 2500;
        renderPlayState();
        try {
            api.post("/api/action", new JSONObject().put("type", "media").put("value", "playpause"), new ApiClient.Callback() {
                @Override public void onResult(JSONObject j, String error) {
                    boolean ok = error == null && j != null && j.optBoolean("ok");
                    if (ok) { mascot.cheer(); return; }
                    toast(error != null ? error : j.optString("error", "failed"));
                    if (tap == playTaps) { // nothing newer happened: undo
                        playing = before;
                        optimisticUntil = 0;
                        renderPlayState();
                    }
                }
            });
        } catch (Exception e) {
            playing = before;
            optimisticUntil = 0;
            renderPlayState();
            toast(e.getMessage());
        }
    }

    private void showVolume(JSONObject j) {
        if (j == null || volume.isDragging()) return;
        volume.setValue(j.optInt("level"));
        muted = j.optBoolean("muted");
        muteButton.setIcon(muted ? "mute" : "volume_up");
    }

    private static String clock(int s) {
        s = Math.max(0, s);
        return s >= 3600 ? String.format("%d:%02d:%02d", s / 3600, (s % 3600) / 60, s % 60)
                : String.format("%d:%02d", s / 60, s % 60);
    }

    // ---------- commands ----------
    private void media(String command) { action("media", command); }

    private void action(String type, String value) {
        try {
            api.post("/api/action", new JSONObject().put("type", type).put("value", value), new ApiClient.Callback() {
                @Override public void onResult(JSONObject j, String error) {
                    if (error != null) { toast(error); return; }
                    if (!j.optBoolean("ok")) toast(j.optString("error", "failed"));
                    else mascot.cheer();
                    refresh();
                }
            });
        } catch (Exception e) {
            toast(e.getMessage());
        }
    }

    private void volumeCall(String body) {
        try {
            api.post("/api/volume", new JSONObject(body), new ApiClient.Callback() {
                @Override public void onResult(JSONObject j, String error) {
                    if (error != null) toast(error); else showVolume(j);
                }
            });
        } catch (Exception e) {
            toast(e.getMessage());
        }
    }

    private void toast(String s) { Toast.makeText(this, s, Toast.LENGTH_LONG).show(); }
}
