using Content.Shared._Serenity.Medical.Analyzer;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.Client.HealthAnalyzer.UI;

// Serenity: the wound and infection section, shown only by advanced analyzers.
public sealed partial class HealthAnalyzerControl
{
    private void DrawWounds(HealthAnalyzerWoundReadout? readout)
    {
        WoundsContainer.RemoveAllChildren();

        if (readout == null || readout.Wounds.Count == 0)
        {
            WoundsDivider.Visible = false;
            WoundsContainer.Visible = false;
            return;
        }

        WoundsDivider.Visible = true;
        WoundsContainer.Visible = true;

        WoundsContainer.AddChild(new Label
        {
            Text = Loc.GetString("health-analyzer-wounds-title"),
            StyleClasses = { "LabelSubText" },
        });

        foreach (var wound in readout.Wounds)
        {
            var label = new RichTextLabel { Margin = new Thickness(0, 1) };
            var color = wound.Tier switch
            {
                >= 3 => "#d83030",
                2 => "#e6873c",
                _ => "#dddd77",
            };
            var where = wound.Location != null ? $" ({Loc.GetString(wound.Location.LocKey)})" : string.Empty;
            label.SetMessage(FormattedMessage.FromMarkupOrThrow(
                $"[color={color}]{Loc.GetString(wound.LocKey)}{where}[/color]  {Loc.GetString("health-analyzer-wound-tier", ("tier", wound.Tier))}"));
            WoundsContainer.AddChild(label);
        }

        if (readout.InfectionSuppressed)
        {
            var note = new Label { Text = Loc.GetString("health-analyzer-infection-suppressed"), Margin = new Thickness(0, 3, 0, 0) };
            note.StyleClasses.Add("LabelSubText");
            WoundsContainer.AddChild(note);
        }
    }
}
