package com.dockbuddy.app;

import android.app.Activity;
import android.app.AlertDialog;
import android.app.ProgressDialog;
import android.content.Context;
import android.content.DialogInterface;
import android.content.Intent;
import android.content.pm.PackageInfo;
import android.net.Uri;
import android.os.Build;
import android.provider.Settings;
import android.widget.Toast;

import org.json.JSONObject;

import java.io.File;
import java.util.ArrayList;
import java.util.List;

/**
 * Remote update: asks the PC server which app version it carries and, if that is newer than the installed
 * one, downloads the APK from the PC and hands it to the system installer.
 */
final class AppUpdater {
    private static boolean autoChecked;      // the automatic check runs once per app launch
    private static int declinedVersion = -1; // "Later" is remembered until the next launch

    private AppUpdater() {}

    /**
     * Where the downloaded APK is kept: the first folder from the list below that exists (or can be created) and can be
     * written to. Old Android needs a place the installer can read, so it tries external storage first there; newer
     * Android reads it through ApkProvider, so the app's own cache is enough. The download and the provider both call this
     * and so always agree on the file.
     */
    static File apkFile(Context c) {
        List<File> dirs = new ArrayList<File>();
        if (Build.VERSION.SDK_INT < 24) dirs.add(c.getExternalFilesDir("update"));
        dirs.add(new File(c.getCacheDir(), "update"));
        dirs.add(new File(c.getFilesDir(), "update"));
        for (File dir : dirs) {
            if (dir == null) continue; // external storage not available
            if ((dir.isDirectory() || dir.mkdirs()) && dir.canWrite()) return new File(dir, "DockBuddy.apk");
        }
        return new File(new File(c.getCacheDir(), "update"), "DockBuddy.apk"); // nothing worked: the download reports why
    }

    /** Checks once per launch, quietly: only speaks up if there is an update. */
    static void checkOnce(Activity a, ApiClient api) {
        if (autoChecked) return;
        autoChecked = true;
        check(a, api, false);
    }

    /** manual = the user asked, so also say when there is nothing new. */
    static void check(final Activity a, final ApiClient api, final boolean manual) {
        api.get("/api/app", new ApiClient.Callback() {
            @Override public void onResult(JSONObject j, String error) {
                if (a.isFinishing()) return;
                if (j == null || j.optInt("versionCode") <= 0) {
                    if (manual) toast(a, error != null && error.contains("404") ? "This PC has no app update to offer" : "Could not check for updates");
                    return;
                }
                int offered = j.optInt("versionCode");
                if (offered > installedCode(a)) {
                    if (manual || offered != declinedVersion) offer(a, api, j);
                } else if (manual) {
                    toast(a, "DockBuddy is up to date (" + installedName(a) + ")");
                }
            }
        });
    }

    private static void offer(final Activity a, final ApiClient api, final JSONObject info) {
        final int offered = info.optInt("versionCode");
        new AlertDialog.Builder(a).setTitle("Update available")
                .setMessage("DockBuddy " + info.optString("versionName") + " is available from your PC (you have "
                        + installedName(a) + ").\n\nUpdate now?")
                .setPositiveButton("Update", new DialogInterface.OnClickListener() {
                    @Override public void onClick(DialogInterface d, int w) { install(a, api); }
                })
                .setNegativeButton("Later", new DialogInterface.OnClickListener() {
                    @Override public void onClick(DialogInterface d, int w) { declinedVersion = offered; }
                }).show();
    }

    private static void install(final Activity a, ApiClient api) {
        if (Build.VERSION.SDK_INT >= 26 && !a.getPackageManager().canRequestPackageInstalls()) {
            new AlertDialog.Builder(a).setTitle("Allow installing")
                    .setMessage("Android needs your permission for DockBuddy to install its own updates. "
                            + "Turn on \"Allow from this source\" on the next screen, come back and try again.")
                    .setPositiveButton("Open settings", new DialogInterface.OnClickListener() {
                        @Override public void onClick(DialogInterface d, int w) {
                            a.startActivity(new Intent(Settings.ACTION_MANAGE_UNKNOWN_APP_SOURCES, Uri.parse("package:" + a.getPackageName())));
                        }
                    }).setNegativeButton("Cancel", null).show();
            return;
        }
        final File dest = apkFile(a);
        final ProgressDialog progress = new ProgressDialog(a);
        progress.setTitle("Downloading update");
        progress.setProgressStyle(ProgressDialog.STYLE_HORIZONTAL);
        progress.setIndeterminate(true);
        progress.setCancelable(false);
        progress.show();
        api.download("/api/app/apk", dest, new ApiClient.DownloadCallback() {
            @Override public void onProgress(long done, long total) {
                if (total > 0) {
                    progress.setIndeterminate(false);
                    progress.setMax((int) (total / 1024));
                    progress.setProgress((int) (done / 1024));
                }
            }

            @Override public void onDone(String error) {
                try { progress.dismiss(); } catch (Exception ignored) { }
                if (error != null) { toast(a, "Download failed: " + error); return; }
                launchInstaller(a, dest);
            }
        });
    }

    private static void launchInstaller(Activity a, File apk) {
        Intent i = new Intent(Intent.ACTION_VIEW);
        if (Build.VERSION.SDK_INT >= 24) {
            i.setDataAndType(ApkProvider.URI, "application/vnd.android.package-archive");
            i.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION);
        } else {
            makeReadable(a, apk);
            i.setDataAndType(Uri.fromFile(apk), "application/vnd.android.package-archive");
        }
        i.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
        try {
            a.startActivity(i);
        } catch (Exception e) {
            toast(a, "Could not start the installer: " + e.getMessage());
        }
    }

    /**
     * Old Android's installer reads the file as another user. On external storage that just works; if the APK had to
     * go into the app's private folder, let the installer walk down to it (execute permission on the folders, read on the file).
     */
    private static void makeReadable(Context c, File apk) {
        apk.setReadable(true, false);
        File stop = c.getFilesDir().getParentFile();
        for (File d = apk.getParentFile(); d != null && d.getPath().startsWith(stop.getPath()); d = d.getParentFile()) {
            d.setExecutable(true, false);
        }
    }

    static int installedCode(Context c) {
        try {
            PackageInfo p = c.getPackageManager().getPackageInfo(c.getPackageName(), 0);
            return p.versionCode;
        } catch (Exception e) {
            return 0;
        }
    }

    static String installedName(Context c) {
        try {
            return "v" + c.getPackageManager().getPackageInfo(c.getPackageName(), 0).versionName;
        } catch (Exception e) {
            return "unknown version";
        }
    }

    private static void toast(Context c, String s) { Toast.makeText(c, s, Toast.LENGTH_LONG).show(); }
}
