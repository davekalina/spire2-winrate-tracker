using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.StatsScreen;

namespace WinrateTracker.WinrateTrackerCode;

/// <summary>
/// Tells the game where the cursor goes when this screen is opened on a gamepad.
///
/// The game does not let a screen keep whatever focus it sets for itself. Every controller
/// press runs <c>NControllerManager.CheckForControllerInput</c>, which calls
/// <c>ActiveScreenContext.FocusOnDefaultControl</c>; that reads the current screen's
/// <c>DefaultFocusedControl</c> and, finding nothing, calls <c>GuiReleaseFocus</c>. So a
/// screen with no declared default control does not merely start unfocused — it is pushed
/// back to unfocused on every press.
///
/// That is fatal here rather than untidy. <c>NScrollableContainer._Input</c> scrolls on the
/// d-pad only while nothing holds focus, so a screen the game keeps un-focusing hands its
/// whole d-pad to the scroll body, and none of the controls above it can ever be reached.
///
/// The borrowed Statistics screen answers with its stats grid's first entry, which
/// <see cref="StatsScreenPatch" /> stops being filled in. This replaces that answer, for
/// this mod's instance only, so the real Statistics screen is untouched.
/// </summary>
[HarmonyPatch(typeof(NStatsScreen), "InitialFocusedControl", MethodType.Getter)]
internal static class ScreenFocusPatch
{
    private static readonly Dictionary<ulong, Func<Control?>> Defaults = [];

    /// <summary>
    /// A callback rather than a control: the row it points into is rebuilt, so what should
    /// hold focus has to be asked for at the moment it is wanted.
    /// </summary>
    public static void SetDefaultControl(NStatsScreen screen, Func<Control?> defaultControl) =>
        Defaults[screen.GetInstanceId()] = defaultControl;

    public static void Forget(NStatsScreen screen) => Defaults.Remove(screen.GetInstanceId());

    [HarmonyPrefix]
    private static bool Prefix(NStatsScreen __instance, ref Control? __result)
    {
        if (!Defaults.TryGetValue(__instance.GetInstanceId(), out var defaultControl))
            return true;

        // Skips the original rather than correcting it afterwards: it reads widgets this
        // mod has already cleared out of the grid.
        __result = defaultControl();
        return false;
    }
}
