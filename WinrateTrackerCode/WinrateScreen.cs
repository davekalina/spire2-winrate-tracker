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

    private static WinrateScreen? _current;

    private readonly NStatsScreen _screen;
    private readonly List<NSettingsTab> _tabs = [];
    // Assigned in Build, which the constructor calls inside a try so a failure can tear
    // the screen back down.
    private FilterBar _filters = null!;
    private VBoxContainer _content = null!;
    private MegaLabel _summary = null!;

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

        _screen.AddChild(BuildFilterRow());
        ReplaceNativeContent();
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
        if (RunArchive.HasLoaded)
        {
            Rebuild();
            return;
        }

        Rebuild();
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

    private void Rebuild()
    {
        if (!_screen.IsValid())
            return;

        foreach (var child in _content.GetChildren())
        {
            _content.RemoveChild(child);
            child.QueueFree();
        }

        var runs = WinrateSession.Filter.Apply(RunArchive.Runs);
        var report = WinrateReport.Build(runs);

        _summary.SetTextAutoSize(SummaryText(report));
        _content.AddChild(NativeTable.BuildTab(
            ReportTables.Build(WinrateSession.Tab, report),
            EmptyMessage()));
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
        var row = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Pass };
        row.AddThemeConstantOverride("separation", 8);
        // Anchored to the screen's centre, like the tab row above it, so the filters stay
        // put at any resolution.
        row.AnchorLeft = 0.5f;
        row.AnchorRight = 0.5f;
        row.AnchorTop = 0.5f;
        row.AnchorBottom = 0.5f;
        row.OffsetLeft = -760f;
        row.OffsetRight = 760f;
        row.OffsetTop = FilterRowTop;
        row.OffsetBottom = FilterRowTop + FilterRowHeight;
        row.GrowHorizontal = Control.GrowDirection.Both;
        row.GrowVertical = Control.GrowDirection.Both;

        row.AddChild(_filters.Root);
        row.AddChild(_summary);
        return row;
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
