package com.dockbuddy.app;

import android.os.Handler;
import android.os.Looper;

import org.json.JSONObject;

import java.io.ByteArrayOutputStream;
import java.io.File;
import java.io.FileOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.net.HttpURLConnection;
import java.net.URL;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

/** Minimal JSON-over-HTTP client for the DockBuddy PC server. */
public class ApiClient {

    public interface Callback {
        void onResult(JSONObject json, String error);
    }

    private static final ExecutorService POOL = Executors.newFixedThreadPool(3);
    private static final Handler MAIN = new Handler(Looper.getMainLooper());

    private final String base;
    private final String token;

    public ApiClient(String host, int port, String token) {
        this.base = "http://" + host + ":" + port;
        this.token = token;
    }

    public ApiClient(Prefs p) {
        this(p.host(), p.port(), p.token());
    }

    /** Runs the request on a background thread and delivers the result on the UI thread. */
    public void call(final String method, final String path, final JSONObject body, final Callback cb) {
        POOL.execute(new Runnable() {
            @Override public void run() {
                JSONObject result = null;
                String error = null;
                try {
                    result = request(method, path, body);
                } catch (Exception e) {
                    error = e.getMessage() != null ? e.getMessage() : e.getClass().getSimpleName();
                }
                final JSONObject r = result;
                final String err = error;
                MAIN.post(new Runnable() {
                    @Override public void run() { if (cb != null) cb.onResult(r, err); }
                });
            }
        });
    }

    public interface BytesCallback {
        void onResult(byte[] data);
    }

    /** Downloads a binary resource (custom icon) in the background; delivers null on failure. */
    public void getBytes(final String path, final BytesCallback cb) {
        POOL.execute(new Runnable() {
            @Override public void run() {
                byte[] data = null;
                HttpURLConnection c = null;
                try {
                    c = (HttpURLConnection) new URL(base + path).openConnection();
                    c.setConnectTimeout(4000);
                    c.setReadTimeout(15000);
                    c.setRequestProperty("X-DockBuddy-Token", token);
                    if (c.getResponseCode() == 200) {
                        InputStream in = c.getInputStream();
                        ByteArrayOutputStream out = new ByteArrayOutputStream();
                        byte[] buf = new byte[4096];
                        int n;
                        while ((n = in.read(buf)) > 0) out.write(buf, 0, n);
                        in.close();
                        data = out.toByteArray();
                    }
                } catch (Exception ignored) {
                } finally {
                    if (c != null) c.disconnect();
                }
                final byte[] d = data;
                MAIN.post(new Runnable() {
                    @Override public void run() { cb.onResult(d); }
                });
            }
        });
    }

    public interface DownloadCallback {
        void onProgress(long done, long total);
        void onDone(String error); // null on success
    }

    /** Downloads a file in the background, reporting progress and the result on the UI thread. */
    public void download(final String path, final File dest, final DownloadCallback cb) {
        POOL.execute(new Runnable() {
            @Override public void run() {
                String err = null;
                HttpURLConnection c = null;
                try {
                    c = (HttpURLConnection) new URL(base + path).openConnection();
                    c.setConnectTimeout(4000);
                    c.setReadTimeout(30000);
                    c.setRequestProperty("X-DockBuddy-Token", token);
                    if (c.getResponseCode() != 200) throw new IOException("Server error " + c.getResponseCode());
                    final long total = c.getContentLength();
                    File dir = dest.getParentFile();
                    if (dir != null && !dir.isDirectory() && !dir.mkdirs() && !dir.isDirectory()) {
                        throw new IOException("Cannot create the folder " + dir.getPath());
                    }
                    InputStream in = c.getInputStream();
                    FileOutputStream out = new FileOutputStream(dest);
                    byte[] buf = new byte[16384];
                    long done = 0;
                    int n;
                    long lastPost = 0;
                    while ((n = in.read(buf)) > 0) {
                        out.write(buf, 0, n);
                        done += n;
                        long now = System.currentTimeMillis();
                        if (now - lastPost > 100) {
                            lastPost = now;
                            final long d = done;
                            MAIN.post(new Runnable() { @Override public void run() { cb.onProgress(d, total); } });
                        }
                    }
                    out.close();
                    in.close();
                } catch (Exception e) {
                    err = e.getMessage() != null ? e.getMessage() : e.getClass().getSimpleName();
                } finally {
                    if (c != null) c.disconnect();
                }
                final String e = err;
                MAIN.post(new Runnable() { @Override public void run() { cb.onDone(e); } });
            }
        });
    }

    public void get(String path, Callback cb) { call("GET", path, null, cb); }
    public void post(String path, JSONObject body, Callback cb) { call("POST", path, body, cb); }

    /** Blocking request; throws IOException with a readable message on failure. */
    public JSONObject request(String method, String path, JSONObject body) throws Exception {
        HttpURLConnection c = (HttpURLConnection) new URL(base + path).openConnection();
        try {
            c.setRequestMethod(method);
            c.setConnectTimeout(4000);
            c.setReadTimeout(35000);
            c.setRequestProperty("X-DockBuddy-Token", token);
            c.setRequestProperty("Accept", "application/json");
            if (body != null) {
                byte[] data = body.toString().getBytes("UTF-8");
                c.setDoOutput(true);
                c.setRequestProperty("Content-Type", "application/json");
                c.setFixedLengthStreamingMode(data.length);
                OutputStream os = c.getOutputStream();
                os.write(data);
                os.close();
            }
            int code = c.getResponseCode();
            if (code == 401) throw new IOException("Wrong token");
            InputStream in = code >= 400 ? c.getErrorStream() : c.getInputStream();
            String text = in == null ? "" : read(in);
            if (code >= 400) {
                String detail = "";
                try { detail = new JSONObject(text).optString("error"); } catch (Exception ignored) { }
                throw new IOException("Server error " + code + (detail.length() > 0 ? ": " + detail : ""));
            }
            if (text.length() == 0) return new JSONObject();
            if (text.trim().startsWith("[")) return new JSONObject().put("items", new org.json.JSONArray(text)); // bare arrays arrive as {"items": [...]}
            return new JSONObject(text);
        } finally {
            c.disconnect();
        }
    }

    private static String read(InputStream in) throws IOException {
        ByteArrayOutputStream out = new ByteArrayOutputStream();
        byte[] buf = new byte[4096];
        int n;
        while ((n = in.read(buf)) > 0) out.write(buf, 0, n);
        in.close();
        return out.toString("UTF-8");
    }
}
