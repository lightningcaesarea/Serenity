using System.Numerics;
using Content.Client.Stylesheets;
using Content.Shared._Serenity.Chemistry;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client._Serenity.Chemistry;

/// <summary>
/// The ChemMaster's Packaging tab: a beaker list and a packaging buffer list (separate from the main
/// buffer), a data-driven grid of packaging types, and a print row. Built in code so the same panel can
/// be dropped into both the modern and classic layouts.
/// </summary>
public sealed partial class ChemMasterPackagingPanel : BoxContainer
{
    [Dependency] private IPrototypeManager _prototype = default!;

    public event Action<ReagentId, FixedPoint2, bool>? OnTransfer;
    public event Action? OnToggleDiscard;
    public event Action<int, EntProtoId>? OnSelect;
    public event Action<int, bool>? OnUseContainer;
    public event Action<int>? OnSetAmount;
    public event Action<string>? OnPrint;
    public event Action? OnEjectOutput;

    private static readonly Color PanelColor = Color.FromHex("#1B1B1E");
    private static readonly Color RowColor1 = Color.FromHex("#1B1B1E");
    private static readonly Color RowColor2 = Color.FromHex("#202025");

    private readonly Label _beakerVolume = new() { StyleClasses = { StyleClass.LabelWeak } };
    private readonly Label _bufferVolume = new() { StyleClasses = { StyleClass.LabelWeak } };
    private readonly BoxContainer _beakerList = new() { Orientation = LayoutOrientation.Vertical, HorizontalExpand = true };
    private readonly BoxContainer _bufferList = new() { Orientation = LayoutOrientation.Vertical, HorizontalExpand = true };
    private readonly Button _modeButton = new() { MinSize = new Vector2(150, 0) };
    private readonly BoxContainer _sections = new() { Orientation = LayoutOrientation.Vertical, HorizontalExpand = true };
    private readonly SpinBox _amount = new() { MinSize = new Vector2(120, 0) };
    private readonly LineEdit _name = new() { HorizontalExpand = true };
    private readonly Button _printButton = new();
    private readonly Label _outputLabel = new() { StyleClasses = { StyleClass.LabelWeak } };
    private readonly Button _ejectOutput = new();

    private readonly List<(int Section, EntProtoId Proto, Button Button)> _packagingButtons = new();
    private readonly List<Button> _containerButtons = new();
    private int _builtSectionCount = -1;
    private int _nameMaxLength = 42;

    public ChemMasterPackagingPanel()
    {
        IoCManager.InjectDependencies(this);

        Orientation = LayoutOrientation.Vertical;
        HorizontalExpand = true;
        VerticalExpand = true;
        Margin = new Thickness(5);
        SeparationOverride = 8;

        var lists = new BoxContainer { Orientation = LayoutOrientation.Horizontal, SeparationOverride = 8, MinSize = new Vector2(0, 170), VerticalExpand = true };

        _modeButton.OnPressed += _ => OnToggleDiscard?.Invoke();
        lists.AddChild(MakeListColumn(Loc.GetString("chem-master-packaging-beaker"), _beakerVolume, null, _beakerList));
        lists.AddChild(MakeListColumn(Loc.GetString("chem-master-packaging-buffer"), _bufferVolume, _modeButton, _bufferList));
        AddChild(lists);

        AddChild(new PanelContainer
        {
            PanelOverride = new StyleBoxFlat(PanelColor),
            VerticalExpand = true,
            MinSize = new Vector2(0, 150),
            Children =
            {
                new ScrollContainer
                {
                    HorizontalExpand = true,
                    HScrollEnabled = false,
                    Children = { new BoxContainer { Orientation = LayoutOrientation.Vertical, Margin = new Thickness(4), Children = { _sections } } },
                },
            },
        });

        _amount.IsValid = x => x > 0 && x <= _maxAmount;
        _amount.InitDefaultButtons();
        _amount.ValueChanged += args => OnSetAmount?.Invoke(args.Value);

        _name.IsValid = s => s.Length <= _nameMaxLength;
        _name.PlaceHolder = Loc.GetString("chem-master-packaging-name-placeholder");

        _printButton.Text = Loc.GetString("chem-master-packaging-print");
        _printButton.MinSize = new Vector2(80, 0);
        _printButton.OnPressed += _ => OnPrint?.Invoke(_name.Text);

        AddChild(new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 6,
            Children =
            {
                new Label { Text = Loc.GetString("chem-master-packaging-amount"), StyleClasses = { StyleClass.LabelWeak } },
                _amount,
                new Label { Text = Loc.GetString("chem-master-packaging-name"), StyleClasses = { StyleClass.LabelWeak } },
                _name,
                _printButton,
            },
        });

        _ejectOutput.Text = Loc.GetString("chem-master-packaging-eject");
        _ejectOutput.OnPressed += _ => OnEjectOutput?.Invoke();
        AddChild(new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 6,
            Children =
            {
                new Label { Text = Loc.GetString("chem-master-packaging-output"), StyleClasses = { StyleClass.LabelWeak } },
                _outputLabel,
                new Control { HorizontalExpand = true },
                _ejectOutput,
            },
        });
    }

    private int _maxAmount = 10;

    private static Control MakeListColumn(string title, Label volume, Control? extra, Control list)
    {
        var header = new BoxContainer { Orientation = LayoutOrientation.Horizontal, SeparationOverride = 6 };
        header.AddChild(new Label { Text = title });
        header.AddChild(volume);
        header.AddChild(new Control { HorizontalExpand = true });
        if (extra != null)
            header.AddChild(extra);

        return new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 6,
            Children =
            {
                header,
                new PanelContainer
                {
                    PanelOverride = new StyleBoxFlat(PanelColor),
                    VerticalExpand = true,
                    HorizontalExpand = true,
                    Children = { new ScrollContainer { HorizontalExpand = true, HScrollEnabled = false, Children = { list } } },
                },
            },
        };
    }

    public void Update(ChemMasterBoundUserInterfaceState state, ChemMasterPackagingComponent comp, ChemMasterPackagingSystem packaging)
    {
        _nameMaxLength = comp.NameMaxLength;
        _maxAmount = comp.PackagingMaxAmount;

        // Beaker column.
        _beakerList.RemoveAllChildren();
        if (state.InputContainerInfo is { } beaker)
        {
            _beakerVolume.Text = $"{beaker.CurrentVolume}/{beaker.MaxVolume}";
            var reagents = beaker.Reagents ?? [];
            if (reagents.Count == 0)
                _beakerList.AddChild(new Label { Text = Loc.GetString("chem-master-packaging-beaker-empty") });

            for (var i = 0; i < reagents.Count; i++)
                _beakerList.AddChild(MakeReagentRow(reagents[i], i, comp, fromBeaker: true));
        }
        else
        {
            _beakerVolume.Text = string.Empty;
            _beakerList.AddChild(new Label { Text = Loc.GetString("chem-master-packaging-no-beaker") });
        }

        // Packaging buffer column.
        _bufferList.RemoveAllChildren();
        _bufferVolume.Text = $"{state.PackagingVolume}/{state.PackagingMaxVolume}";
        if (state.PackagingReagents.Count == 0)
            _bufferList.AddChild(new Label { Text = Loc.GetString("chem-master-packaging-buffer-empty") });

        for (var i = 0; i < state.PackagingReagents.Count; i++)
            _bufferList.AddChild(MakeReagentRow(state.PackagingReagents[i], i, comp, fromBeaker: false));

        _modeButton.Text = Loc.GetString(comp.Discarding ? "chem-master-packaging-discarding" : "chem-master-packaging-moving");

        BuildSections(comp, packaging);

        for (var i = 0; i < _containerButtons.Count && i < comp.Sections.Count; i++)
            _containerButtons[i].Pressed = comp.Sections[i].ContainerPressed;

        foreach (var (section, proto, button) in _packagingButtons)
            button.Pressed = comp.SelectedPackaging is { } sel && sel.Index == section && sel.Packaging == proto;

        _amount.OverrideValue(comp.PackagingAmount);
        _printButton.Disabled = comp.SelectedPackaging == null || state.PackagingVolume <= FixedPoint2.Zero;
        _printButton.ToolTip = comp.SelectedPackaging == null
            ? Loc.GetString("chem-master-packaging-select-first")
            : state.PackagingVolume <= FixedPoint2.Zero
                ? Loc.GetString("chem-master-packaging-buffer-empty")
                : null;

        _outputLabel.Text = state.OutputContainerName ?? Loc.GetString("chem-master-packaging-output-empty");
        _ejectOutput.Disabled = state.OutputContainerName == null;
    }

    private Control MakeReagentRow(ReagentQuantity reagent, int index, ChemMasterPackagingComponent comp, bool fromBeaker)
    {
        _prototype.TryIndex(reagent.Reagent.Prototype, out ReagentPrototype? proto);
        var name = proto?.LocalizedName ?? Loc.GetString("chem-master-window-unknown-reagent-text");

        var buttons = new BoxContainer { Orientation = LayoutOrientation.Horizontal };
        var amounts = comp.TransferAmounts;
        for (var i = 0; i < amounts.Length; i++)
        {
            var amount = amounts[i];
            buttons.AddChild(MakeTransferButton(amount.Int().ToString(), i == 0 ? StyleClass.ButtonOpenRight : StyleClass.ButtonOpenBoth, reagent.Reagent, amount, fromBeaker));
        }

        buttons.AddChild(MakeTransferButton(Loc.GetString("chem-master-packaging-all"), StyleClass.ButtonOpenLeft, reagent.Reagent, FixedPoint2.MaxValue, fromBeaker));

        return new PanelContainer
        {
            PanelOverride = new StyleBoxFlat(index % 2 == 1 ? RowColor1 : RowColor2),
            Children =
            {
                new BoxContainer
                {
                    Orientation = LayoutOrientation.Horizontal,
                    Children =
                    {
                        new PanelContainer
                        {
                            MinWidth = 4,
                            VerticalExpand = true,
                            Margin = new Thickness(0, 1, 4, 1),
                            PanelOverride = new StyleBoxFlat(proto?.SubstanceColor ?? Color.Transparent),
                        },
                        new Label { Text = name, ClipText = true, HorizontalExpand = true },
                        new Label { Text = $"{reagent.Quantity}u", StyleClasses = { StyleClass.LabelWeak }, Margin = new Thickness(4, 0) },
                        buttons,
                    },
                },
            },
        };
    }

    private Button MakeTransferButton(string text, string styleClass, ReagentId reagent, FixedPoint2 amount, bool fromBeaker)
    {
        var button = new Button { Text = text, MinSize = new Vector2(30, 0), StyleClasses = { styleClass } };
        button.OnPressed += _ => OnTransfer?.Invoke(reagent, amount, fromBeaker);
        return button;
    }

    /// <summary>Sections come from YAML and never change while the window is open, so build them once.</summary>
    private void BuildSections(ChemMasterPackagingComponent comp, ChemMasterPackagingSystem packaging)
    {
        if (_builtSectionCount == comp.Sections.Count)
            return;

        _builtSectionCount = comp.Sections.Count;
        _sections.RemoveAllChildren();
        _packagingButtons.Clear();
        _containerButtons.Clear();

        for (var i = 0; i < comp.Sections.Count; i++)
        {
            var index = i;
            var section = comp.Sections[i];

            _sections.AddChild(new Label { Text = section.Name, StyleClasses = { StyleClass.LabelWeak }, Margin = new Thickness(0, 4, 0, 2) });

            var containerButton = new Button { ToggleMode = true, Visible = section.Container != null, HorizontalAlignment = HAlignment.Left };
            if (section.Container is { } containerId)
            {
                containerButton.Text = Loc.GetString("chem-master-packaging-pack-into",
                    ("container", _prototype.TryIndex(containerId, out var containerProto) ? containerProto.Name : containerId.Id));
            }

            containerButton.OnPressed += args => OnUseContainer?.Invoke(index, args.Button.Pressed);
            _containerButtons.Add(containerButton);
            _sections.AddChild(containerButton);

            var grid = new GridContainer { Columns = 10 };
            foreach (var packagingId in section.Packaging)
            {
                if (!_prototype.TryIndex(packagingId, out var packagingProto))
                    continue;

                var tooltip = packaging.TryGetPackagingMaxVolume(packagingId, out var capacity)
                    ? $"{packagingProto.Name} ({capacity.Value.Int()}u)"
                    : packagingProto.Name;

                var view = new EntityPrototypeView();
                view.SetPrototype(packagingId);
                var button = new Button
                {
                    ToggleMode = true,
                    ToolTip = tooltip,
                    MinSize = new Vector2(40, 40),
                    Children = { view },
                };

                button.OnPressed += _ => OnSelect?.Invoke(index, packagingId);
                _packagingButtons.Add((index, packagingId, button));
                grid.AddChild(button);
            }

            _sections.AddChild(grid);
        }
    }
}
