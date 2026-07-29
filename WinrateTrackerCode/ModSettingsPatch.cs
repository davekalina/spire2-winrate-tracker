using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;
using MegaCrit.Sts2.Core.Nodes.Screens.ModdingScreen;

namespace WinrateTracker.WinrateTrackerCode;

/// <summary>
/// The mod's settings, shown in the info panel of Settings → Mod Settings when this mod
/// is selected.
///
/// The game has no per-mod settings API and no page to put one on, but it does have a
/// panel that already describes the selected mod. Adding the control there keeps it where
/// a player would look for it, and keeps it off the Win Rates screen — what counts as a
/// run is a standing decision, not something to flip while reading a table.
/// </summary>
[HarmonyPatch(typeof(NModInfoContainer))]
internal static class ModSettingsPatch
{
    private const string ContainerName = "WinrateTrackerModSettings";
    private const string TickboxScene = "screens/card_library/card_library_tickbox";

    // The info panel is 666 x 901 with its description running to y 886. The control
    // takes the bottom strip and the description gives up the room.
    private const float DescriptionBottom = 820f;
    private const float ControlsTop = 836f;

    [HarmonyPostfix]
    [HarmonyPatch(nameof(NModInfoContainer.Fill))]
    private static void AfterFill(NModInfoContainer __instance, Mod mod)
    {
        try
        {
            var isThisMod = mod.manifest?.id == MainFile.ModId;
            var controls = Resolve(__instance, isThisMod);
            if (controls != null)
                controls.Visible = isThisMod;
            if (__instance.GetNodeOrNull<Control>("ModDescription") is { } description)
                description.OffsetBottom = isThisMod ? DescriptionBottom : 886f;
        }
        catch (Exception exception)
        {
            // A missing setting is better than an unusable Mod Settings screen.
            MainFile.Logger.Error($"Could not add the Winrate Tracker settings control: {exception}");
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(NModInfoContainer.Clear))]
    private static void AfterClear(NModInfoContainer __instance)
    {
        if (__instance.GetNodeOrNull<Control>(ContainerName) is { } controls)
            controls.Visible = false;
    }

    private static Control? Resolve(NModInfoContainer panel, bool create)
    {
        if (panel.GetNodeOrNull<Control>(ContainerName) is { } existing)
            return existing;
        if (!create)
            return null;

        var controls = new VBoxContainer
        {
            Name = ContainerName,
            OffsetLeft = 25f,
            OffsetTop = ControlsTop,
            OffsetRight = 641f,
            OffsetBottom = 886f,
        };
        controls.AddThemeConstantOverride("separation", 4);

        var ignoreEarly = SceneHelper.Instantiate<NLibraryStatTickbox>(TickboxScene);
        ignoreEarly.Name = "IgnoreEarlyAbandons";
        ignoreEarly.FocusNeighborTop = new NodePath();
        ignoreEarly.FocusNeighborBottom = new NodePath();
        ignoreEarly.CustomMinimumSize = new Vector2(0, 42);
        ignoreEarly.Ready += () =>
        {
            ignoreEarly.SetLabel("Ignore floor-1 abandons");
            ignoreEarly.IsTicked = WinrateSettings.IgnoreEarlyAbandons;
        };
        ignoreEarly.Toggled += tickbox => WinrateSettings.IgnoreEarlyAbandons = tickbox.IsTicked;
        controls.AddChild(ignoreEarly);

        panel.AddChild(controls);
        return controls;
    }
}
