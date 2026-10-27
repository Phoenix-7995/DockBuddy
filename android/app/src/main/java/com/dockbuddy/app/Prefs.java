package com.dockbuddy.app;

import android.content.Context;
import android.content.SharedPreferences;

import org.json.JSONArray;
import org.json.JSONObject;

import java.util.ArrayList;
import java.util.List;

/** Saved connection details: the PC in use, plus every PC this phone has paired with. */
public class Prefs {
    /** A PC this phone has paired with. */
    public static class Pc {
        public String name = "", host = "", token = "";
        public String mac = "", broadcast = ""; // for Wake-on-LAN, learned while the PC is on
        public int port = 8765;

        public boolean same(String host, int port) { return this.host.equals(host) && this.port == port; }

        public String label() { return name.length() > 0 ? name : host; }
    }

    private final SharedPreferences sp;

    public Prefs(Context c) {
        sp = c.getSharedPreferences("dockbuddy", Context.MODE_PRIVATE);
    }

    public String host() { return sp.getString("host", ""); }
    public int port() { return sp.getInt("port", 8765); }
    public String token() { return sp.getString("token", ""); }

    public boolean isConfigured() { return host().length() > 0 && token().length() > 0; }

    /** Makes this PC the current one and remembers it (a PC already in the list keeps its name unless a new one is given). */
    public void save(String name, String host, int port, String token) {
        List<Pc> list = pcs();
        Pc pc = null;
        for (Pc p : list) if (p.same(host, port)) pc = p;
        if (pc == null) { pc = new Pc(); list.add(pc); }
        if (name != null && name.length() > 0) pc.name = name;
        pc.host = host;
        pc.port = port;
        pc.token = token;
        writePcs(list);
        sp.edit().putString("host", host).putInt("port", port).putString("token", token).commit();
    }

    public void save(String host, int port, String token) { save("", host, port, token); }

    /** Every PC this phone has paired with, oldest first. A PC saved before this list existed is added the first time. */
    public List<Pc> pcs() {
        List<Pc> list = new ArrayList<Pc>();
        try {
            JSONArray arr = new JSONArray(sp.getString("pcs", "[]"));
            for (int i = 0; i < arr.length(); i++) {
                JSONObject o = arr.getJSONObject(i);
                Pc p = new Pc();
                p.name = o.optString("name");
                p.host = o.optString("host");
                p.port = o.optInt("port", 8765);
                p.token = o.optString("token");
                p.mac = o.optString("mac");
                p.broadcast = o.optString("broadcast");
                list.add(p);
            }
        } catch (Exception ignored) { }
        if (isConfigured()) {
            boolean known = false;
            for (Pc p : list) if (p.same(host(), port())) known = true;
            if (!known) {
                Pc p = new Pc();
                p.host = host();
                p.port = port();
                p.token = token();
                list.add(p);
            }
        }
        return list;
    }

    /** The PC in use (with its saved Wake-on-LAN details), or null if none is paired. */
    public Pc current() {
        for (Pc p : pcs()) if (p.same(host(), port())) return p;
        return null;
    }

    /** Remembers the hardware address a PC can be woken at. */
    public void setWake(String host, int port, String mac, String broadcast) {
        List<Pc> list = pcs();
        for (Pc p : list) {
            if (p.same(host, port)) { p.mac = mac; p.broadcast = broadcast; }
        }
        writePcs(list);
    }

    public boolean isCurrent(Pc pc) { return pc.same(host(), port()); }

    /** Removes a PC from the list; if it was the current one the phone is unpaired. */
    public void forget(Pc pc) {
        List<Pc> list = new ArrayList<Pc>();
        for (Pc p : pcs()) if (!p.same(pc.host, pc.port)) list.add(p);
        writePcs(list);
        if (isCurrent(pc)) sp.edit().remove("host").remove("port").remove("token").commit();
    }

    private void writePcs(List<Pc> list) {
        JSONArray arr = new JSONArray();
        try {
            for (Pc p : list) {
                arr.put(new JSONObject().put("name", p.name).put("host", p.host).put("port", p.port).put("token", p.token)
                        .put("mac", p.mac).put("broadcast", p.broadcast));
            }
        } catch (Exception ignored) { }
        sp.edit().putString("pcs", arr.toString()).commit();
    }

    public void clear() { sp.edit().clear().commit(); }
}
