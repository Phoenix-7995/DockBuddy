package com.dockbuddy.app;

import android.app.Activity;
import android.app.AlertDialog;
import android.content.Context;
import android.content.DialogInterface;
import android.content.Intent;
import android.os.Build;
import android.os.Bundle;
import android.os.Handler;
import android.view.Gravity;
import android.view.MotionEvent;
import android.view.View;
import android.view.ViewGroup;
import android.view.WindowManager;
import android.widget.FrameLayout;
import android.widget.LinearLayout;
import android.widget.TextView;
import android.widget.Toast;

import org.json.JSONObject;

import java.util.HashMap;
import java.util.List;
import java.util.Map;

/**
 * Landscape deck made only of cubes on a 7x4 grid. The PC decides where the buttons and the
 * built-in widgets (volume fader, mute, stats, page switch, mascot, menu) sit.
 */
public class DeckActivity extends Activity implements DeckHost {
    private static final int POLL_MS = 2000;

    private final Handler handler = new Handler();
    private Prefs prefs;
    private ApiClient api;
    private Deck deck;
    private int pageIndex;
    private String deckJson = "";
    private final Map<String, Boolean> toggles = new HashMap<String, Boolean>(); // toggle buttons showing their second state
    private String togglesJson = "";
    private boolean polling;
    private String foregroundApp = "";

    private LinearLayout root;
    private FrameLayout screen;
    private DeckBoard board;
    private TextView message;
    private LinearLayout offlineView;   // shown instead of the deck while the PC can't be reached
    private ButtonView wakeTile;
    private TextView offlineText;
    private boolean offline, wakeSent;
    private int failedPolls;
    private Map<String, DeckWidget> widgetViews;
    private int cube, rows;
    private boolean popcornOffered;

    @Override protected void onCreate(Bundle b) {
        super.onCreate(b);
        getWindow().addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON
                | WindowManager.LayoutParams.FLAG_FULLSCREEN);
        prefs = new Prefs(this);
        api = new ApiClient(prefs);

        root = new LinearLayout(this) {
            @Override protected void onSizeChanged(final int w, final int h, int ow, int oh) {
                super.onSizeChanged(w, h, ow, oh);
                // Changing layout params during a layout pass is ignored on old Android, so defer.
                post(new Runnable() {
                    @Override public void run() { layoutTiles(w, h); }
                });
            }
        };
        root.setOrientation(LinearLayout.HORIZONTAL);
        root.setGravity(Gravity.CENTER);
        root.setBackgroundColor(0xFF000000);

        widgetViews = WidgetRegistry.createAll(this);

        board = new DeckBoard(this);
        board.setListener(new DeckBoard.Listener() {
            @Override public void onTap(int slot) {
                Deck.Button btn = deck == null ? null : buttonAt(slot);
                if (btn != null) run(btn, board.viewAt(slot));
            }
        });

        message = new TextView(this);
        message.setTextColor(0xFFFFCDD2);
        message.setTextSize(15);
        message.setGravity(Gravity.CENTER);
        message.setVisibility(View.GONE);
        screen = new FrameLayout(this);
        screen.addView(board, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT));
        screen.addView(message, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.MATCH_PARENT));
        // Widgets sit on top of the board so they win the touches; their cells hold no buttons.
        for (DeckWidget w : widgetViews.values()) {
            w.view().setVisibility(View.GONE);
            screen.addView(w.view(), new FrameLayout.LayoutParams(0, 0));
        }

        buildOfflineView();
        screen.addView(offlineView, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.MATCH_PARENT));

        root.addView(screen);
        setContentView(root);
        loadDeck();
    }

    static Deck.Button tile(String label, String icon, int color) {
        Deck.Button b = new Deck.Button();
        b.label = label;
        b.icon = icon;
        b.color = String.format("#%06X", color & 0xFFFFFF);
        return b;
    }

    /** Sizes every cube so the 7x4 screen fills the display, then puts the widgets in their cells. */
    private void layoutTiles(int w, int h) {
        rows = DeckBoard.ROWS;
        cube = Math.min(h / rows, w / DeckBoard.COLS);
        screen.setLayoutParams(new LinearLayout.LayoutParams(DeckBoard.COLS * cube, rows * cube));
        wakeTile.setLayoutParams(new LinearLayout.LayoutParams(cube, cube));
        board.setCube(cube);
        applyWidgets();
        bindBoard();
    }

    /** The current page's widgets: each page can show them or not and place them differently. */
    /**
     * What the screen shows while the PC can't be reached: a black screen with one Wake PC tile (the same style as
     * the widgets) in the middle and "PC is offline" under it.
     */
    private void buildOfflineView() {
        offlineView = new LinearLayout(this);
        offlineView.setOrientation(LinearLayout.VERTICAL);
        offlineView.setGravity(Gravity.CENTER);
        offlineView.setBackgroundColor(0xFF000000);
        offlineView.setClickable(true); // swallow touches so nothing underneath can be pressed
        offlineView.setVisibility(View.GONE);

        wakeTile = new ButtonView(this);
        wakeTile.bind(tile("Wake PC", "power", 0x37474F));
        wakeTile.setOnClickListener(new View.OnClickListener() {
            @Override public void onClick(View v) { wakeFromOffline(); }
        });
        offlineView.addView(wakeTile, new LinearLayout.LayoutParams(0, 0));

        offlineText = new TextView(this);
        offlineText.setTextColor(0xFF90A4AE);
        offlineText.setTextSize(15);
        offlineText.setGravity(Gravity.CENTER);
        offlineText.setPadding(0, 8, 0, 0);
        offlineView.addView(offlineText, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT));

        TextView change = new TextView(this);
        change.setText("Change PC");
        change.setTextColor(0xFF607D8B);
        change.setTextSize(13);
        change.setPadding(24, 24, 24, 24);
        change.setOnClickListener(new View.OnClickListener() {
            @Override public void onClick(View v) { changePc(); }
        });
        offlineView.addView(change, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT));
        showOfflineText();
    }

    private void showOfflineText() {
        offlineText.setText(wakeSent ? "PC is offline\nWake signal sent, waiting for it to start…" : "PC is offline");
    }

    private void setOffline(boolean value) {
        if (value == offline) return;
        offline = value;
        if (!value) { wakeSent = false; showOfflineText(); }
        offlineView.setVisibility(value ? View.VISIBLE : View.GONE);
        if (value) message.setVisibility(View.GONE);
    }

    private void wakeFromOffline() {
        Prefs.Pc pc = prefs.current();
        if (pc == null || pc.mac.length() == 0) {
            toast("The phone doesn't know this PC's hardware address yet. Connect while the PC is on once, then it can be woken.");
            return;
        }
        WakeOnLan.send(pc.mac, pc.broadcast, new WakeOnLan.Done() {
            @Override public void onDone(String error) {
                if (error != null) { toast("Could not send: " + error); return; }
                wakeSent = true;
                showOfflineText();
            }
        });
    }

    private void changePc() {
        Intent i = new Intent(this, ConnectActivity.class);
        i.putExtra("change", true);
        startActivity(i);
        finish();
    }

    private List<Deck.Widget> widgets() { return deck != null ? page().widgets : Deck.defaultWidgets(); }

    /** Places each widget on its rectangle of cells (or hides it) and tells it about its config. */
    private void applyWidgets() {
        if (cube <= 0) return;
        for (DeckWidget w : widgetViews.values()) w.view().setVisibility(View.GONE); // kinds the PC doesn't list stay hidden
        for (Deck.Widget d : widgets()) {
            DeckWidget w = widgetViews.get(d.kind);
            if (w == null || !d.visible) continue; // unknown kind (newer server) or hidden on the PC
            View v = w.view();
            v.setVisibility(View.VISIBLE);
            FrameLayout.LayoutParams lp = new FrameLayout.LayoutParams(d.w * cube, d.h * cube, Gravity.TOP | Gravity.LEFT);
            lp.leftMargin = d.col * cube;
            lp.topMargin = d.row * cube;
            v.setLayoutParams(lp);
            w.onConfigChanged(d, cube);
        }
        notifyPage();
    }

    private void notifyPage() {
        if (deck == null) return;
        for (DeckWidget w : widgetViews.values()) w.onPage(deck, pageIndex);
    }

    @Override public boolean dispatchTouchEvent(MotionEvent ev) {
        for (DeckWidget w : widgetViews.values()) w.onScreenTouch(ev);
        return super.dispatchTouchEvent(ev);
    }

    @Override public void onWindowFocusChanged(boolean hasFocus) {
        super.onWindowFocusChanged(hasFocus);
        if (hasFocus) hideSystemUi();
    }

    private void hideSystemUi() {
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
        AppUpdater.checkOnce(this, api);
        for (DeckWidget w : widgetViews.values()) w.onStart();
    }

    @Override protected void onPause() {
        super.onPause();
        polling = false;
        handler.removeCallbacks(poll);
        for (DeckWidget w : widgetViews.values()) w.onStop();
    }

    // ---------- live data ----------
    private final Runnable poll = new Runnable() {
        @Override public void run() {
            if (!polling) return;
            api.get("/api/stats", new ApiClient.Callback() {
                @Override public void onResult(JSONObject j, String error) {
                    for (DeckWidget w : widgetViews.values()) w.onStats(j);
                    // Two misses in a row (about 4 s) mean the PC is gone; one answer brings the deck back.
                    if (j != null || (error != null && error.startsWith("Server error"))) { failedPolls = 0; setOffline(false); }
                    else if (++failedPolls >= 2) setOffline(true);
                }
            });
            api.get("/api/nowplaying", new ApiClient.Callback() {
                @Override public void onResult(JSONObject j, String error) {
                    for (DeckWidget w : widgetViews.values()) w.onNowPlaying(j);
                    maybeOfferPopcorn(j);
                }
            });
            if (!anyWidgetBusy()) {
                api.get("/api/volume", new ApiClient.Callback() {
                    @Override public void onResult(JSONObject j, String error) { showVolume(j); }
                });
            }
            api.get("/api/toggles", new ApiClient.Callback() {
                @Override public void onResult(JSONObject j, String error) { if (j != null) applyToggles(j); }
            });
            api.get("/api/foreground", new ApiClient.Callback() {
                @Override public void onResult(JSONObject j, String error) { if (j != null) followForeground(j.optString("app")); }
            });
            api.get("/api/deck", new ApiClient.Callback() {
                @Override public void onResult(JSONObject j, String error) { if (j != null) applyDeck(j); }
            });
            handler.postDelayed(this, POLL_MS);
        }
    };

    /** Offers Popcorn mode once each time a video starts playing fullscreen; asks again only after playback stops. */
    private void maybeOfferPopcorn(JSONObject j) {
        // Only while the video fills the PC screen (fullscreen player, F11, theater-less YouTube fullscreen...).
        boolean video = j != null && j.optBoolean("isVideo") && j.optBoolean("isFullScreen")
                && "playing".equals(j.optString("status"));
        if (!video) { popcornOffered = false; return; }
        if (popcornOffered || !polling) return;
        popcornOffered = true;
        String what = j.optString("title", "");
        String app = j.optString("app", "");
        new AlertDialog.Builder(this).setTitle("Video playing")
                .setMessage((what.length() > 0 ? what : "A video") + (app.length() > 0 ? "\n(" + app + ")" : "")
                        + "\n\nOpen Popcorn mode?")
                .setPositiveButton("Popcorn mode", new DialogInterface.OnClickListener() {
                    @Override public void onClick(DialogInterface d, int w) { startActivity(new Intent(DeckActivity.this, PopcornActivity.class)); }
                }).setNegativeButton("Not now", null).show();
    }

    static String rate(double kbps) {
        if (kbps >= 1024) return String.format("%.1f MB/s", kbps / 1024);
        return String.format("%.0f KB/s", kbps);
    }

    private boolean anyWidgetBusy() {
        for (DeckWidget w : widgetViews.values()) if (w.isBusy()) return true;
        return false;
    }

    private void showVolume(JSONObject j) {
        if (j == null) return;
        int level = j.optInt("level");
        boolean muted = j.optBoolean("muted");
        for (DeckWidget w : widgetViews.values()) w.onVolume(level, muted);
    }

    // ---------- DeckHost: what widgets may ask for ----------
    @Override public Context context() { return this; }

    @Override public ApiClient api() { return api; }

    @Override public void toast(String s) { Toast.makeText(this, s, Toast.LENGTH_LONG).show(); }

    @Override public void openActivity(Class<?> activity) { startActivity(new Intent(this, activity)); }

    @Override public void nextPage() {
        if (deck == null) return;
        pageIndex = (pageIndex + 1) % deck.pages.size();
        refresh();
    }

    @Override public void showMenu() { mainMenu(); }

    @Override public void sendVolume(JSONObject body) {
        api.post("/api/volume", body, new ApiClient.Callback() {
            @Override public void onResult(JSONObject j, String error) {
                if (error != null) toast(error); else showVolume(j);
            }
        });
    }

    // ---------- deck loading / saving ----------
    private void loadDeck() {
        api.get("/api/deck", new ApiClient.Callback() {
            @Override public void onResult(JSONObject j, String error) {
                if (j == null) {
                    if (deck == null) {
                        boolean reachable = error != null && (error.startsWith("Server error") || error.equals("Wrong token"));
                        if (reachable) {
                            message.setText("Could not load the deck\n" + error + "\n\nTap Menu, then Change PC.");
                            message.setVisibility(View.VISIBLE);
                        } else {
                            setOffline(true); // can't reach the PC at all: offer to wake it
                        }
                    }
                    return;
                }
                applyDeck(j);
            }
        });
    }

    /** Shows the page anchored to the program that just came to the front on the PC (pages without an anchor never auto-switch). */
    private void followForeground(String app) {
        if (app == null || app.length() == 0 || app.equals(foregroundApp)) return;
        foregroundApp = app;
        if (deck == null) return;
        for (int i = 0; i < deck.pages.size(); i++) {
            if (app.equalsIgnoreCase(deck.pages.get(i).app)) {
                if (i != pageIndex) { pageIndex = i; refresh(); }
                return;
            }
        }
    }

    /** The toggle buttons that currently show their second state (kept by the PC). */
    private void applyToggles(JSONObject j) {
        String text = j.toString();
        if (text.equals(togglesJson)) return;
        togglesJson = text;
        toggles.clear();
        java.util.Iterator<String> keys = j.keys();
        while (keys.hasNext()) { String k = keys.next(); toggles.put(k, j.optBoolean(k)); }
        bindBoard();
    }

    /** What a button's tile shows: a toggle button shows the face of the action its next tap will run. */
    private Deck.Button face(Deck.Button b) {
        return b.alt != null && Boolean.TRUE.equals(toggles.get(b.id)) ? b.alt : b;
    }

    /** The PC owns the layout: adopt it whenever it changes there. */
    private void applyDeck(JSONObject j) {
        String text = j.toString();
        if (text.equals(deckJson)) return;
        deckJson = text;
        message.setVisibility(View.GONE);
        boolean first = deck == null;
        deck = Deck.fromJson(j);
        pageIndex = Math.min(pageIndex, deck.pages.size() - 1);
        refresh();
        if (first) learnWakeInfo();
    }

    private Deck.Page page() { return deck.pages.get(pageIndex); }

    private void refresh() {
        applyWidgets(); // the new page may arrange its widgets differently
        bindBoard();
    }

    // ---------- board slots ----------
    /** Gives every button of the page a unique cell, placing unplaced ones in the first free cells. */
    private void placeButtons(Deck.Page p) {
        boolean[] used = new boolean[DeckBoard.CELLS];
        for (Deck.Widget d : p.widgets) {
            if (!d.visible) continue;
            for (int i = 0; i < DeckBoard.CELLS; i++) if (d.covers(i)) used[i] = true; // buttons never sit under a widget
        }
        for (Deck.Button b : p.buttons) {
            if (b.slot >= 0 && b.slot < DeckBoard.CELLS && !used[b.slot]) used[b.slot] = true; else b.slot = -1;
        }
        for (Deck.Button b : p.buttons) {
            if (b.slot >= 0) continue;
            for (int i = 0; i < DeckBoard.CELLS; i++) if (!used[i]) { used[i] = true; b.slot = i; break; }
        }
    }

    private Deck.Button buttonAt(int slot) {
        for (Deck.Button b : page().buttons) if (b.slot == slot) return b; // the main button; face() only changes how it looks
        return null;
    }

    private void bindBoard() {
        if (deck == null || board == null) return;
        placeButtons(page());
        Deck.Button[] by = new Deck.Button[DeckBoard.CELLS];
        for (Deck.Button b : page().buttons) if (b.slot >= 0) by[b.slot] = face(b);
        board.setButtons(by);
    }

    // ---------- running buttons ----------
    private void run(final Deck.Button b, final ButtonView view) {
        view.setBusy(true);
        ApiClient.Callback done = new ApiClient.Callback() {
            @Override public void onResult(JSONObject j, String error) {
                view.setBusy(false);
                if (error != null) { toast(error); return; }
                boolean ok = j.optBoolean("ok");
                String out = j.optString("output", "").trim();
                String err = j.optString("error", "");
                Deck.Button shown = face(b); // the face that was tapped, before a toggle flips
                if (j.has("on")) {
                    toggles.put(b.id, j.optBoolean("on"));
                    togglesJson = "";
                    bindBoard();
                }
                for (DeckWidget w : widgetViews.values()) w.onButtonResult(b, ok);
                if (!ok) toast(shown.label + ": " + (err.length() > 0 ? err : "failed"));
                if (out.length() > 0 && (shown.type.equals("command") || shown.type.equals("script") || shown.type.equals("macro"))) showOutput(shown.label, out);
            }
        };
        try {
            if (b.id.length() > 0) {
                api.post("/api/button/" + b.id, new JSONObject(), done); // the PC runs it: macros, and which side of a toggle
            } else {
                JSONObject body = new JSONObject().put("type", b.type).put("value", b.value);
                if (b.args.length() > 0) body.put("args", b.args);
                api.post("/api/action", body, done);
            }
        } catch (Exception e) {
            view.setBusy(false);
            toast(e.getMessage());
        }
    }

    private void showOutput(String title, String text) {
        new AlertDialog.Builder(this).setTitle(title).setMessage(text).setPositiveButton("OK", null).show();
    }

    /** While the PC is reachable, remember its hardware address (it may also have changed since the last time). */
    private void learnWakeInfo() {
        final String host = prefs.host();
        final int port = prefs.port();
        api.get("/api/wake", new ApiClient.Callback() {
            @Override public void onResult(JSONObject j, String error) {
                if (j != null) ConnectActivity.saveWakeInfo(prefs, host, port, j);
            }
        });
    }

    /** Sends a Wake-on-LAN packet to the current PC (its hardware address was saved while it was on). */
    private void wakePc() {
        Prefs.Pc pc = prefs.current();
        if (pc == null || pc.mac.length() == 0) {
            toast("The phone doesn't know this PC's hardware address yet. Open DockBuddy while the PC is on, then try again.");
            return;
        }
        WakeOnLan.send(pc.mac, pc.broadcast, new WakeOnLan.Done() {
            @Override public void onDone(String error) {
                toast(error == null ? "Wake signal sent. The PC can take a minute to start." : "Could not send: " + error);
            }
        });
    }

    private void mainMenu() {
        final String[] items = {"Popcorn mode", "PC monitor", "Wake PC", "Check for update (" + AppUpdater.installedName(this) + ")", "Change PC"};
        new AlertDialog.Builder(this).setItems(items, new DialogInterface.OnClickListener() {
            @Override public void onClick(DialogInterface d, int which) {
                String item = items[which];
                if (item.equals("Popcorn mode")) { startActivity(new Intent(DeckActivity.this, PopcornActivity.class)); return; }
                if (item.equals("PC monitor")) { startActivity(new Intent(DeckActivity.this, MonitorActivity.class)); return; }
                if (item.equals("Wake PC")) { wakePc(); return; }
                if (item.startsWith("Check for update")) { AppUpdater.check(DeckActivity.this, api, true); return; }
                if (item.equals("Change PC")) { changePc(); return; }
            }
        }).show();
    }
}
