using System.Drawing;
using System.Runtime.Versioning;

namespace DockBuddy.Server.Gui;

/// <summary>Edits the steps of a macro button: add, edit, remove and reorder.</summary>
[SupportedOSPlatform("windows")]
public sealed partial class MacroForm : Form
{
    public List<MacroStep> Steps { get; }

    public MacroForm(List<MacroStep> steps)
    {
        Steps = steps.Select(s => new MacroStep { Type = s.Type, Value = s.Value, Args = s.Args, DelayMs = s.DelayMs }).ToList();
        InitializeComponent();
        Reload(Steps.Count > 0 ? 0 : -1);
    }

    private static string Describe(int index, MacroStep s)
        => $"{index + 1}.  {s.Type}: {s.Value}" + (s.DelayMs > 0 ? $"    (then wait {s.DelayMs} ms)" : "");

    private void Reload(int select)
    {
        lstSteps.Items.Clear();
        for (var i = 0; i < Steps.Count; i++) lstSteps.Items.Add(Describe(i, Steps[i]));
        if (select >= 0 && select < Steps.Count) lstSteps.SelectedIndex = select;
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        var i = lstSteps.SelectedIndex;
        btnEdit.Enabled = btnRemove.Enabled = i >= 0;
        btnUp.Enabled = i > 0;
        btnDown.Enabled = i >= 0 && i < Steps.Count - 1;
    }

    private void AddStep()
    {
        var step = new MacroStep { Type = "keys" };
        using var f = new StepForm(step);
        if (f.ShowDialog(this) != DialogResult.OK) return;
        Steps.Add(step);
        Reload(Steps.Count - 1);
    }

    private void EditStep()
    {
        var i = lstSteps.SelectedIndex;
        if (i < 0) return;
        using var f = new StepForm(Steps[i]);
        if (f.ShowDialog(this) == DialogResult.OK) Reload(i);
    }

    private void RemoveStep()
    {
        var i = lstSteps.SelectedIndex;
        if (i < 0) return;
        Steps.RemoveAt(i);
        Reload(Math.Min(i, Steps.Count - 1));
    }

    private void MoveStep(int delta)
    {
        var i = lstSteps.SelectedIndex;
        var j = i + delta;
        if (i < 0 || j < 0 || j >= Steps.Count) return;
        (Steps[i], Steps[j]) = (Steps[j], Steps[i]);
        Reload(j);
    }
}
