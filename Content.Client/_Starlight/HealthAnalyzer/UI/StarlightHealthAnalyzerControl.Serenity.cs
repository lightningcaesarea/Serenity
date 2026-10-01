using Content.Shared._Serenity.Medical.Analyzer;
using Content.Shared._Starlight.Medical.HealthAnalyzer;
using Content.Shared.MedicalScanner;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;

namespace Content.Client._Starlight.HealthAnalyzer.UI;

// Serenity: the infection alert every analyzer shows, and the wound section only advanced analyzers show.
public sealed partial class StarlightHealthAnalyzerControl
{
    private static readonly Color InfectionColor = Color.FromHex("#B9C24A");

    private void AddSerenityAbnormalities(HealthAnalyzerUiState state, List<HealthAnalyzerAbnormalityData> abnormalities)
    {
        if (state.InfectionDetected == true)
        {
            abnormalities.Add(new HealthAnalyzerAbnormalityData
            {
                Description = Loc.GetString("health-analyzer-infection-detected"),
                Color = InfectionColor,
            });
        }
    }

    private void DrawWounds(HealthAnalyzerWoundReadout? readout)
    {
        WoundsContainer.RemoveAllChildren();

        if (readout == null || readout.Wounds.Count == 0)
        {
            WoundsHeader.Visible = false;
            WoundsContainer.Visible = false;
            return;
        }

        WoundsHeader.Visible = true;
        WoundsContainer.Visible = true;

        foreach (var wound in readout.Wounds)
        {
            var color = wound.Tier switch
            {
                >= 3 => Color.FromHex("#D83030"),
                2 => Color.FromHex("#E6873C"),
                _ => Color.FromHex("#DDDD77"),
            };

            var row = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                HorizontalExpand = true,
                SeparationOverride = 6,
            };

            row.AddChild(new PanelContainer
            {
                MinWidth = 10,
                MaxWidth = 10,
                MinHeight = 16,
                VerticalAlignment = VAlignment.Stretch,
                PanelOverride = new StyleBoxFlat(color),
            });

            var where = wound.Location != null ? $" ({Loc.GetString(wound.Location.LocKey)})" : string.Empty;
            row.AddChild(new Label
            {
                Text = Loc.GetString(wound.LocKey) + where,
                HorizontalExpand = true,
                HorizontalAlignment = HAlignment.Left,
            });

            row.AddChild(new Label
            {
                Text = Loc.GetString("health-analyzer-wound-tier", ("tier", wound.Tier)),
                HorizontalAlignment = HAlignment.Right,
                StyleClasses = { "FontSmall" },
                FontColorOverride = Color.FromHex("#C7CED7"),
            });

            WoundsContainer.AddChild(row);
        }

        if (readout.InfectionSuppressed)
        {
            WoundsContainer.AddChild(new Label
            {
                Text = Loc.GetString("health-analyzer-infection-suppressed"),
                StyleClasses = { "FontSmall" },
                FontColorOverride = Color.FromHex("#C7CED7"),
                Margin = new Thickness(0, 3, 0, 0),
            });
        }
    }
}
