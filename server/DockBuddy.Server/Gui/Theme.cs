using System.Drawing;
using System.Runtime.Versioning;

namespace DockBuddy.Server.Gui;

/// <summary>Colors and small styling helpers shared by the server's windows.</summary>
[SupportedOSPlatform("windows")]
internal static class Theme
{
    public static readonly Color Back = Color.FromArgb(16, 16, 16);
    public static readonly Color Field = Color.FromArgb(34, 34, 34);
    public static readonly Color Muted = Color.FromArgb(160, 160, 160);
    public static readonly Color Border = Color.FromArgb(80, 80, 80);
    public static readonly Color ButtonBack = Color.FromArgb(48, 48, 48);

    public static void StyleButton(Button b)
    {
        b.BackColor = ButtonBack;
        b.FlatStyle = FlatStyle.Flat;
        b.FlatAppearance.BorderColor = Border;
        b.ForeColor = Color.White;
        b.UseVisualStyleBackColor = false;
    }

    public static void StyleField(Control c)
    {
        c.BackColor = Field;
        c.ForeColor = Color.White;
        if (c is ComboBox cb) cb.FlatStyle = FlatStyle.Flat;
        if (c is TextBox tb) tb.BorderStyle = BorderStyle.FixedSingle;
    }

    public static Color Parse(string? hex, Color fallback)
    {
        try { return ColorTranslator.FromHtml(hex ?? ""); } catch { return fallback; }
    }

    public static string ToHex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";
}
