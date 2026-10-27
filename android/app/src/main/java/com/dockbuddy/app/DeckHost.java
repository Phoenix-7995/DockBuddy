package com.dockbuddy.app;

import android.content.Context;

import org.json.JSONObject;

/** What a {@link DeckWidget} may ask of the deck screen it lives on. */
public interface DeckHost {
    Context context();

    /** Client for the PC server (calls complete on the UI thread). */
    ApiClient api();

    void toast(String text);

    /** Opens another screen of the app (e.g. MonitorActivity.class). */
    void openActivity(Class<?> activity);

    /** Switches to the next page of buttons. */
    void nextPage();

    /** Shows the app menu (Popcorn mode, PC monitor, Change PC). */
    void showMenu();

    /** POSTs to /api/volume ({"level":n} or {"mute":"toggle"}); every widget then gets {@link DeckWidget#onVolume}. */
    void sendVolume(JSONObject body);
}
