package com.dockbuddy.app;

import android.content.Context;
import android.net.DhcpInfo;
import android.net.wifi.WifiManager;
import android.os.Handler;
import android.os.Looper;

import org.json.JSONObject;

import java.net.DatagramPacket;
import java.net.DatagramSocket;
import java.net.InetAddress;
import java.util.ArrayList;
import java.util.List;

/** Finds DockBuddy servers on the local network with a UDP broadcast. */
public class Discovery {

    public static class Server {
        public final String name, host;
        public final int port;
        Server(String name, String host, int port) { this.name = name; this.host = host; this.port = port; }
        @Override public String toString() { return name + "  (" + host + ")"; }
    }

    public interface Listener {
        void onDone(List<Server> servers);
    }

    private static final int DISCOVERY_PORT = 8766;

    public static void scan(final Context context, final Listener l) {
        final Context app = context.getApplicationContext();
        final Handler main = new Handler(Looper.getMainLooper());
        new Thread(new Runnable() {
            @Override public void run() {
                final List<Server> found = new ArrayList<Server>();
                DatagramSocket socket = null;
                try {
                    socket = new DatagramSocket();
                    socket.setBroadcast(true);
                    socket.setSoTimeout(500);
                    byte[] msg = "DOCKBUDDY_DISCOVER".getBytes("UTF-8");
                    List<InetAddress> targets = new ArrayList<InetAddress>();
                    targets.add(InetAddress.getByName("255.255.255.255"));
                    InetAddress subnet = subnetBroadcast(app);
                    if (subnet != null) targets.add(subnet);
                    for (InetAddress t : targets) {
                        socket.send(new DatagramPacket(msg, msg.length, t, DISCOVERY_PORT));
                    }
                    long end = System.currentTimeMillis() + 2000;
                    byte[] buf = new byte[512];
                    while (System.currentTimeMillis() < end) {
                        try {
                            DatagramPacket p = new DatagramPacket(buf, buf.length);
                            socket.receive(p);
                            JSONObject j = new JSONObject(new String(p.getData(), 0, p.getLength(), "UTF-8"));
                            if (!"dockbuddy".equals(j.optString("app"))) continue;
                            String host = p.getAddress().getHostAddress();
                            boolean dup = false;
                            for (Server s : found) if (s.host.equals(host)) dup = true;
                            if (!dup) found.add(new Server(j.optString("name", host), host, j.optInt("port", 8765)));
                        } catch (java.net.SocketTimeoutException ignored) {
                        } catch (Exception ignored) {
                        }
                    }
                } catch (Exception ignored) {
                } finally {
                    if (socket != null) socket.close();
                }
                main.post(new Runnable() {
                    @Override public void run() { l.onDone(found); }
                });
            }
        }).start();
    }

    private static InetAddress subnetBroadcast(Context c) {
        try {
            WifiManager wm = (WifiManager) c.getSystemService(Context.WIFI_SERVICE);
            DhcpInfo d = wm.getDhcpInfo();
            if (d == null || d.ipAddress == 0) return null;
            int bcast = (d.ipAddress & d.netmask) | ~d.netmask;
            byte[] q = new byte[4];
            for (int i = 0; i < 4; i++) q[i] = (byte) ((bcast >> (i * 8)) & 0xFF);
            return InetAddress.getByAddress(q);
        } catch (Exception e) {
            return null;
        }
    }
}
