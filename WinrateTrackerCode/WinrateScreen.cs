using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;
using MegaCrit.Sts2.Core.Nodes.Screens.StatsScreen;
using MegaCrit.Sts2.addons.mega_text;

namespace WinrateTracker.WinrateTrackerCode;

/// <summary>
/// The Win Rates screen.
///
/// It is a second, private instance of the game's own Statistics screen with its contents
/// replaced. That screen is already the exact shape this one needs — a tabbed, scrollable
/// panel of numbers with a back button — and reusing it means the back button, the
/// Escape and controller dismissal, the scroll gradient, and the scroll-follows-focus
/// behaviour are all the game's own, not imitations.
/// It also has to be a real <c>NSubmenu</c> to go on the submenu stack at all, and a mod
/// assembly cannot declare one: a subclass of a Godot script type needs a registered
/// script, which only the game's own scenes have.
///
/// Two things are borrowed and then neutralised. The scene ships two tabs, so two more
/// are added before it enters the tree, while <c>NStatsTabManager</c> is still counting
/// them. And <c>NGeneralStatsGrid.LoadStats</c> is skipped for this instance by
/// <see cref="StatsScreenPatch" />, because the widgets it writes into are freed when the
/// native content is cleared out.
/// </summary>
internal sealed class WinrateScreen : IDisposable
{
    private const string StatsScreenScene = "screens/stats_screen/stats_screen";
    private const string TabScene = "screens/settings_tab";

    /// <summary>Content inset. Wider than the native screen's, which is sized for two columns.</summary>
    private const int ContentMarginLeft = 250;

    private const int ContentMarginRight = 470;

    /// <summary>Clears the tab row and the filter row above it.</summary>
    private const int ContentMarginTop = 288;

    /// <summary>
    /// Extra height the filter band takes on the Cards and Relics tabs, where a second row
    /// of filters appears. The content and the scrollbar drop by the same amount, so the
    /// band never covers the table under it.
    /// </summary>
    private const float PickRowHeight = 96f;

    private const int ContentMarginBottom = 100;

    /// <summary>Filter row position, below the tabs, in the screen's 1920x1080 reference frame.</summary>
    private const float FilterRowTop = -392f;

    private const float FilterRowHeight = 132f;

    /// <summary>The screen's design height; the scroll mask's gradient is a fraction of it.</summary>
    private const float ReferenceHeight = 1080f;

    /// <summary>How much of the screen the content takes to dissolve, as a fraction.</summary>
    private const float FadeDepth = 0.05f;

    /// <summary>Clearance between the filter row and the top of the scrollbar.</summary>
    private const float ScrollbarGap = 36f;

    /// <summary>Matches the game's own top-bar gear, which is drawn at 64 px.</summary>
    private const float GearSize = 64f;

    /// <summary>From <c>settings_tab.tscn</c>, whose root has a 256x90 minimum.</summary>
    private const float TabWidth = 256f;

    /// <summary>The scene's own separation between tabs.</summary>
    private const float TabSeparation = 12f;

    /// <summary>
    /// Byline placement, in the bottom-left corner under the back button — which the
    /// scene anchors at x -40 with a 40 px inset on its ribbon, so 24 lines up with the
    /// arrow's left edge.
    ///
    /// The width matters: the content column starts at <see cref="ContentMarginLeft" />,
    /// and a byline box wide enough to reach it will sit over the tables.
    /// </summary>
    private const float BylineLeft = 24f;

    private const float BylineWidth = ContentMarginLeft - BylineLeft - 8f;
    private const float BylineBottom = 40f;
    private const float BylineHeight = 52f;

    private static readonly string GearIconPath =
        ImageHelper.GetImagePath("atlases/ui_atlas.sprites/top_bar/top_bar_settings.tres");

    private static WinrateScreen? _current;

    private readonly NStatsScreen _screen;
    private readonly List<NSettingsTab> _tabs = [];
    // Assigned in Build, which the constructor calls inside a try so a failure can tear
    // the screen back down.
    private FilterBar _filters = null!;
    private MarginContainer? _contentInset;
    private PickFilterBar _pickFilters = null!;
    private Control _filterFrame = null!;
    private VBoxContainer _content = null!;
    private MegaLabel _summary = null!;
    private GraphPopup? _graph;
    private SettingsPopup? _settings;
    private Control? _gear;
    private ScrollCursor? _scrollCursor;

    private WinrateScreen(NSubmenuStack stack)
    {
        _screen = SceneHelper.Instantiate<NStatsScreen>(StatsScreenScene);
        try
        {
            Build(stack);
        }
        catch
        {
            // Build parents the screen to the stack partway through. Left there, it would
            // draw over the Compendium without being on the stack, so nothing could
            // dismiss it.
            _screen.QueueFree();
            throw;
        }
    }

    private void Build(NSubmenuStack stack)
    {
        // Before the tree, so NStatsTabManager._Ready counts every tab: it snapshots the
        // container's children once and never looks again.
        var tabContainer = _screen.GetNode<Control>("%Tabs").GetNode<Control>("TabContainer");
        for (var i = tabContainer.GetChildCount(); i < TabCount; i++)
            tabContainer.AddChild(SceneHelper.Instantiate<NSettingsTab>(TabScene));

        // Everything that does not need the screen is built first, while nothing is
        // parented yet. If any of it throws, the stack is left exactly as it was rather
        // than holding a half-built screen.
        _filters = new FilterBar();
        _filters.Changed += Rebuild;

        _pickFilters = new PickFilterBar();
        _pickFilters.Changed += Rebuild;

        _summary = NativeStyle.Cell("", rightAligned: false, header: true);
        _summary.HorizontalAlignment = HorizontalAlignment.Center;

        _content = new VBoxContainer();
        _content.AddThemeConstantOverride("separation", NativeStyle.SectionSeparation);

        // Must start hidden, exactly as NMainMenuSubmenuStack does for the game's own
        // submenus. NSubmenu enables its back button from the VisibilityChanged signal,
        // and NSubmenuStack.Push raises that by setting Visible = true — which is not a
        // change, and so not a signal, on a screen that was already visible. Skip this and
        // the screen opens with no way out of it.
        _screen.Visible = false;
        stack.AddChild(_screen);
        _screen.SetStack(stack);
        StatsScreenPatch.SuppressNativeStats(_screen);
        // Where the cursor lands on a gamepad. Registered before anything can ask, and
        // asked lazily, because the filter row is not built yet.
        ScreenFocusPatch.SetDefaultControl(_screen, () => _filters.Controls.FirstOrDefault());

        // The scene sizes its row for two tabs; this screen has five and a gear beside
        // them. Each tab is 256 wide, so the row has to be told it is allowed to be wider
        // or the outer tabs are laid out past its edge.
        WidenTabRow(tabContainer.GetParent() as Control);

        _tabs.AddRange(tabContainer.GetChildren().OfType<NSettingsTab>());
        for (var i = 0; i < _tabs.Count && i < TabCount; i++)
        {
            var tab = (ReportTab)i;
            _tabs[i].SetLabel(ReportTables.Title(tab));
            // The scene's second tab is the game's unreleased Achievements tab: it ships
            // disabled, with a greyed label and a padlock over it. All three have to go.
            // Only the label's tint is reset — the tab's Outline carries the cyan
            // selection glow, and whitening that would flatten the selected state.
            _tabs[i].Enable();
            if (_tabs[i].GetNodeOrNull<CanvasItem>("Lock") is { } padlock)
                padlock.Visible = false;
            if (_tabs[i].GetNodeOrNull<CanvasItem>("Label") is { } label)
                label.Modulate = Colors.White;
            // Focusable so the d-pad can reach the tab row from the filters and Select
            // presses it; NClickableControl already answers Select when focused.
            _tabs[i].FocusMode = Control.FocusModeEnum.All;
            _tabs[i].Connect(
                NClickableControl.SignalName.Released,
                Callable.From<NClickableControl>(_ => Show(tab)));
        }

        ReplaceNativeContent();
        RaiseContentFade();
        LowerScrollbar();
        AddGearButton(tabContainer);
        _screen.AddChild(BuildByline());
        // Added last so the filter row sits above the scroll body in draw order.
        _screen.AddChild(BuildFilterRow());
        LayOutForTab();
        WireGamepad();
    }

    /// <summary>
    /// Give the tab row room for every tab. Anchored to the screen's centre like the scene
    /// has it, so it stays put at any resolution.
    /// </summary>
    private void WidenTabRow(Control? tabRow)
    {
        if (tabRow is null || !tabRow.IsValid())
            return;
        var half = ((TabCount * TabWidth) + ((TabCount - 1) * TabSeparation) + GearSize + TabSeparation) / 2f;
        tabRow.OffsetLeft = -half;
        tabRow.OffsetRight = half;
    }

    /// <summary>The scroll track.</summary>
    private Control? Scrollbar =>
        _screen.GetNodeOrNull<Control>("%StatsGrid/ScrollableContent/Scrollbar");

    /// <summary>
    /// Let the scrollbar be shortened from the top.
    ///
    /// <see cref="LayOutForTab" /> drops its top edge below the filter band, but moving the
    /// top alone is not enough: the scene gives the scrollbar a minimum height of 800 and
    /// grows it from its centre, so a shorter rect is expanded back out in both directions,
    /// putting the top straight back under the filters. Growing downwards instead is what
    /// makes the shorter rect stick.
    /// </summary>
    private void LowerScrollbar()
    {
        if (Scrollbar is { } scrollbar)
            scrollbar.GrowVertical = Control.GrowDirection.End;
    }

    /// <summary>
    /// The gear, at the end of the tab row. The same settings are in Settings → Mod
    /// Settings, but a setting that changes what the table in front of you counts should
    /// not require leaving the table to reach.
    /// </summary>
    private void AddGearButton(Control tabContainer)
    {
        if (tabContainer.GetParent() is not Control tabRow)
            return;
        _gear = NativeStyle.IconButton(GearIconPath, GearSize, ShowSettings);
        tabRow.AddChild(_gear);
    }

    /// <summary>
    /// Make the screen navigable with a d-pad.
    ///
    /// Nothing is intercepted to do it. The game binds its d-pad to <c>ui_up</c> and
    /// friends — <c>MegaInput.up</c> is that string — which is Godot's own focus-navigation
    /// action, so a correct set of focus neighbours is the whole implementation.
    /// <see cref="RelinkFocus" /> lays them out; <c>ui_select</c> presses whatever they
    /// land on.
    /// </summary>
    private void WireGamepad()
    {
        // Created once. Its stand-in is a node on the screen, so relinking must not build
        // another one. It watches both filter rows, and hands the cursor back to whichever
        // control was last on.
        if (_screen.GetNodeOrNull<NScrollableContainer>("%StatsGrid/ScrollableContent") is { } scroll)
            _scrollCursor = new ScrollCursor(
                _screen,
                scroll,
                _filters.Controls.Concat(_pickFilters.Controls).ToList());

        RelinkFocus();

        _screen.Connect(CanvasItem.SignalName.VisibilityChanged, Callable.From(() =>
        {
            if (_screen.Visible)
                _filters.FocusFirst();
        }));
    }

    /// <summary>
    /// Chain every row of controls into one closed run of focus stops.
    ///
    /// The rows are the tabs and the gear, the run filters, and — only on the Cards &amp;
    /// Relics tabs — the pick filters. Up and down step between neighbouring rows, left and
    /// right walk along one, and down out of the last row reaches the tables through
    /// <see cref="ScrollCursor" />.
    ///
    /// Every one of the four neighbours is set on every control, and none points anywhere
    /// but at these rows. That has to be exhaustive: Godot only falls back to searching the
    /// screen geometrically when a neighbour is left unset, and that search will happily
    /// find something in the tables. They hold no focus stops of their own either — see
    /// <see cref="SealScrollContent" /> — so a missed neighbour has nothing to land on.
    ///
    /// Re-run whenever the rows change, which means on every tab switch.
    /// </summary>
    private void RelinkFocus()
    {
        var rows = new List<List<Control>>();

        var tabs = _tabs.Cast<Control>().Where(control => control.IsValid()).ToList();
        if (_gear is not null && _gear.IsValid())
            tabs.Add(_gear);
        if (tabs.Count > 0)
            rows.Add(tabs);

        AddRow(rows, _filters.Controls);
        if (_pickFilters.Root.IsValid() && _pickFilters.Root.Visible)
            AddRow(rows, _pickFilters.Controls);

        if (rows.Count == 0)
            return;

        for (var i = 0; i < rows.Count; i++)
            Chain(rows[i], i > 0 ? rows[i - 1] : null, i < rows.Count - 1 ? rows[i + 1] : null);

        // The last row is the one that opens onto the tables.
        if (_scrollCursor is not null && _scrollCursor.Proxy.IsValid())
            foreach (var control in rows[^1])
                control.FocusNeighborBottom = _scrollCursor.Proxy.GetPath();

        SealScrollContent();
    }

    private static void AddRow(List<List<Control>> rows, IEnumerable<Control> controls)
    {
        var row = controls.Where(control => control.IsValid()).ToList();
        if (row.Count > 0)
            rows.Add(row);
    }

    /// <summary>
    /// Wire one row: left and right along it, up and down onto its neighbouring rows —
    /// scaled so the ends of a short row still reach the ends of a long one. A missing
    /// neighbour means that direction points back at the control itself, so the cursor
    /// stops rather than wandering.
    /// </summary>
    private static void Chain(List<Control> row, List<Control>? above, List<Control>? below)
    {
        for (var i = 0; i < row.Count; i++)
        {
            row[i].FocusNeighborLeft = (i > 0 ? row[i - 1] : row[i]).GetPath();
            row[i].FocusNeighborRight = (i < row.Count - 1 ? row[i + 1] : row[i]).GetPath();
            row[i].FocusNeighborTop = Facing(row, i, above);
            row[i].FocusNeighborBottom = Facing(row, i, below);
            // Tab and shift-tab would otherwise walk the whole tree, tables included.
            row[i].FocusNext = row[i].GetPath();
            row[i].FocusPrevious = row[i].GetPath();
        }
    }

    /// <summary>
    /// The control in <paramref name="other" /> nearest the one at <paramref name="index" />,
    /// so the cursor lands under where it left rather than jumping across the row.
    /// </summary>
    private static NodePath Facing(List<Control> row, int index, List<Control>? other)
    {
        if (other is null || other.Count == 0)
            return row[index].GetPath();

        var across = row.Count == 1
            ? 0
            : (int)Math.Round(index * (other.Count - 1) / (double)(row.Count - 1));
        return other[Math.Clamp(across, 0, other.Count - 1)].GetPath();
    }

    /// <summary>
    /// Take the focus off everything in the scrolling body. The tables are read, not
    /// operated, and a focus stop among them would drag the scroll position around on its
    /// own — <c>NScrollableContainer</c> re-centres on whatever inside it takes focus.
    /// <see cref="ScrollCursor" />'s stand-in is parented to the screen rather than to the
    /// content, so it is not swept up here.
    /// </summary>
    private void SealScrollContent()
    {
        foreach (var node in _content.FindChildren("*", "Control", recursive: true, owned: false))
            if (node is Control control)
                control.FocusMode = Control.FocusModeEnum.None;
    }

    private void ShowSettings()
    {
        _settings?.Close();
        _settings = SettingsPopup.Show(_screen);
    }

    /// <summary>
    /// Whose mod this is, under the back button. Small and dim: it should be findable
    /// when wanted and invisible when not.
    /// </summary>
    private static Control BuildByline()
    {
        var label = NativeStyle.Byline(MainFile.ModName, MainFile.Version, MainFile.Author);
        label.AnchorLeft = 0f;
        label.AnchorRight = 0f;
        label.AnchorTop = 1f;
        label.AnchorBottom = 1f;
        label.OffsetLeft = BylineLeft;
        label.OffsetRight = BylineLeft + BylineWidth;
        // Both offsets are measured up from the bottom edge, so the box sits above it
        // rather than half over the edge.
        label.OffsetTop = -(BylineBottom + BylineHeight);
        label.OffsetBottom = -BylineBottom;
        label.GrowVertical = Control.GrowDirection.Begin;
        label.VerticalAlignment = VerticalAlignment.Bottom;
        return label;
    }

    private static int TabCount => Enum.GetValues<ReportTab>().Length;

    /// <summary>
    /// Open the screen, building it the first time and reading the archive in the
    /// background. Safe to call from the Compendium tile on every press.
    /// </summary>
    public static void Open(NSubmenuStack stack)
    {
        if (_current is null || !_current._screen.IsValid())
        {
            _current?.Dispose();
            try
            {
                _current = new WinrateScreen(stack);
            }
            catch (Exception exception)
            {
                // Building the screen parents it to the stack partway through. Left
                // there, a half-built screen draws over the Compendium without being on
                // the stack, so nothing dismisses it and every further press builds
                // another one. Tear it down and stay on the Compendium instead.
                MainFile.Logger.Error($"Could not build the Win Rates screen: {exception}");
                _current?.Dispose();
                _current = null;
                return;
            }
        }

        // A graph left open from last time would be sitting over the screen on reopen.
        _current._graph?.Close();
        _current._graph = null;

        stack.Push(_current._screen);
        _current.RestoreTab();
        _current.BeginLoad();
    }

    /// <summary>
    /// Re-select the tab the player left on. Pushing the screen runs the native
    /// <c>ResetTabs</c>, which always lands on the first tab, and it moves the highlight
    /// without emitting anything — so the remembered tab has to be pressed for real to
    /// bring the highlight and the content back into agreement.
    /// </summary>
    private void RestoreTab()
    {
        var index = (int)WinrateSession.Tab;
        if (index >= 0 && index < _tabs.Count)
            _tabs[index].ForceTabPressed();
        else
            Show(ReportTab.Overview);

        // The borrowed screen hands the focus system NGeneralStatsGrid's first stat entry,
        // which this screen hides — so on a gamepad the player would open onto a focus
        // ring they cannot see. Focus starts on the first filter arrow, from which up
        // reaches the tabs.
        _filters.FocusFirst();
    }

    /// <summary>
    /// Reading roughly 30 MB of run files takes long enough to stutter a screen
    /// transition, so it happens off the main thread and the screen redraws when it
    /// lands. Already-parsed runs are cached, so this is usually a no-op after the first
    /// visit.
    /// </summary>
    private void BeginLoad()
    {
        Rebuild();

        // Resolved every time rather than once: the history directory is profile-scoped,
        // so switching profiles changes it, and RunArchive empties its cache when it does.
        // Re-reading is cheap when nothing moved, because parsed runs are cached by file.
        var directory = RunArchive.ResolveHistoryDirectory();
        _ = Task.Run(async () =>
        {
            await RunArchive.RefreshAsync(directory).ConfigureAwait(false);
            Callable.From(OnArchiveLoaded).CallDeferred();
        });
    }

    private void OnArchiveLoaded()
    {
        if (!_screen.IsValid())
            return;
        // The ascension and character lists are only knowable once the runs are in.
        _filters.Rebuild();
        Rebuild();
    }

    private void Show(ReportTab tab)
    {
        WinrateSession.Tab = tab;
        // The pick tabs carry a second filter row, so the header band and the focus chain
        // both change shape with the tab.
        LayOutForTab();
        RelinkFocus();
        Rebuild();
    }

    /// <summary>
    /// Put a table's graph up over the screen. Only one at a time, so pressing Show Graph
    /// on a second table replaces the first rather than stacking them.
    /// </summary>
    private void ShowGraph(TableSection section)
    {
        _graph?.Close();
        _graph = GraphPopup.Show(_screen, section);
    }

    /// <summary>
    /// Recompute the report and redraw the open tab.
    ///
    /// The replacement is built before the old table is torn down. Clearing first and
    /// building second means anything that throws mid-build leaves the screen blank, with
    /// the header still reporting a healthy run count — which looks like the data
    /// vanished rather than like a bug in the drawing.
    /// </summary>
    private void Rebuild()
    {
        if (!_screen.IsValid())
            return;

        Control replacement;
        try
        {
            var runs = WinrateSession.Filter.Apply(RunArchive.Runs);
            var report = WinrateReport.Build(runs);
            _summary.SetTextAutoSize(SummaryText(report));
            // Which rarities exist depends on the runs in view, so the pick filter's own
            // options are refreshed before they are read.
            _pickFilters.Rebuild(report);
            replacement = NativeTable.BuildTab(
                ReportTables.Build(WinrateSession.Tab, report, WinrateSession.Picks),
                EmptyMessage(),
                ShowGraph);
        }
        catch (Exception exception)
        {
            MainFile.Logger.Error($"Could not draw the {WinrateSession.Tab} tab: {exception}");
            return;
        }

        foreach (var child in _content.GetChildren())
        {
            _content.RemoveChild(child);
            child.QueueFree();
        }
        _content.AddChild(replacement);
        // The replacement is new nodes, so its focus stops have to be taken off again.
        SealScrollContent();
    }

    private static string EmptyMessage()
    {
        if (!RunArchive.HasLoaded)
            return "Reading run history…";
        if (RunArchive.FailureReason is { } failure)
            return failure;
        // On the pick tabs the run filter is only half the story: the minimum and the
        // rarity can empty the list on their own, and saying "no runs match" would send
        // the player to the wrong control.
        return WinrateSession.Tab is ReportTab.Cards or ReportTab.Relics
            ? "Nothing picked matches these filters."
            : "No runs match this filter.";
    }

    /// <summary>
    /// The always-visible line under the filters. It states the co-op exclusion and any
    /// unreadable files, because a run silently missing from a win rate is worse than a
    /// win rate that admits what it left out.
    /// </summary>
    private static string SummaryText(WinrateReport report)
    {
        if (!RunArchive.HasLoaded)
            return "Reading run history…";

        var parts = new List<string>
        {
            $"{report.Overall.Runs} runs · {Format.WinLoss(report.Overall)} · {Format.Percent(report.Overall)}",
            "solo runs only",
        };
        if (RunArchive.UnreadableFiles > 0)
            parts.Add($"{RunArchive.UnreadableFiles} file(s) could not be read");
        return string.Join(" · ", parts);
    }

    private Control BuildFilterRow()
    {
        // Anchored to the screen's centre, like the tab row above it, so the filters stay
        // put at any resolution.
        var frame = new Control { MouseFilter = Control.MouseFilterEnum.Pass };
        frame.AnchorLeft = 0.5f;
        frame.AnchorRight = 0.5f;
        frame.AnchorTop = 0.5f;
        frame.AnchorBottom = 0.5f;
        frame.OffsetLeft = -760f;
        frame.OffsetRight = 760f;
        frame.OffsetTop = FilterRowTop;
        frame.OffsetBottom = FilterRowTop + FilterRowHeight;
        _filterFrame = frame;
        frame.GrowHorizontal = Control.GrowDirection.Both;
        frame.GrowVertical = Control.GrowDirection.Both;

        // A band behind the filters in the same slate the stats panels use, so they read
        // as a fixed header rather than as text floating over the table. Opaque, because
        // the tables scroll underneath it; see NativeStyle.HeaderBandColor.
        var backdrop = new ColorRect
        {
            Color = NativeStyle.HeaderBandColor,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        frame.AddChild(backdrop);

        var row = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Pass };
        row.AddThemeConstantOverride("separation", 8);
        row.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        row.AddChild(_filters.Root);
        row.AddChild(_pickFilters.Root);
        row.AddChild(_summary);
        frame.AddChild(row);
        return frame;
    }

    /// <summary>
    /// Raise the point at which scrolled content dissolves, so it disappears above the
    /// filter row instead of sliding underneath it.
    ///
    /// The scroll body is clipped by a <c>Mask</c> whose gradient alpha is the clip: the
    /// scene fades content out over the top eighth of the screen, which sits well above
    /// where this screen's filter row starts. Re-cutting the gradient is the screen's own
    /// mechanism for this — the same one that fades the last row away at the bottom — so
    /// the result still looks like the native screen rather than a lid laid over it.
    /// </summary>
    private void RaiseContentFade()
    {
        if (_screen.GetNodeOrNull<TextureRect>("%StatsGrid/ScrollableContent/Mask") is not { } mask)
            return;

        var fadeEnd = (FilterRowTop + FilterRowHeight + ReferenceHeight / 2f) / ReferenceHeight;
        var gradient = new Gradient
        {
            Offsets = [fadeEnd - FadeDepth, fadeEnd, 0.975f, 1f],
            Colors =
            [
                new Color(0, 0, 0, 0),
                new Color(0, 0, 0, 1),
                new Color(0, 0, 0, 1),
                // The scene's own bottom stop, kept so the foot of the list fades as before.
                new Color(0.0862745f, 0.0862745f, 0.0862745f, 0),
            ],
        };

        mask.Texture = new GradientTexture2D
        {
            Gradient = gradient,
            Width = 1,
            Height = 128,
            FillTo = new Vector2(0, 1),
        };
    }

    /// <summary>
    /// Take over the native scroll body and widen its margins for tables of up to eight
    /// columns. The scrollbar stays where the scene put it, so the right margin leaves it
    /// clear rather than running the table underneath it.
    ///
    /// The native rows are hidden, not freed. A hidden child takes no room in a
    /// <see cref="VBoxContainer" />, so it costs nothing — and the grid still holds
    /// references to those widgets, one of which it hands back as the screen's default
    /// focus target. Freeing them would leave the focus system holding a dead node.
    /// </summary>
    private void ReplaceNativeContent()
    {
        var container = _screen.GetNode<Control>("%StatsScrollableContainer");
        foreach (var child in container.GetChildren().OfType<CanvasItem>())
            child.Visible = false;

        if (container.GetParent() is MarginContainer inset)
        {
            _contentInset = inset;
            inset.AddThemeConstantOverride("margin_left", ContentMarginLeft);
            inset.AddThemeConstantOverride("margin_right", ContentMarginRight);
            inset.AddThemeConstantOverride("margin_bottom", ContentMarginBottom);
        }

        container.AddChild(_content);
    }

    /// <summary>
    /// Fit the screen around however many filter rows the open tab needs.
    ///
    /// The pick tabs show a second row, which makes the header band taller;
    /// the table below it and the scrollbar beside it both have to start lower or the band
    /// draws over them. Three numbers move together, so they are set in one place.
    /// </summary>
    private void LayOutForTab()
    {
        var showPicks = WinrateSession.Tab is ReportTab.Cards or ReportTab.Relics;
        var extra = showPicks ? PickRowHeight : 0f;

        _pickFilters.Root.Visible = showPicks;

        if (_filterFrame.IsValid())
            _filterFrame.OffsetBottom = FilterRowTop + FilterRowHeight + extra;

        if (_contentInset is not null && _contentInset.IsValid())
            _contentInset.AddThemeConstantOverride("margin_top", ContentMarginTop + (int)extra);

        if (Scrollbar is { } scrollbar)
        {
            scrollbar.OffsetTop = FilterRowTop + FilterRowHeight + extra + ScrollbarGap;
            scrollbar.CustomMinimumSize = new Vector2(
                scrollbar.CustomMinimumSize.X,
                Math.Max(0f, scrollbar.OffsetBottom - scrollbar.OffsetTop));
        }
    }

    public void Dispose()
    {
        _filters.Changed -= Rebuild;
        _pickFilters.Changed -= Rebuild;
        if (_screen.IsValid())
        {
            StatsScreenPatch.Forget(_screen);
            ScreenFocusPatch.Forget(_screen);
            _screen.QueueFree();
        }
        if (ReferenceEquals(_current, this))
            _current = null;
    }
}
