using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.Versioning;

namespace DockBuddy.Server.Gui;

/// <summary>Imports user-chosen images as custom button icons (scaled to fit 192x192, kept as PNG).</summary>
[SupportedOSPlatform("windows")]
public static class IconStore
{
    public const string Prefix = "img:";
    private const int Size = 192;

    public static bool IsCustom(string icon) => icon.StartsWith(Prefix, StringComparison.Ordinal);

    /// <summary>Copies the image into the icons folder and returns the icon value to store on the button.</summary>
    public static string Import(ConfigStore store, string sourcePath)
    {
        using var src = Image.FromFile(sourcePath);
        var scale = Math.Min((float)Size / src.Width, (float)Size / src.Height);
        var w = Math.Max(1, (int)Math.Round(src.Width * scale));
        var h = Math.Max(1, (int)Math.Round(src.Height * scale));
        using var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.DrawImage(src, 0, 0, w, h);
        }
        Directory.CreateDirectory(store.IconsDir);
        var name = Guid.NewGuid().ToString("N")[..12];
        bmp.Save(Path.Combine(store.IconsDir, name + ".png"), ImageFormat.Png);
        return Prefix + name;
    }

    public static void Delete(ConfigStore store, string icon)
    {
        if (!IsCustom(icon)) return;
        var path = store.IconFile(icon[Prefix.Length..]);
        try { if (path != null) File.Delete(path); } catch { /* still in use: leave it */ }
    }

    /// <summary>Deletes the custom images of a button and of its toggle's second state.</summary>
    public static void DeleteAll(ConfigStore store, DeckButton button)
    {
        Delete(store, button.Icon);
        if (button.Alt != null) Delete(store, button.Alt.Icon);
    }
}
