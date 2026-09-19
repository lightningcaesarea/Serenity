using System.Linq;
using System.Numerics;
using Content.Shared._Serenity.Consent;
using Content.Shared._Serenity.Kinks;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;
using Content.Client.Lobby;

namespace Content.Client._Serenity.Kinks;

public sealed class KinkListWindow : DefaultWindow
{
    private readonly IClientPreferencesManager _prefs;
    private readonly IPrototypeManager _proto;

    private readonly Dictionary<string, KinkPreferenceLevel> _current = new();
    private readonly Dictionary<string, bool> _consent = new();
    private readonly LineEdit _search;
    private readonly BoxContainer _categoriesBox;

    private static readonly Color ColorFavorite = Color.FromHex("#d4a017");
    private static readonly Color ColorYes = Color.FromHex("#4caf50");
    private static readonly Color ColorMaybe = Color.FromHex("#2196f3");
    private static readonly Color ColorNo = Color.FromHex("#f44336");
    private static readonly Color ColorUnset = Color.FromHex("#555555");

    public KinkListWindow(IClientPreferencesManager prefs, IPrototypeManager proto)
    {
        _prefs = prefs;
        _proto = proto;

        Title = Loc.GetString("kink-list-window-title");
        SetSize = new Vector2(700, 550);
        MinSize = new Vector2(500, 400);

        if (_prefs.Preferences != null)
        {
            foreach (var (k, v) in _prefs.Preferences.KinkPreferences)
                _current[k] = v;
            foreach (var (k, v) in _prefs.Preferences.ConsentToggles)
                _consent[k] = v;
        }

        var tabs = new TabContainer();
        Contents.AddChild(tabs);

        var consentTab = BuildConsentTab();
        tabs.AddChild(consentTab);
        tabs.SetTabTitle(0, Loc.GetString("consent-tab-title"));

        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        tabs.AddChild(root);
        tabs.SetTabTitle(1, Loc.GetString("kink-tab-title"));

        // Search bar
        _search = new LineEdit
        {
            PlaceHolder = Loc.GetString("kink-list-search-placeholder"),
            HorizontalExpand = true,
            Margin = new Thickness(0, 0, 0, 6),
        };
        _search.OnTextChanged += _ => RebuildCategories();
        root.AddChild(_search);

        // Legend row
        var legend = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            Margin = new Thickness(0, 0, 0, 4),
            SeparationOverride = 6,
        };
        AddLegendItem(legend, ColorFavorite, Loc.GetString("kink-preference-favorite"));
        AddLegendItem(legend, ColorYes, Loc.GetString("kink-preference-yes"));
        AddLegendItem(legend, ColorMaybe, Loc.GetString("kink-preference-maybe"));
        AddLegendItem(legend, ColorNo, Loc.GetString("kink-preference-no"));
        root.AddChild(legend);

        // Scrollable categories
        var scroll = new ScrollContainer
        {
            VerticalExpand = true,
            HScrollEnabled = false,
        };
        _categoriesBox = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            SeparationOverride = 4,
        };
        scroll.AddChild(_categoriesBox);
        root.AddChild(scroll);

        RebuildCategories();
    }

    private Control BuildConsentTab()
    {
        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
        };

        root.AddChild(new Label
        {
            Text = Loc.GetString("consent-tab-blurb"),
            Margin = new Thickness(4, 4, 4, 8),
        });

        var scroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        var box = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            SeparationOverride = 4,
        };
        scroll.AddChild(box);
        root.AddChild(scroll);

        var grouped = new Dictionary<string, List<ConsentTogglePrototype>>();
        foreach (var toggle in _proto.EnumeratePrototypes<ConsentTogglePrototype>())
        {
            if (!grouped.TryGetValue(toggle.Category, out var list))
                grouped[toggle.Category] = list = new List<ConsentTogglePrototype>();
            list.Add(toggle);
        }

        foreach (var pair in grouped.OrderBy(k => k.Key))
        {
            pair.Value.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

            var header = new PanelContainer();
            header.AddStyleClass("BackgroundPanel");
            header.AddChild(new Label
            {
                Text = Loc.GetString($"consent-category-{pair.Key}"),
                StyleClasses = { "LabelHeading" },
                Margin = new Thickness(6, 2),
            });
            box.AddChild(header);

            foreach (var toggle in pair.Value)
                box.AddChild(BuildConsentRow(toggle));
        }

        return root;
    }

    private Control BuildConsentRow(ConsentTogglePrototype toggle)
    {
        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            Margin = new Thickness(4, 2),
        };

        var check = new CheckBox
        {
            Text = toggle.Name,
            Pressed = _consent.TryGetValue(toggle.ID, out var set) ? set : toggle.Default,
            ToolTip = toggle.Description,
        };
        check.OnToggled += args =>
        {
            _consent[toggle.ID] = args.Pressed;
            _prefs.UpdateConsentToggles(new Dictionary<string, bool>(_consent));
        };
        row.AddChild(check);

        row.AddChild(new Label
        {
            Text = toggle.Description,
            FontColorOverride = Color.FromHex("#999999"),
            Margin = new Thickness(22, 0, 0, 0),
        });

        return row;
    }

    private static void AddLegendItem(BoxContainer parent, Color color, string text)
    {
        var dot = new Label
        {
            Text = "●",
            FontColorOverride = color,
            Margin = new Thickness(0, 0, 2, 0),
        };
        parent.AddChild(dot);
        parent.AddChild(new Label { Text = text, Margin = new Thickness(0, 0, 8, 0) });
    }

    private void RebuildCategories()
    {
        _categoriesBox.RemoveAllChildren();

        var query = _search.Text?.ToLowerInvariant() ?? string.Empty;

        var grouped = new Dictionary<string, List<KinkPrototype>>();
        foreach (var kink in _proto.EnumeratePrototypes<KinkPrototype>())
        {
            if (!string.IsNullOrEmpty(query) &&
                !kink.Name.ToLowerInvariant().Contains(query) &&
                !kink.Description.ToLowerInvariant().Contains(query) &&
                !kink.Category.ToLowerInvariant().Contains(query))
                continue;

            if (!grouped.TryGetValue(kink.Category, out var list))
                grouped[kink.Category] = list = new List<KinkPrototype>();
            list.Add(kink);
        }

        foreach (var pair in grouped.OrderBy(k => k.Key))
        {
            pair.Value.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            _categoriesBox.AddChild(BuildCategorySection(pair.Key, pair.Value));
        }
    }

    private Control BuildCategorySection(string category, List<KinkPrototype> kinks)
    {
        var container = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
        };

        var header = new PanelContainer();
        header.AddStyleClass("BackgroundPanel");
        var headerLabel = new Label
        {
            Text = Loc.GetString($"kink-category-{category}"),
            StyleClasses = { "LabelHeading" },
            Margin = new Thickness(6, 2),
        };
        header.AddChild(headerLabel);
        container.AddChild(header);

        var kinkBox = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            SeparationOverride = 1,
        };

        foreach (var kink in kinks)
            kinkBox.AddChild(BuildKinkRow(kink));

        container.AddChild(kinkBox);
        return container;
    }

    private Control BuildKinkRow(KinkPrototype kink)
    {
        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            SeparationOverride = 4,
            Margin = new Thickness(2, 1),
        };

        var nameLabel = new Label
        {
            Text = kink.Name,
            HorizontalExpand = true,
            ClipText = true,
            ToolTip = kink.Description,
        };
        row.AddChild(nameLabel);

        var btnFavorite = MakePrefButton("★", ColorFavorite, KinkPreferenceLevel.Favorite);
        var btnYes = MakePrefButton("✓", ColorYes, KinkPreferenceLevel.Yes);
        var btnMaybe = MakePrefButton("~", ColorMaybe, KinkPreferenceLevel.Maybe);
        var btnNo = MakePrefButton("✗", ColorNo, KinkPreferenceLevel.No);

        var buttons = new[] { btnFavorite, btnYes, btnMaybe, btnNo };
        RefreshButtons(kink.ID, buttons);

        void OnClick(KinkPreferenceLevel level)
        {
            if (_current.TryGetValue(kink.ID, out var existing) && existing == level)
                _current.Remove(kink.ID);
            else
                _current[kink.ID] = level;
            RefreshButtons(kink.ID, buttons);
            SavePreferences();
        }

        btnFavorite.OnPressed += _ => OnClick(KinkPreferenceLevel.Favorite);
        btnYes.OnPressed += _ => OnClick(KinkPreferenceLevel.Yes);
        btnMaybe.OnPressed += _ => OnClick(KinkPreferenceLevel.Maybe);
        btnNo.OnPressed += _ => OnClick(KinkPreferenceLevel.No);

        row.AddChild(btnFavorite);
        row.AddChild(btnYes);
        row.AddChild(btnMaybe);
        row.AddChild(btnNo);

        return row;
    }

    private static Button MakePrefButton(string text, Color color, KinkPreferenceLevel level)
    {
        return new Button
        {
            Text = text,
            MinSize = new Vector2(28, 22),
            MaxSize = new Vector2(28, 22),
            // Sit flush with the top of the row rather than centring on it — centred, the
            // glyphs drift below the kink name they belong to.
            VerticalAlignment = VAlignment.Top,
            Margin = new Thickness(0, -6, 0, 0),
            ToolTip = Loc.GetString($"kink-preference-{level.ToString().ToLowerInvariant()}"),
            ModulateSelfOverride = color,
        };
    }

    private void RefreshButtons(string kinkId, Button[] buttons)
    {
        var hasActive = _current.TryGetValue(kinkId, out var active);
        var colors = new[] { ColorFavorite, ColorYes, ColorMaybe, ColorNo };

        for (var i = 0; i < buttons.Length; i++)
        {
            var level = (KinkPreferenceLevel)i;
            buttons[i].ModulateSelfOverride = (hasActive && active == level) ? colors[i] : ColorUnset;
        }
    }

    private void SavePreferences()
    {
        _prefs.UpdateKinkPreferences(new Dictionary<string, KinkPreferenceLevel>(_current));
    }
}
