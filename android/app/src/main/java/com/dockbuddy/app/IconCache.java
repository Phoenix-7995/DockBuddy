package com.dockbuddy.app;

import android.content.Context;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;

import java.util.HashMap;
import java.util.HashSet;
import java.util.Map;
import java.util.Set;

/** In-memory cache of custom button images ("img:name"), downloaded from the PC on first use. */
public final class IconCache {
    public static final String PREFIX = "img:";

    public interface Ready {
        void onReady();
    }

    private static final Map<String, Bitmap> BITMAPS = new HashMap<String, Bitmap>();
    private static final Set<String> LOADING = new HashSet<String>();

    private IconCache() {}

    public static boolean isCustom(String icon) { return icon != null && icon.startsWith(PREFIX); }

    /** The bitmap if already downloaded; otherwise starts the download and calls ready when it arrives. */
    public static Bitmap get(Context ctx, final String icon, final Ready ready) {
        Bitmap b = BITMAPS.get(icon);
        if (b != null || LOADING.contains(icon)) return b;
        LOADING.add(icon);
        new ApiClient(new Prefs(ctx)).getBytes("/api/icon/" + icon.substring(PREFIX.length()), new ApiClient.BytesCallback() {
            @Override public void onResult(byte[] data) {
                LOADING.remove(icon);
                Bitmap bmp = data == null ? null : BitmapFactory.decodeByteArray(data, 0, data.length);
                if (bmp == null) return; // retried the next time the button is bound
                BITMAPS.put(icon, bmp);
                ready.onReady();
            }
        });
        return null;
    }
}
