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
/// Escape and controller dismissal, the focus handling, the scroll gradient, the
/// scrollbar, and the L/R trigger tab cycling are all the game's own, not imitations.
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

    private const int ContentMarginBottom = 100;

    /// <summary>Filter row position, below the tabs, in the screen's 1920x1080 reference frame.</summary>
    private const float FilterRowTop = -392f;

    private const float FilterRowHeight = 132f;

    /// <summary>The screen's design height; the scroll mask's gradient is a fraction of it.</summary>
    private const float ReferenceHeight = 1080f;

    /// <summary>How much of the screen the content takes to dissolve, as a fraction.</summary>
    private const float FadeDepth = 0.05f;

    /// <summary>Clearance between the filter row and the top of the scrollbar.</summary>
    private const float ScrollbarGap = 24f;

    private const float GearSize = 64f;
    private const float BylineLeft = 64f;
    private const float BylineBottom = 48f;

    private static readonly string GearIconPath =
        ImageHelper.GetImagePath("atlases/ui_atlas.sprites/top_bar/top_bar_settings.tres");

    private static WinrateScreen? _current;

    private readonly NStatsScreen _screen;
    private readonly List<NSettingsTab> _tabs = [];
    // Assigned in Build, which the constructor calls inside a try so a failure can tear
    // the screen back down.
    private FilterBar _filters = null!;
    private VBoxContainer _content = null!;
    private MegaLabel _summary = null!;
    private GraphPopup? _graph;
    private SettingsPopup? _settings;

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
        // Before the tree, so NStatsTabManager._Ready counts four tabs and wires trigger
        // cycling across all of them.
        var tabContainer = _screen.GetNode<Control>("%Tabs").GetNode<Control>("TabContainer");
        for (var i = tabContainer.GetChildCount(); i < TabCount; i++)
            tabContainer.AddChild(SceneHelper.Instantiate<NSettingsTab>(TabScene));

        // Everything that does not need the screen is built first, while nothing is
        // parented yet. If any of it throws, the stack is left exactly as it was rather
        // than holding a half-built screen.
        _filters = new FilterBar();
        _filters.Changed += Rebuild;

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
    }

    /// <summary>
    /// Drop the scrollbar below the filter row. The scene anchors it for a screen whose
    /// content starts under the tabs; this one has a header strip in between, and the
    /// scrollbar was running up behind it.
    /// </summary>
    private void LowerScrollbar()
    {
        if (_screen.GetNodeOrNull<Control>("%StatsGrid/ScrollableContent/Scrollbar") is not { } scrollbar)
            return;
        scrollbar.OffsetTop = FilterRowTop + FilterRowHeight + ScrollbarGap;
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
        tabRow.AddChild(NativeStyle.IconButton(GearIconPath, GearSize, ShowSettings));
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
        var label = NativeStyle.Byline($"{MainFile.ModName} {MainFile.Version} by {MainFile.Author}");
        label.SetAnchorsPreset(Control.LayoutPreset.BottomLeft);
        label.AnchorTop = 1f;
        label.AnchorBottom = 1f;
        label.OffsetLeft = BylineLeft;
        label.OffsetTop = -BylineBottom;
        label.OffsetBottom = -BylineBottom + 28f;
        label.GrowVertical = Control.GrowDirection.Begin;
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
            replacement = NativeTable.BuildTab(
                ReportTables.Build(WinrateSession.Tab, report),
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
    }

    private static string EmptyMessage()
    {
        if (!RunArchive.HasLoaded)
            return "Reading run history…";
        return RunArchive.FailureReason ?? "No runs match this filter.";
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
        frame.GrowHorizontal = Control.GrowDirection.Both;
        frame.GrowVertical = Control.GrowDirection.Both;

        // A band behind the filters in the same slate the stats panels use, so they read
        // as a fixed header rather than as text floating over the table.
        var backdrop = new ColorRect
        {
            Color = NativeStyle.PanelColor,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        frame.AddChild(backdrop);

        var row = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Pass };
        row.AddThemeConstantOverride("separation", 8);
        row.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        row.AddChild(_filters.Root);
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
            inset.AddThemeConstantOverride("margin_left", ContentMarginLeft);
            inset.AddThemeConstantOverride("margin_right", ContentMarginRight);
            inset.AddThemeConstantOverride("margin_top", ContentMarginTop);
            inset.AddThemeConstantOverride("margin_bottom", ContentMarginBottom);
        }

        container.AddChild(_content);
    }

    public void Dispose()
    {
        _filters.Changed -= Rebuild;
        if (_screen.IsValid())
        {
            StatsScreenPatch.Forget(_screen);
            _screen.QueueFree();
        }
        if (ReferenceEquals(_current, this))
            _current = null;
    }
}
