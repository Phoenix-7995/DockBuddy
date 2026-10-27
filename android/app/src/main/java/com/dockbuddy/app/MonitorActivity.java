package com.dockbuddy.app;

import android.app.Activity;
import android.os.Build;
import android.os.Bundle;
import android.os.Handler;
import android.view.Gravity;
import android.view.View;
import android.view.ViewGroup;
import android.view.WindowManager;
import android.widget.BaseAdapter;
import android.widget.FrameLayout;
import android.widget.GridView;

import org.json.JSONArray;
import org.json.JSONObject;

import java.util.ArrayList;
import java.util.List;

/** Full-screen PC dashboard made of tiles three cubes wide: host, CPU, RAM, network and one cube per disk. */
public class MonitorActivity extends Activity {
    private static final int TARGET_CUBE_DP = 110;
    private static final int TILE_SPAN = 3; // a stat tile is as wide as three deck cubes

    private static class Cube {
        final String[] labels, values;
        final int[] percents;
        Cube(String[] l, String[] v, int[] p) { labels = l; values = v; percents = p; }
    }

    private final Handler handler = new Handler();
    private final List<Cube> cubes = new ArrayList<Cube>();
    private ApiClient api;
    private FrameLayout root;
    private GridView grid;
    private Adapter adapter;
    private boolean polling;
    private int cube;

    @Override protected void onCreate(Bundle b) {
        super.onCreate(b);
        getWindow().addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON
                | WindowManager.LayoutParams.FLAG_FULLSCREEN);
        api = new ApiClient(new Prefs(this));

        root = new FrameLayout(this) {
            @Override protected void onSizeChanged(final int w, final int h, int ow, int oh) {
                super.onSizeChanged(w, h, ow, oh);
                post(new Runnable() {
                    @Override public void run() { layoutGrid(w, h); }
                });
            }
        };
        root.setBackgroundColor(0xFF000000);
        grid = new GridView(this);
        grid.setStretchMode(GridView.NO_STRETCH);
        grid.setSelector(android.R.color.transparent);
        grid.setOverScrollMode(View.OVER_SCROLL_NEVER);
        grid.setVerticalScrollBarEnabled(false);
        adapter = new Adapter();
        grid.setAdapter(adapter);
        root.addView(grid, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.MATCH_PARENT, Gravity.CENTER_HORIZONTAL));
        setContentView(root);
        cubes.add(new Cube(new String[]{"PC"}, new String[]{"connecting"}, new int[]{-1}));
    }

    private void layoutGrid(int w, int h) {
        float d = getResources().getDisplayMetrics().density;
        int rows = Math.max(3, Math.round(h / (TARGET_CUBE_DP * d)));
        cube = h / rows;
        // Each tile is three cubes wide.
        int cols = Math.max(1, w / (TILE_SPAN * cube));
        FrameLayout.LayoutParams lp = new FrameLayout.LayoutParams(cols * TILE_SPAN * cube, rows * cube, Gravity.CENTER);
        grid.setLayoutParams(lp);
        grid.setColumnWidth(TILE_SPAN * cube);
        grid.setNumColumns(cols);
        adapter.notifyDataSetChanged();
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
    }

    @Override protected void onPause() {
        super.onPause();
        polling = false;
        handler.removeCallbacks(poll);
    }

    private final Runnable poll = new Runnable() {
        @Override public void run() {
            if (!polling) return;
            api.get("/api/stats", new ApiClient.Callback() {
                @Override public void onResult(JSONObject j, String error) { render(j); }
            });
            handler.postDelayed(this, 1500);
        }
    };

    private static Cube one(String label, String value, int percent) {
        return new Cube(new String[]{label}, new String[]{value}, new int[]{percent});
    }

    private void render(JSONObject j) {
        cubes.clear();
        if (j == null) {
            cubes.add(one("PC", "offline", -1));
        } else {
            long up = j.optLong("uptimeSeconds");
            cubes.add(new Cube(new String[]{"PC", "UP"},
                    new String[]{j.optString("host"), (up / 86400) + "d " + ((up % 86400) / 3600) + "h"},
                    new int[]{-1, -1}));
            int cpu = (int) j.optDouble("cpu"), ram = (int) j.optDouble("ramPercent");
            cubes.add(one("CPU", cpu + "%", cpu));
            cubes.add(one("RAM " + j.optLong("ramUsedMb") + "/" + j.optLong("ramTotalMb") + " MB", ram + "%", ram));
            JSONObject gpu = j.optJSONObject("gpu");
            if (gpu != null) {
                int load = (int) Math.round(gpu.optDouble("load"));
                String temp = gpu.isNull("tempC") ? "" : "  " + Math.round(gpu.optDouble("tempC")) + "\u00B0C";
                cubes.add(one("GPU" + temp, load + "%", load));
                long used = gpu.optLong("memUsedMb"), total = gpu.optLong("memTotalMb");
                if (total > 0) {
                    int pct = (int) (100 * used / total);
                    cubes.add(one(String.format("VRAM %.1f/%.1f GB", used / 1024.0, total / 1024.0), pct + "%", pct));
                }
            }
            cubes.add(new Cube(new String[]{"DL", "UL"},
                    new String[]{DeckActivity.rate(j.optDouble("netDownKbps")), DeckActivity.rate(j.optDouble("netUpKbps"))},
                    new int[]{-1, -1}));
            JSONArray disks = j.optJSONArray("disks");
            for (int i = 0; disks != null && i < disks.length(); i++) {
                JSONObject dk = disks.optJSONObject(i);
                if (dk == null) continue;
                double used = dk.optDouble("usedGb"), total = dk.optDouble("totalGb");
                int pct = total > 0 ? (int) (100 * used / total) : 0;
                cubes.add(one(dk.optString("name") + " " + (int) used + "/" + (int) total + " GB", pct + "%", pct));
            }
        }
        adapter.notifyDataSetChanged();
    }

    private class Adapter extends BaseAdapter {
        @Override public int getCount() { return cubes.size(); }
        @Override public Object getItem(int i) { return cubes.get(i); }
        @Override public long getItemId(int i) { return i; }
        @Override public View getView(int i, View convert, ViewGroup parent) {
            StatTile t = convert instanceof StatTile ? (StatTile) convert : new StatTile(MonitorActivity.this);
            t.setLayoutParams(new GridView.LayoutParams(TILE_SPAN * cube, cube));
            Cube c = cubes.get(i);
            t.setRows(c.labels, c.values, c.percents);
            return t;
        }
    }
}
