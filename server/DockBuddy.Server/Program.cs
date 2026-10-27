using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using DockBuddy.Server;
using DockBuddy.Server.Actions;
using DockBuddy.Server.Audio;
using DockBuddy.Server.Media;
using DockBuddy.Server.Monitor;
using DockBuddy.Server.Update;

var dev = args.Contains("--dev") || !OperatingSystem.IsWindows();
var configDir = args.SkipWhile(a => a != "--config-dir").Skip(1).FirstOrDefault();

// On Windows (outside --dev) the server runs as a tray app; --dev and non-Windows runs stay headless for testing.
#if HEADLESS
var gui = false;
#else
var gui = !dev && OperatingSystem.IsWindows();
if (gui && !DockBuddy.Server.Gui.GuiLauncher.TryAcquireSingleInstance()) return; // already running: it will show its window
#endif
var startHidden = args.Contains("--minimized");

var store = new ConfigStore(configDir);
var cfg = store.Config;

IVolumeControl volume = dev ? new FakeVolumeControl() : new WindowsVolumeControl();
IMediaController media = dev ? new FakeMediaController()
    : OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041) ? new WindowsMediaController()
    : new NullMediaController();
IVolumeControl micControl = dev ? new FakeVolumeControl() : new WindowsVolumeControl(microphone: true);
IAppMixer mixer = dev ? new FakeAppMixer() : new WindowsAppMixer();
var runner = new ActionRunner(volume, micControl, media, dev);
var monitor = new SystemMonitor(cfg.Name);

var builder = WebApplication.CreateBuilder(args);
builder.Logging.SetMinimumLevel(LogLevel.Warning);
builder.WebHost.UseUrls($"http://0.0.0.0:{cfg.Port}");
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    o.SerializerOptions.PropertyNameCaseInsensitive = true;
});
var app = builder.Build();

// Token check for everything except /api/ping.
app.Use(async (ctx, next) =>
{
    if (ctx.Request.Path.StartsWithSegments("/api") && !ctx.Request.Path.StartsWithSegments("/api/ping"))
    {
        var supplied = ctx.Request.Headers["X-DockBuddy-Token"].ToString();
        var a = Encoding.UTF8.GetBytes(supplied);
        var b = Encoding.UTF8.GetBytes(cfg.Token);
        if (!System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(a, b))
        {
            ctx.Response.StatusCode = 401;
            await ctx.Response.WriteAsJsonAsync(new { error = "Invalid token" });
            return;
        }
    }
    await next();
});

app.MapGet("/api/ping", () => new { name = cfg.Name, version = "1.0", app = "dockbuddy" });
app.MapGet("/api/deck", () => store.Deck);
app.MapGet("/api/icon/{name}", (string name) =>
{
    var path = store.IconFile(name);
    return path == null ? Results.NotFound() : Results.File(path, "image/png");
});
app.MapGet("/api/foreground", () => new { app = DockBuddy.Server.Platform.ForegroundApp.Current() });
app.MapGet("/api/stats", () => monitor.Read());
app.MapGet("/api/nowplaying", async () => await media.GetAsync());

app.MapPost("/api/action", async (ActionRequest req) => Results.Json(await runner.RunAsync(req), ConfigStore.Json));

// Toggle buttons: which ones are currently showing their second state (kept in memory; resets when the server restarts).
var toggles = new System.Collections.Concurrent.ConcurrentDictionary<string, bool>();
app.MapGet("/api/toggles", () => toggles.Where(t => t.Value).ToDictionary(t => t.Key, t => true));

app.MapPost("/api/button/{id}", async (string id) =>
{
    var btn = store.Deck.Pages.SelectMany(p => p.Buttons).FirstOrDefault(b => b.Id == id);
    if (btn == null) return Results.NotFound(new { error = "No such button" });
    var isToggle = btn.Alt != null;
    var on = isToggle && toggles.GetValueOrDefault(id);
    var result = await runner.RunButtonAsync(on ? btn.Alt! : btn);
    if (isToggle)
    {
        if (result.Ok) toggles[id] = !on; // a failed tap leaves the state as it was
        result = result with { On = toggles.GetValueOrDefault(id) };
    }
    return Results.Json(result, ConfigStore.Json);
});

app.MapGet("/api/volume", () => runner.GetVolume());
app.MapPost("/api/volume", (VolumeRequest req) =>
{
    if (req.Level.HasValue) runner.Volume(req.Level.Value.ToString());
    if (req.Mute.HasValue)
    {
        var m = req.Mute.Value;
        if (m.ValueKind == JsonValueKind.True) runner.Volume("mute");
        else if (m.ValueKind == JsonValueKind.False) runner.Volume("unmute");
        else if (m.ValueKind == JsonValueKind.String && m.GetString() == "toggle") runner.Volume("toggle-mute");
    }
    return runner.GetVolume();
});

// Remote update: phones ask which app version this PC carries and download it.
app.MapGet("/api/app", () => AppPackage.Describe(store.ConfigDir) is { } info ? Results.Json(info, ConfigStore.Json) : Results.NotFound(new { error = "No app package on this PC" }));
app.MapGet("/api/app/apk", () => AppPackage.Open(store.ConfigDir) is { } apk
    ? Results.File(apk, "application/vnd.android.package-archive", "DockBuddy.apk")
    : Results.NotFound(new { error = "No app package on this PC" }));

app.MapGet("/api/wake", () => new { adapters = DockBuddy.Server.Network.WakeInfo.Adapters() });
app.MapGet("/api/mic", () => runner.GetMic());
app.MapPost("/api/mic", (MicRequest req) =>
{
    var toggle = req.Mute is { ValueKind: JsonValueKind.String } t && t.GetString() == "toggle";
    bool? mute = req.Mute is { ValueKind: JsonValueKind.True or JsonValueKind.False } m ? m.GetBoolean() : null;
    return runner.SetMic(mute, toggle, req.Level);
});

// A failing Core Audio call is reported with its message (the phone shows it) instead of a bare 500.
app.MapGet("/api/mixer", () =>
{
    try { return Results.Json(mixer.List(), ConfigStore.Json); }
    catch (Exception ex) { return Results.Json(new { error = ex.GetType().Name + ": " + ex.Message }, statusCode: 500); }
});
app.MapPost("/api/mixer", (MixerRequest req) =>
{
    try
    {
        if (string.IsNullOrWhiteSpace(req.Name) || !mixer.Set(req.Name, req.Level, req.Mute))
            return Results.NotFound(new { error = "No such app" });
        return Results.Json(mixer.List(), ConfigStore.Json);
    }
    catch (Exception ex) { return Results.Json(new { error = ex.GetType().Name + ": " + ex.Message }, statusCode: 500); }
});

// UDP discovery: reply to "DOCKBUDDY_DISCOVER" with the server's name and port.
_ = Task.Run(async () =>
{
    try
    {
        using var udp = new UdpClient(new IPEndPoint(IPAddress.Any, cfg.DiscoveryPort)) { EnableBroadcast = true };
        while (true)
        {
            var r = await udp.ReceiveAsync();
            if (Encoding.UTF8.GetString(r.Buffer).Trim() != "DOCKBUDDY_DISCOVER") continue;
            var reply = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { app = "dockbuddy", name = cfg.Name, port = cfg.Port }));
            await udp.SendAsync(reply, reply.Length, r.RemoteEndPoint);
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Discovery disabled: {ex.Message}");
    }
});

var addresses = Dns.GetHostAddresses(Dns.GetHostName())
    .Where(i => i.AddressFamily == AddressFamily.InterNetwork)
    .Select(i => i.ToString()).ToArray();

if (!gui)
{
    Console.WriteLine("=== DockBuddy server ===");
    Console.WriteLine($"Name : {cfg.Name}{(dev ? "  [DEV MODE - actions are stubbed]" : "")}");
    Console.WriteLine($"Port : {cfg.Port}");
    Console.WriteLine($"Token: {cfg.Token}");
    foreach (var ip in addresses) Console.WriteLine($"Addr : {ip}");
    Console.WriteLine("Enter the address and token in the DockBuddy app (or let it auto-discover this PC).");
    app.Run();
    return;
}

#if !HEADLESS
await DockBuddy.Server.Gui.GuiLauncher.RunHostWithTrayAsync(app, new DockBuddy.Server.Gui.ServerInfo(cfg.Name, cfg.Port, cfg.Token, addresses, store), startHidden);
#endif

public record MixerRequest(string? Name, int? Level, bool? Mute);
public record MicRequest(int? Level, JsonElement? Mute);
public record VolumeRequest(int? Level, JsonElement? Mute);
