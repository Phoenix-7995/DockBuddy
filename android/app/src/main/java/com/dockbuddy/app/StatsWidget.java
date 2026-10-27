package com.dockbuddy.app;

import android.view.View;

import org.json.JSONObject;

import java.util.ArrayList;
import java.util.Collections;
import java.util.Comparator;
import java.util.List;

/** Live CPU / RAM / disk / network numbers; tap opens the full PC monitor. Shows more rows when two or more cubes tall. */
class StatsWidget extends DeckWidget {
    private final StatTile tile;
    private int maxRows = 3;

    StatsWidget(final DeckHost host) {
        super(host);
        tile = new StatTile(host.context());
        tile.setRows(new String[]{"PC"}, new String[]{"connecting"}, new int[]{-1});
        tile.setOnClickListener(new View.OnClickListener() {
            @Override public void onClick(View v) { host.openActivity(MonitorActivity.class); }
        });
    }

    @Override public View view() { return tile; }

    @Override public void onConfigChanged(Deck.Widget config, int cellPx) {
        super.onConfigChanged(config, cellPx);
        tile.setBackgroundTint(color(0x1B2630));
        // As many rows as fit at about 30 dp each (never fewer than three); a taller widget shows more.
        float density = host.context().getResources().getDisplayMetrics().density;
        float heightDp = config.h * cellPx / density;
        maxRows = Math.max(3, (int) ((heightDp - 16) / 30));
    }

    private static class Row {
        final String label, value;
        final int percent, keep, order;
        Row(String label, String value, int percent, int keep, int order) {
            this.label = label; this.value = value; this.percent = percent; this.keep = keep; this.order = order;
        }
    }

    @Override public void onStats(JSONObject j) {
        if (j == null) {
            tile.setRows(new String[]{"PC"}, new String[]{"offline"}, new int[]{-1});
            return;
        }
        List<Row> rows = new ArrayList<Row>();
        // keep = which rows survive when space is short (lowest first); order = where a row sits on screen.
        int cpu = (int) j.optDouble("cpu"), ram = (int) j.optDouble("ramPercent");
        rows.add(new Row("CPU", cpu + "%", cpu, 0, 0));
        rows.add(new Row("RAM", ram + "%", ram, 1, 2));
        JSONObject gpu = j.optJSONObject("gpu");
        if (gpu != null) {
            int load = (int) Math.round(gpu.optDouble("load"));
            rows.add(new Row("GPU", load + "%", load, 2, 1));
            long used = gpu.optLong("memUsedMb"), total = gpu.optLong("memTotalMb");
            if (used > 0 || total > 0) {
                rows.add(total > 0
                        ? new Row("VRAM", gb(used) + "/" + gb(total), (int) (100 * used / total), 6, 3)
                        : new Row("VRAM", gb(used), -1, 6, 3));
            }
            if (!gpu.isNull("tempC")) {
                int temp = (int) Math.round(gpu.optDouble("tempC"));
                rows.add(new Row("GPU \u00B0C", temp + "\u00B0", Math.min(100, temp), 7, 7));
            }
        }
        JSONObject disk = j.optJSONArray("disks") == null ? null : j.optJSONArray("disks").optJSONObject(0);
        if (disk != null && disk.optDouble("totalGb") > 0) {
            int dsk = (int) (100 * disk.optDouble("usedGb") / disk.optDouble("totalGb"));
            rows.add(new Row("DSK", dsk + "%", dsk, 3, 4));
        }
        rows.add(new Row("DL", DeckActivity.rate(j.optDouble("netDownKbps")), -1, 4, 5));
        rows.add(new Row("UL", DeckActivity.rate(j.optDouble("netUpKbps")), -1, 5, 6));

        // Keep the most important rows that fit, then show them in reading order.
        Collections.sort(rows, new Comparator<Row>() {
            @Override public int compare(Row a, Row b) { return a.keep - b.keep; }
        });
        while (rows.size() > maxRows) rows.remove(rows.size() - 1);
        Collections.sort(rows, new Comparator<Row>() {
            @Override public int compare(Row a, Row b) { return a.order - b.order; }
        });
        String[] labels = new String[rows.size()], values = new String[rows.size()];
        int[] percents = new int[rows.size()];
        for (int i = 0; i < rows.size(); i++) {
            labels[i] = rows.get(i).label;
            values[i] = rows.get(i).value;
            percents[i] = rows.get(i).percent;
        }
        tile.setRows(labels, values, percents);
    }

    /** Megabytes as gigabytes with one decimal ("3.2"). */
    private static String gb(long mb) { return String.format("%.1f", mb / 1024.0); }
}
