package com.dockbuddy.app;

import org.json.JSONArray;
import org.json.JSONException;
import org.json.JSONObject;

import java.util.ArrayList;
import java.util.List;

/** In-memory copy of the deck stored on the PC. Round-trips through the server's JSON shape. */
public class Deck {

    public static class Button {
        public int slot = -1; // cell on the 7x4 screen grid, -1 = not placed yet
        public Button alt; // second state of a toggle button, else null
        public String id = "", label = "", icon = "", color = "#37474F", type = "command", value = "", args = "";

        static Button fromJson(JSONObject j) {
            Button b = new Button();
            b.id = j.optString("id");
            b.label = j.optString("label");
            b.icon = j.optString("icon");
            b.color = j.optString("color", "#37474F");
            b.type = j.optString("type", "command");
            b.value = j.optString("value");
            b.args = j.isNull("args") ? "" : j.optString("args");
            b.slot = j.optInt("slot", -1);
            JSONObject aj = j.optJSONObject("alt");
            if (aj != null) { b.alt = fromJson(aj); b.alt.id = b.id; }
            return b;
        }

        JSONObject toJson() throws JSONException {
            JSONObject j = new JSONObject();
            // An empty id makes the server-side default generate nothing, so always supply one.
            j.put("id", id.length() > 0 ? id : Long.toHexString(System.nanoTime() & 0xFFFFFFFFL));
            j.put("label", label);
            j.put("icon", icon);
            j.put("color", color);
            j.put("type", type);
            j.put("value", value);
            j.put("slot", slot);
            if (args.length() > 0) j.put("args", args);
            return j;
        }

        public String toJsonString() {
            try { return toJson().toString(); } catch (JSONException e) { return "{}"; }
        }

        public static Button parse(String s) {
            try { return fromJson(new JSONObject(s)); } catch (JSONException e) { return new Button(); }
        }
    }

    public static class Page {
        public String name = "Main";
        public String app = ""; // PC program this page is anchored to; empty = none
        public final List<Button> buttons = new ArrayList<Button>();
        public List<Widget> widgets = defaultWidgets(); // built-in tiles of this page
    }

    /** A built-in tile (volume, mute, ...) covering a rectangle of cells of the 7x4 screen. */
    public static class Widget {
        public String kind = "";
        public boolean visible = true;
        public int col, row, w = 1, h = 1;
        public String color = ""; // "" = the tile's default
        public final java.util.Map<String, String> options = new java.util.HashMap<String, String>(); // per-widget settings from the PC

        Widget(String kind, int col, int row, int w, int h) {
            this.kind = kind; this.col = col; this.row = row; this.w = w; this.h = h;
        }

        static Widget fromJson(JSONObject j) {
            Widget d = new Widget(j.optString("kind"), j.optInt("col"), j.optInt("row"), Math.max(1, j.optInt("w", 1)), Math.max(1, j.optInt("h", 1)));
            d.visible = j.optBoolean("visible", true);
            d.color = j.isNull("color") ? "" : j.optString("color");
            JSONObject oj = j.optJSONObject("options");
            if (oj != null) {
                java.util.Iterator<String> keys = oj.keys();
                while (keys.hasNext()) { String k = keys.next(); d.options.put(k, oj.optString(k)); }
            }
            // keep it on the grid, whatever the server sent
            d.w = Math.min(d.w, DeckBoard.COLS);
            d.h = Math.min(d.h, DeckBoard.ROWS);
            d.col = Math.max(0, Math.min(d.col, DeckBoard.COLS - d.w));
            d.row = Math.max(0, Math.min(d.row, DeckBoard.ROWS - d.h));
            return d;
        }

        boolean covers(int cell) {
            int c = cell % DeckBoard.COLS, r = cell / DeckBoard.COLS;
            return c >= col && c < col + w && r >= row && r < row + h;
        }
    }

    /** Where the tiles sit when the server sends no widget list (an older server). */
    public static List<Widget> defaultWidgets() {
        List<Widget> l = new ArrayList<Widget>();
        l.add(new Widget("volume", 0, 0, 1, 2));
        l.add(new Widget("mute", 0, 2, 1, 1));
        l.add(new Widget("menu", 0, 3, 1, 1));
        l.add(new Widget("stats", 6, 0, 1, 2));
        l.add(new Widget("mascot", 6, 2, 1, 1));
        l.add(new Widget("page", 6, 3, 1, 1));
        return l;
    }

    public final List<Page> pages = new ArrayList<Page>();

    private static List<Widget> parseWidgets(JSONArray arr) {
        if (arr == null || arr.length() == 0) return null;
        List<Widget> l = new ArrayList<Widget>();
        for (int i = 0; i < arr.length(); i++) {
            JSONObject wj = arr.optJSONObject(i);
            if (wj != null) l.add(Widget.fromJson(wj));
        }
        return l;
    }

    public static Deck fromJson(JSONObject root) {
        Deck d = new Deck();
        JSONArray pages = root.optJSONArray("pages");
        if (pages != null) {
            for (int i = 0; i < pages.length(); i++) {
                JSONObject pj = pages.optJSONObject(i);
                if (pj == null) continue;
                Page p = new Page();
                p.name = pj.optString("name", "Page " + (i + 1));
                p.app = pj.optString("app", "");
                // per-page widgets; an older server sends one deck-wide list instead
                List<Widget> ws = parseWidgets(pj.optJSONArray("widgets"));
                if (ws == null) ws = parseWidgets(root.optJSONArray("widgets"));
                if (ws != null) p.widgets = ws;
                JSONArray bs = pj.optJSONArray("buttons");
                if (bs != null) {
                    for (int k = 0; k < bs.length(); k++) {
                        JSONObject bj = bs.optJSONObject(k);
                        if (bj != null) p.buttons.add(Button.fromJson(bj));
                    }
                }
                d.pages.add(p);
            }
        }
        if (d.pages.isEmpty()) d.pages.add(new Page());
        return d;
    }

    public JSONObject toJson() throws JSONException {
        JSONArray pa = new JSONArray();
        for (Page p : pages) {
            JSONArray ba = new JSONArray();
            for (Button b : p.buttons) ba.put(b.toJson());
            pa.put(new JSONObject().put("name", p.name).put("app", p.app).put("buttons", ba));
        }
        return new JSONObject().put("pages", pa);
    }
}
