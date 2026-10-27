package com.dockbuddy.app;

import android.os.Handler;
import android.os.Looper;

import java.net.DatagramPacket;
import java.net.DatagramSocket;
import java.net.InetAddress;

/** Sends a Wake-on-LAN "magic packet": 6 bytes of 0xFF followed by the PC's hardware address 16 times, over UDP. */
final class WakeOnLan {
    interface Done {
        void onDone(String error); // null on success
    }

    private WakeOnLan() {}

    /** The 102-byte magic packet for a hardware address like "A0-B1-C2-D3-E4-F5" (also accepts ":" and no separator). */
    static byte[] packet(String mac) {
        String hex = mac.replaceAll("[^0-9A-Fa-f]", "");
        if (hex.length() != 12) throw new IllegalArgumentException("Bad hardware address: " + mac);
        byte[] addr = new byte[6];
        for (int i = 0; i < 6; i++) addr[i] = (byte) Integer.parseInt(hex.substring(i * 2, i * 2 + 2), 16);
        byte[] p = new byte[102];
        for (int i = 0; i < 6; i++) p[i] = (byte) 0xFF;
        for (int i = 0; i < 16; i++) System.arraycopy(addr, 0, p, 6 + i * 6, 6);
        return p;
    }

    /** Sends to the PC's LAN broadcast address (if known) and to 255.255.255.255, on the two usual ports (9 and 7). */
    static void send(final String mac, final String broadcast, final Done done) {
        new Thread(new Runnable() {
            @Override public void run() {
                String error = null;
                DatagramSocket socket = null;
                try {
                    byte[] data = packet(mac);
                    socket = new DatagramSocket();
                    socket.setBroadcast(true);
                    String[] targets = broadcast != null && broadcast.length() > 0
                            ? new String[]{broadcast, "255.255.255.255"} : new String[]{"255.255.255.255"};
                    for (String target : targets) {
                        for (int port : new int[]{9, 7}) {
                            socket.send(new DatagramPacket(data, data.length, InetAddress.getByName(target), port));
                        }
                    }
                } catch (Exception e) {
                    error = e.getMessage() != null ? e.getMessage() : e.getClass().getSimpleName();
                } finally {
                    if (socket != null) socket.close();
                }
                final String err = error;
                new Handler(Looper.getMainLooper()).post(new Runnable() {
                    @Override public void run() { done.onDone(err); }
                });
            }
        }).start();
    }
}
