using System.Linq;
using System.Numerics;
using Content.Shared._Serenity.Intimacy;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;

namespace Content.Client._Serenity.Intimacy;

/// <summary>
/// The intimacy window: a soft header, stat bars with their numbers laid right over the fill, one
/// searchable list of acts per category tab (instead of a button grid), and a climax button at the
/// bottom. Own layout.
/// </summary>
public sealed partial class IntimacyWindow : DefaultWindow
{
    [Dependency] private IPrototypeManager _proto = default!;

    public event Action<string>? OnActPressed;
    public event Action? OnClimaxPressed;

    private static readonly Color AccentColor = Color.FromHex("#c97bb0");
    private static readonly Color DimTextColor = Color.FromHex("#9a9a9a");

    private readonly Label _targetLabel;
    private readonly TabContainer _tabs;
    private readonly Button _climaxButton;

    private readonly Dictionary<string, ProgressBar> _actorBarByStat = new();
    private readonly Dictionary<string, Label> _actorValueByStat = new();
    private readonly Dictionary<string, ProgressBar> _targetBarByStat = new();
    private readonly Dictionary<string, Label> _targetValueByStat = new();
    private readonly Dictionary<string, Button> _actButtons = new();

    /// <summary>
    /// Every act row and its lowercased name, so a tab's search box can show/hide rows without
    /// touching which acts are actually available.
    /// </summary>
    private readonly List<(Control Row, string LowerName)> _actRows = new();

    public IntimacyWindow()
    {
        IoCManager.InjectDependencies(this);

        Title = Loc.GetString("intimacy-window-title");
        SetSize = new Vector2(560, 620);
        MinSize = new Vector2(460, 460);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(6, 6),
        };
        Contents.AddChild(root);

        var header = new PanelContainer();
        header.AddStyleClass("BackgroundPanel");
        _targetLabel = new Label { StyleClasses = { "LabelHeading" }, Margin = new Thickness(8, 4) };
        header.AddChild(_targetLabel);
        root.AddChild(header);

        root.AddChild(MakeAccentRule(new Thickness(0, 6, 0, 8)));

        var stats = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            SeparationOverride = 10,
        };
        root.AddChild(stats);

        stats.AddChild(BuildStatColumn(Loc.GetString("intimacy-column-you"), _actorBarByStat, _actorValueByStat));
        stats.AddChild(BuildStatColumn(Loc.GetString("intimacy-column-them"), _targetBarByStat, _targetValueByStat));

        _tabs = new TabContainer { VerticalExpand = true, Margin = new Thickness(0, 10, 0, 8) };
        root.AddChild(_tabs);
        BuildTabs();

        _climaxButton = new Button
        {
            Text = Loc.GetString("intimacy-climax-button"),
            Disabled = true,
            MinHeight = 34,
            ModulateSelfOverride = AccentColor,
        };
        _climaxButton.OnPressed += _ => OnClimaxPressed?.Invoke();
        root.AddChild(_climaxButton);
    }

    private static Control MakeAccentRule(Thickness margin)
    {
        return new PanelContainer
        {
            MinHeight = 2,
            HorizontalExpand = true,
            Margin = margin,
            PanelOverride = new StyleBoxFlat { BackgroundColor = AccentColor.WithAlpha(0.55f) },
        };
    }

    /// <summary>
    /// A short glyph per stat so the bars read at a glance. Falls back to a plain dot for any
    /// stat content adds later that this doesn't recognise.
    /// </summary>
    private static string GlyphFor(string statId) => statId switch
    {
        "Pleasure" => "♥",
        "Arousal" => "✧",
        "Pain" => "⚠",
        _ => "●",
    };

    private BoxContainer BuildStatColumn(
        string heading,
        Dictionary<string, ProgressBar> bars,
        Dictionary<string, Label> values)
    {
        var column = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            SeparationOverride = 4,
        };
        column.AddChild(new Label { Text = heading, StyleClasses = { "LabelSubText" } });

        foreach (var stat in _proto.EnumeratePrototypes<IntimacyStatPrototype>().OrderBy(s => s.Order))
        {
            var bar = new ProgressBar
            {
                MinValue = 0,
                MaxValue = stat.Max,
                Value = 0,
                HorizontalExpand = true,
                MinHeight = 22,
                ForegroundStyleBoxOverride = new StyleBoxFlat { BackgroundColor = stat.Color.WithAlpha(0.65f) },
                BackgroundStyleBoxOverride = new StyleBoxFlat
                {
                    BackgroundColor = Color.FromHex("#1b1b1b"),
                    BorderColor = stat.Color.WithAlpha(0.8f),
                    BorderThickness = new Thickness(1),
                },
            };

            var nameLabel = new Label
            {
                Text = $"{GlyphFor(stat.ID)} {Loc.GetString(stat.Name)}",
                HorizontalAlignment = HAlignment.Left,
                Margin = new Thickness(6, 0, 0, 0),
            };
            var valueLabel = new Label
            {
                Text = "0",
                HorizontalAlignment = HAlignment.Right,
                Margin = new Thickness(0, 0, 6, 0),
                FontColorOverride = DimTextColor,
            };
            bar.AddChild(nameLabel);
            bar.AddChild(valueLabel);

            bars[stat.ID] = bar;
            values[stat.ID] = valueLabel;
            column.AddChild(bar);
        }

        return column;
    }

    private void BuildTabs()
    {
        var categories = _proto.EnumeratePrototypes<IntimacyCategoryPrototype>().OrderBy(c => c.Order).ToList();
        var acts = _proto.EnumeratePrototypes<IntimacyActPrototype>().ToList();

        var index = 0;
        foreach (var category in categories)
        {
            var categoryActs = acts.Where(a => a.Category == category.ID)
                .OrderBy(a => Loc.GetString(a.Name))
                .ToList();

            var tabRoot = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, VerticalExpand = true };

            var rowsBox = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Vertical,
                HorizontalExpand = true,
                SeparationOverride = 2,
            };

            if (categoryActs.Count > 4)
            {
                var search = new LineEdit
                {
                    PlaceHolder = Loc.GetString("intimacy-search-placeholder"),
                    HorizontalExpand = true,
                    Margin = new Thickness(0, 0, 0, 4),
                };
                search.OnTextChanged += args => FilterRows(rowsBox, args.Text);
                tabRoot.AddChild(search);
            }

            var scroll = new ScrollContainer { HorizontalExpand = true, VerticalExpand = true, HScrollEnabled = false };
            scroll.AddChild(rowsBox);
            tabRoot.AddChild(scroll);

            foreach (var act in categoryActs)
                rowsBox.AddChild(BuildActRow(act, category.Color));

            _tabs.AddChild(tabRoot);
            _tabs.SetTabTitle(index++, Loc.GetString(category.Name));
        }
    }

    private Control BuildActRow(IntimacyActPrototype act, Color categoryColor)
    {
        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
        };

        var accent = new PanelContainer
        {
            MinWidth = 4,
            VerticalExpand = true,
            PanelOverride = new StyleBoxFlat { BackgroundColor = categoryColor.WithAlpha(0.75f) },
            Margin = new Thickness(0, 1, 4, 1),
        };
        row.AddChild(accent);

        var button = new Button
        {
            Text = Loc.GetString(act.Name),
            HorizontalExpand = true,
            Disabled = true,
        };
        button.Label.HorizontalAlignment = HAlignment.Left;
        var id = act.ID;
        button.OnPressed += _ => OnActPressed?.Invoke(id);
        row.AddChild(button);

        _actButtons[id] = button;
        _actRows.Add((row, Loc.GetString(act.Name).ToLowerInvariant()));

        return row;
    }

    private void FilterRows(BoxContainer rowsBox, string query)
    {
        query = query.Trim().ToLowerInvariant();

        foreach (var child in rowsBox.Children)
        {
            var match = _actRows.FirstOrDefault(r => ReferenceEquals(r.Row, child));
            if (match.Row == null)
                continue;

            child.Visible = query.Length == 0 || match.LowerName.Contains(query);
        }
    }

    public void Update(IntimacyUiState state)
    {
        _targetLabel.Text = Loc.GetString("intimacy-window-target", ("name", state.TargetName));

        foreach (var (stat, bar) in _actorBarByStat)
        {
            var value = state.ActorStats.GetValueOrDefault(stat);
            bar.Value = value;
            _actorValueByStat[stat].Text = $"{value:0}";
        }

        foreach (var (stat, bar) in _targetBarByStat)
        {
            var value = state.TargetStats.GetValueOrDefault(stat);
            bar.Value = value;
            _targetValueByStat[stat].Text = $"{value:0}";
        }

        foreach (var (id, button) in _actButtons)
            button.Disabled = !state.Available.Contains(id);

        _climaxButton.Disabled = !state.CanClimax;
    }
}
