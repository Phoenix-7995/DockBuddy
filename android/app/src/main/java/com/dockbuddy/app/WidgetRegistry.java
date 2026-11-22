package com.dockbuddy.app;

import java.util.LinkedHashMap;
import java.util.Map;

/**
 * Every widget kind the deck screen knows. Add a line to the static block to register a new one;
 * the kind string must match the one registered on the server (Widgets.cs).
 */
public final class WidgetRegistry {
    public interface Factory {
        DeckWidget create(DeckHost host);
    }

    private static final Map<String, Factory> FACTORIES = new LinkedHashMap<String, Factory>();

    static {
        register("volume", new Factory() { @Override public DeckWidget create(DeckHost h) { return new VolumeWidget(h); } });
        register("mute", new Factory() { @Override public DeckWidget create(DeckHost h) { return new MuteWidget(h); } });
        register("menu", new Factory() { @Override public DeckWidget create(DeckHost h) { return new MenuWidget(h); } });
        register("stats", new Factory() { @Override public DeckWidget create(DeckHost h) { return new StatsWidget(h); } });
        register("mascot", new Factory() { @Override public DeckWidget create(DeckHost h) { return new CatWidget(h, CatStyle.GINNY); } });
        register("caspar", new Factory() { @Override public DeckWidget create(DeckHost h) { return new CatWidget(h, CatStyle.CASPAR); } });
        register("mimi", new Factory() { @Override public DeckWidget create(DeckHost h) { return new CatWidget(h, CatStyle.MIMI); } });
        register("page", new Factory() { @Override public DeckWidget create(DeckHost h) { return new PageWidget(h); } });
        register("mic", new Factory() { @Override public DeckWidget create(DeckHost h) { return new MicWidget(h); } });
        register("mixer", new Factory() { @Override public DeckWidget create(DeckHost h) { return new MixerWidget(h); } });
        register("media", new Factory() { @Override public DeckWidget create(DeckHost h) { return new MediaWidget(h); } });
        register("clock", new Factory() { @Override public DeckWidget create(DeckHost h) { return new ClockWidget(h); } });
    }

    private WidgetRegistry() {}

    public static void register(String kind, Factory factory) { FACTORIES.put(kind, factory); }

    /** One new widget per registered kind, keyed by kind. */
    public static Map<String, DeckWidget> createAll(DeckHost host) {
        Map<String, DeckWidget> all = new LinkedHashMap<String, DeckWidget>();
        for (Map.Entry<String, Factory> e : FACTORIES.entrySet()) all.put(e.getKey(), e.getValue().create(host));
        return all;
    }
}
