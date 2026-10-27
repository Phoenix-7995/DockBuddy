package com.dockbuddy.app;

import android.app.Activity;
import android.content.Intent;
import android.os.Bundle;
import android.view.View;
import android.view.ViewGroup;
import android.widget.Button;
import android.widget.EditText;
import android.widget.LinearLayout;
import android.widget.ScrollView;
import android.widget.TextView;
import android.widget.Toast;

import org.json.JSONObject;

import java.util.List;

/** Pair with a PC: auto-discover or enter address + token. */
public class ConnectActivity extends Activity {
    private Prefs prefs;
    private EditText hostEdit, portEdit, tokenEdit;
    private TextView status;
    private LinearLayout found, saved;
    private Button scanBtn;

    @Override protected void onCreate(Bundle b) {
        super.onCreate(b);
        prefs = new Prefs(this);

        // Skip pairing if we already have a saved PC (unless asked to change it).
        if (prefs.isConfigured() && !getIntent().getBooleanExtra("change", false)) {
            openDeck();
            return;
        }

        LinearLayout root = new LinearLayout(this);
        root.setOrientation(LinearLayout.HORIZONTAL);
        int pad = Ui.dp(this, 16);
        root.setPadding(pad, pad, pad, pad);
        ScrollView scroll = new ScrollView(this);
        scroll.setFillViewport(true);
        scroll.addView(root);
        setContentView(scroll);

        LinearLayout left = new LinearLayout(this);
        left.setOrientation(LinearLayout.VERTICAL);
        left.setPadding(0, 0, pad, 0);
        LinearLayout right = new LinearLayout(this);
        right.setOrientation(LinearLayout.VERTICAL);
        root.addView(left, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1));
        root.addView(right, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1));

        left.addView(Ui.text(this, "DockBuddy", 26));
        left.addView(Ui.text(this, "Run DockBuddy.Server.exe on the PC. It prints its address and token.", 14));

        saved = new LinearLayout(this);
        saved.setOrientation(LinearLayout.VERTICAL);
        left.addView(saved, Ui.matchWrap());
        showSaved();

        scanBtn = new Button(this);
        scanBtn.setText("Find PCs on this network");
        scanBtn.setOnClickListener(new View.OnClickListener() {
            @Override public void onClick(View v) { scan(); }
        });
        left.addView(scanBtn, Ui.matchWrap());

        found = new LinearLayout(this);
        found.setOrientation(LinearLayout.VERTICAL);
        left.addView(found, Ui.matchWrap());
        status = Ui.text(this, "", 14);
        left.addView(status);

        right.addView(Ui.text(this, "Or enter manually", 16));
        hostEdit = edit("PC address, e.g. 192.168.1.20", prefs.host());
        portEdit = edit("Port", String.valueOf(prefs.port()));
        portEdit.setInputType(android.text.InputType.TYPE_CLASS_NUMBER);
        tokenEdit = edit("Token", prefs.token());
        right.addView(hostEdit, Ui.matchWrap());
        right.addView(portEdit, Ui.matchWrap());
        right.addView(tokenEdit, Ui.matchWrap());

        Button connect = new Button(this);
        connect.setText("Connect");
        connect.setOnClickListener(new View.OnClickListener() {
            @Override public void onClick(View v) { connect(); }
        });
        right.addView(connect, Ui.matchWrap());
        scan();
    }

    /** The PCs this phone has paired with: tap to switch to one, press and hold to forget it. */
    private void showSaved() {
        saved.removeAllViews();
        final List<Prefs.Pc> pcs = prefs.pcs();
        if (pcs.isEmpty()) return;
        saved.addView(Ui.text(this, "Paired PCs (tap to connect, hold to wake or forget)", 14));
        for (final Prefs.Pc pc : pcs) {
            Button btn = new Button(this);
            btn.setText((prefs.isCurrent(pc) ? "\u2713 " : "") + pc.label() + "\n" + pc.host + ":" + pc.port);
            btn.setOnClickListener(new View.OnClickListener() {
                @Override public void onClick(View v) { connectTo(pc.name, pc.host, pc.port, pc.token); }
            });
            btn.setOnLongClickListener(new View.OnLongClickListener() {
                @Override public boolean onLongClick(View v) { pcMenu(pc); return true; }
            });
            saved.addView(btn, Ui.matchWrap());
        }
    }

    /** Press and hold on a paired PC: wake it, or forget it. */
    private void pcMenu(final Prefs.Pc pc) {
        final String[] items = {"Wake up", "Forget"};
        new android.app.AlertDialog.Builder(this).setTitle(pc.label())
                .setItems(items, new android.content.DialogInterface.OnClickListener() {
                    @Override public void onClick(android.content.DialogInterface d, int which) {
                        if (which == 1) { confirmForget(pc); return; }
                        wake(pc);
                    }
                }).show();
    }

    private void wake(Prefs.Pc pc) {
        if (pc.mac.length() == 0) {
            Toast.makeText(this, "Connect to this PC once while it is on so the phone can learn how to wake it", Toast.LENGTH_LONG).show();
            return;
        }
        final String label = pc.label();
        WakeOnLan.send(pc.mac, pc.broadcast, new WakeOnLan.Done() {
            @Override public void onDone(String error) {
                status.setText(error == null ? "Wake signal sent to " + label + ". It can take a minute to start." : "Could not send: " + error);
            }
        });
    }

    private void confirmForget(final Prefs.Pc pc) {
        new android.app.AlertDialog.Builder(this).setTitle("Forget " + pc.label() + "?")
                .setMessage("This phone will no longer remember its address and token.")
                .setPositiveButton("Forget", new android.content.DialogInterface.OnClickListener() {
                    @Override public void onClick(android.content.DialogInterface d, int w) {
                        prefs.forget(pc);
                        showSaved();
                    }
                }).setNegativeButton("Cancel", null).show();
    }

    private EditText edit(String hint, String value) {
        EditText e = new EditText(this);
        e.setHint(hint);
        e.setText(value);
        e.setSingleLine(true);
        return e;
    }

    private void scan() {
        scanBtn.setEnabled(false);
        status.setText("Searching…");
        found.removeAllViews();
        Discovery.scan(this, new Discovery.Listener() {
            @Override public void onDone(List<Discovery.Server> servers) {
                scanBtn.setEnabled(true);
                status.setText(servers.isEmpty() ? "No PC found. Enter its address manually." : "Tap a PC, then enter the token.");
                for (final Discovery.Server s : servers) {
                    Button btn = new Button(ConnectActivity.this);
                    btn.setText(s.toString());
                    btn.setOnClickListener(new View.OnClickListener() {
                        @Override public void onClick(View v) {
                            hostEdit.setText(s.host);
                            portEdit.setText(String.valueOf(s.port));
                            tokenEdit.requestFocus();
                        }
                    });
                    found.addView(btn, Ui.matchWrap());
                }
            }
        });
    }

    private void connect() {
        final String host = hostEdit.getText().toString().trim();
        final String token = tokenEdit.getText().toString().trim();
        int port;
        try { port = Integer.parseInt(portEdit.getText().toString().trim()); } catch (NumberFormatException e) { port = 8765; }
        if (host.length() == 0 || token.length() == 0) {
            Toast.makeText(this, "Enter the address and token", Toast.LENGTH_SHORT).show();
            return;
        }
        connectTo("", host, port, token);
    }

    /** Checks the PC answers with this token, then remembers it as paired and opens the deck. */
    private void connectTo(final String name, final String host, final int port, final String token) {
        status.setText("Connecting to " + host + "…");
        final ApiClient api = new ApiClient(host, port, token);
        api.get("/api/deck", new ApiClient.Callback() {
            @Override public void onResult(JSONObject json, String error) {
                if (error != null) {
                    status.setText("Could not connect: " + error);
                    return;
                }
                // The PC's name (no token needed) labels it in the paired list.
                api.get("/api/ping", new ApiClient.Callback() {
                    @Override public void onResult(JSONObject ping, String err) {
                        String pcName = ping != null ? ping.optString("name") : "";
                        prefs.save(pcName.length() > 0 ? pcName : name, host, port, token);
                        // Learn the PC's hardware address now, while it is on, so it can be woken later.
                        api.get("/api/wake", new ApiClient.Callback() {
                            @Override public void onResult(JSONObject wake, String e2) {
                                saveWakeInfo(prefs, host, port, wake);
                                openDeck();
                            }
                        });
                    }
                });
            }
        });
    }

    /** Stores the best adapter (wired first) from a /api/wake reply. */
    static void saveWakeInfo(Prefs prefs, String host, int port, JSONObject wake) {
        org.json.JSONArray arr = wake == null ? null : wake.optJSONArray("adapters");
        JSONObject first = arr == null ? null : arr.optJSONObject(0);
        if (first == null) return;
        prefs.setWake(host, port, first.optString("mac"), first.optString("broadcast"));
    }

    private void openDeck() {
        startActivity(new Intent(this, DeckActivity.class));
        finish();
    }
}
