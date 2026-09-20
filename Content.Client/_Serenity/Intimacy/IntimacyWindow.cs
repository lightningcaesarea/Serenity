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
/// The intimacy window: your stats and the target's on top, one tab of acts per category,
/// and a climax button at the bottom.
/// </summary>
public sealed partial class IntimacyWindow : DefaultWindow
{
    [Dependency] private IPrototypeManager _proto = default!;

    public event Action<string>? OnActPressed;
    public event Action? OnClimaxPressed;

    private readonly Label _targetLabel;
    private readonly BoxContainer _actorBars;
    private readonly BoxContainer _targetBars;
    private readonly TabContainer _tabs;
    private readonly Button _climaxButton;

    private readonly Dictionary<string, ProgressBar> _actorBarByStat = new();
    private readonly Dictionary<string, ProgressBar> _targetBarByStat = new();
    private readonly Dictionary<string, Button> _actButtons = new();

    public IntimacyWindow()
    {
        IoCManager.InjectDependencies(this);

        Title = Loc.GetString("intimacy-window-title");
        SetSize = new Vector2(520, 560);
        MinSize = new Vector2(420, 400);

        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        Contents.AddChild(root);

        _targetLabel = new Label { StyleClasses = { "LabelHeading" }, Margin = new Thickness(0, 0, 0, 6) };
        root.AddChild(_targetLabel);

        var stats = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, HorizontalExpand = true };
        root.AddChild(stats);

        _actorBars = BuildStatColumn(Loc.GetString("intimacy-column-you"), _actorBarByStat);
        _targetBars = BuildStatColumn(Loc.GetString("intimacy-column-them"), _targetBarByStat);
        stats.AddChild(_actorBars);
        stats.AddChild(_targetBars);

        _tabs = new TabContainer { VerticalExpand = true, Margin = new Thickness(0, 8, 0, 8) };
        root.AddChild(_tabs);
        BuildTabs();

        _climaxButton = new Button { Text = Loc.GetString("intimacy-climax-button"), Disabled = true };
        _climaxButton.OnPressed += _ => OnClimaxPressed?.Invoke();
        root.AddChild(_climaxButton);
    }

    private BoxContainer BuildStatColumn(string heading, Dictionary<string, ProgressBar> bars)
    {
        var column = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            Margin = new Thickness(4, 0),
        };
        column.AddChild(new Label { Text = heading });

        foreach (var stat in _proto.EnumeratePrototypes<IntimacyStatPrototype>().OrderBy(s => s.Order))
        {
            var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, HorizontalExpand = true };
            row.AddChild(new Label { Text = Loc.GetString(stat.Name), MinWidth = 70 });

            var bar = new ProgressBar
            {
                MinValue = 0,
                MaxValue = stat.Max,
                Value = 0,
                HorizontalExpand = true,
                MinHeight = 14,
                ForegroundStyleBoxOverride = new StyleBoxFlat { BackgroundColor = stat.Color },
            };
            row.AddChild(bar);
            bars[stat.ID] = bar;
            column.AddChild(row);
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
            var scroll = new ScrollContainer { HorizontalExpand = true, VerticalExpand = true };
            var grid = new GridContainer { Columns = 3, HorizontalExpand = true };
            scroll.AddChild(grid);

            foreach (var act in acts.Where(a => a.Category == category.ID).OrderBy(a => Loc.GetString(a.Name)))
            {
                var button = new Button
                {
                    Text = Loc.GetString(act.Name),
                    HorizontalExpand = true,
                    Disabled = true,
                    Modulate = category.Color,
                };
                var id = act.ID;
                button.OnPressed += _ => OnActPressed?.Invoke(id);
                grid.AddChild(button);
                _actButtons[id] = button;
            }

            _tabs.AddChild(scroll);
            _tabs.SetTabTitle(index++, Loc.GetString(category.Name));
        }
    }

    public void Update(IntimacyUiState state)
    {
        _targetLabel.Text = Loc.GetString("intimacy-window-target", ("name", state.TargetName));

        foreach (var (stat, bar) in _actorBarByStat)
            bar.Value = state.ActorStats.GetValueOrDefault(stat);

        foreach (var (stat, bar) in _targetBarByStat)
            bar.Value = state.TargetStats.GetValueOrDefault(stat);

        foreach (var (id, button) in _actButtons)
            button.Disabled = !state.Available.Contains(id);

        _climaxButton.Disabled = !state.CanClimax;
    }
}
