using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.StatsScreen;

namespace WinrateTracker.WinrateTrackerCode;

/// <summary>
/// Stops the borrowed Statistics screen from filling in statistics.
///
/// <see cref="WinrateScreen" /> reuses a second instance of the Statistics scene and
/// clears its contents out. <c>NGeneralStatsGrid.LoadStats</c> writes into the widgets
/// that clearing frees, and the screen calls it every time it opens, so it has to be
/// skipped — not just hidden — for that one grid.
///
/// The suppression is keyed on the grid instance, so the real Statistics screen is
/// untouched: opening Compendium → Statistics still loads and shows everything it always
/// did.
/// </summary>
[HarmonyPatch(typeof(NGeneralStatsGrid), nameof(NGeneralStatsGrid.LoadStats))]
internal static class StatsScreenPatch
{
    private const string GridPath = "%StatsGrid";

    private static readonly HashSet<ulong> SuppressedGrids = [];

    public static void SuppressNativeStats(NStatsScreen screen)
    {
        if (GridOf(screen) is { } grid)
            SuppressedGrids.Add(grid.GetInstanceId());
    }

    public static void Forget(NStatsScreen screen)
    {
        if (GridOf(screen) is { } grid)
            SuppressedGrids.Remove(grid.GetInstanceId());
    }

    private static Node? GridOf(NStatsScreen screen) =>
        GodotObject.IsInstanceValid(screen) ? screen.GetNodeOrNull(GridPath) : null;

    [HarmonyPrefix]
    private static bool Prefix(NGeneralStatsGrid __instance) =>
        !SuppressedGrids.Contains(__instance.GetInstanceId());
}
