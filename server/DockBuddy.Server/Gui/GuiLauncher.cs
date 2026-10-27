using System.Runtime.Versioning;
using Microsoft.Win32;

namespace DockBuddy.Server.Gui;

public record ServerInfo(string Name, int Port, string Token, string[] Addresses, ConfigStore Store);

/// <summary>Starts the tray window. Everything that touches WinForms lives under Gui/ so a headless build can drop it.</summary>
public static class GuiLauncher
{
    private static Mutex? _mutex;
    private static EventWaitHandle? _showEvent;

    /// <summary>False if another copy is already running; that copy is asked to show its window.</summary>
    public static bool TryAcquireSingleInstance()
    {
        _mutex = new Mutex(true, "DockBuddy.Server.SingleInstance", out var created);
        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "DockBuddy.Server.ShowWindow");
        if (created) return true;
        _showEvent.Set();
        return false;
    }

    /// <summary>Starts the web server, shows the tray window, and stops the server when the user quits.</summary>
    [SupportedOSPlatform("windows")]
    public static async Task RunHostWithTrayAsync(WebApplication app, ServerInfo info, bool startHidden)
    {
        try
        {
            await app.StartAsync();
        }
        catch (Exception ex)
        {
            ShowError($"DockBuddy could not start its server on port {info.Port}.\n\n{ex.Message}\n\nIs another program using that port?");
            return;
        }
        Run(info, startHidden);
        await app.StopAsync();
    }

    [SupportedOSPlatform("windows")]
    public static void ShowError(string message)
    {
        var t = new Thread(() => MessageBox.Show(message, "DockBuddy Server", MessageBoxButtons.OK, MessageBoxIcon.Error));
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        t.Join();
    }

    /// <summary>Runs the window and tray icon on an STA thread and blocks until the user quits.</summary>
    [SupportedOSPlatform("windows")]
    public static void Run(ServerInfo info, bool startHidden)
    {
        var thread = new Thread(() =>
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            var form = new MainForm(info, startHidden);

            // A second launch signals us to come to the front.
            var listener = new Thread(() =>
            {
                while (_showEvent!.WaitOne())
                {
                    try { form.BeginInvoke(new Action(form.ShowFromTray)); } catch { return; }
                }
            }) { IsBackground = true };
            listener.Start();

            Application.Run(form);
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }

    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunName = "DockBuddy";

    [SupportedOSPlatform("windows")]
    public static bool IsAutostart()
    {
        using var k = Registry.CurrentUser.OpenSubKey(RunKey);
        return k?.GetValue(RunName) != null;
    }

    [SupportedOSPlatform("windows")]
    public static void SetAutostart(bool on)
    {
        using var k = Registry.CurrentUser.CreateSubKey(RunKey);
        if (on) k.SetValue(RunName, $"\"{Environment.ProcessPath}\" --minimized");
        else k.DeleteValue(RunName, false);
    }
}
