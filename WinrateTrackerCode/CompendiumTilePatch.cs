using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.addons.mega_text;

namespace WinrateTracker.WinrateTrackerCode;

/// <summary>
/// Adds the Win Rates tile to the Compendium's bottom row.
///
/// The tile is a fresh instance of the game's own <c>compendium_bottom_button</c> scene,
/// so it has the Statistics and Run History tiles' frame, hover scale, press feedback,
/// and focus behaviour without any of it being reimplemented.
///
/// The scene ships two hidden tiles — Leaderboards and an unreferenced Achievements — and
/// it is tempting to take one over rather than add a node. This does not: those are
/// features MegaCrit has clearly built the art for and not yet shipped, and a mod sitting
/// in one of their slots would start fighting the game the patch they turn it on. A new
/// tile is only ever this mod's.
/// </summary>
[HarmonyPatch(typeof(NCompendiumSubmenu))]
internal static class CompendiumTilePatch
{
    private const string ButtonScene = "screens/main_menu/compendium_bottom_button";
    private const string BottomRowPath = "%BottomRow";
    private const string Label = "Win Rates";

    /// <summary>
    /// Rankings artwork, which reads as standings. It belongs to the hidden Leaderboards
    /// tile, so nothing visible in the Compendium wears it.
    /// </summary>
    private static readonly string IconPath = ImageHelper.GetImagePath("packed/main_menu/submenu_leaderboards_icon.png");

    /// <summary>
    /// The icon needs cropping to sit in a tile. These are the values the Compendium scene
    /// already uses for this exact texture in this exact frame.
    /// </summary>
    private static readonly Vector2 IconSize = new(194, 142);

    private static readonly Vector2 IconOffsetTopLeft = new(-105, -92);
    private static readonly Vector2 IconOffsetBottomRight = new(105, 50);

    /// <summary>
    /// Hue and saturation for the tile's panel, picked clear of every other Compendium
    /// tile (0.24, 0.48, 0.725, 0.84, 0.93, 1.0). Brightness is left alone: the button
    /// reads its own resting brightness in <c>_Ready</c> and animates hover against it.
    /// </summary>
    private const float PanelHue = 0.58f;

    private const float PanelSaturation = 2.0f;

    private static readonly StringName HueParameter = "h";
    private static readonly StringName SaturationParameter = "s";

    [HarmonyPostfix]
    [HarmonyPatch("_Ready")]
    private static void AfterReady(NCompendiumSubmenu __instance)
    {
        try
        {
            Install(__instance);
        }
        catch (Exception exception)
        {
            // A missing tile is a far better outcome than a main menu that will not open.
            MainFile.Logger.Error($"Could not add the Win Rates tile: {exception}");
        }
    }

    /// <summary>
    /// The game decides tile visibility every time the Compendium opens: it hides
    /// Leaderboards outright and shows or hides Run History and the Bestiary depending on
    /// save state. Ours is re-asserted afterwards, and focus is re-linked here rather than
    /// in <c>_Ready</c> because only now is it settled which tiles are actually on screen.
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(nameof(NCompendiumSubmenu.OnSubmenuOpened))]
    private static void AfterSubmenuOpened(NCompendiumSubmenu __instance)
    {
        if (Button(__instance) is not { } button)
            return;
        button.Visible = true;
        LinkFocusNeighbours(__instance, button);
    }

    private static void Install(NCompendiumSubmenu submenu)
    {
        if (Button(submenu) is not null)
            return;

        var row = submenu.GetNodeOrNull<Control>(BottomRowPath);
        if (row is null)
        {
            MainFile.Logger.Warn($"The Compendium has no {BottomRowPath}; the Win Rates tile was not added.");
            return;
        }

        var button = SceneHelper.Instantiate<NCompendiumBottomButton>(ButtonScene);
        button.Name = "WinrateTrackerButton";
        button.FocusMode = Control.FocusModeEnum.All;
        row.AddChild(button);

        Tint(button);
        SetLabel(button);
        SetIcon(button);

        button.Connect(
            NClickableControl.SignalName.Released,
            Callable.From<NButton>(_ => Open(submenu)));
    }

    private static void Open(NCompendiumSubmenu submenu)
    {
        if (submenu.GetParent() is NSubmenuStack stack)
            WinrateScreen.Open(stack);
        else
            MainFile.Logger.Error("The Compendium is not on a submenu stack; the Win Rates screen cannot open.");
    }

    private static NCompendiumBottomButton? Button(NCompendiumSubmenu submenu) =>
        submenu.GetNodeOrNull<Control>(BottomRowPath)?.GetNodeOrNull<NCompendiumBottomButton>("WinrateTrackerButton");

    /// <summary>
    /// The panel material is marked local to its scene, so each instantiated tile owns its
    /// own copy and re-tinting one cannot bleed into the game's tiles.
    /// </summary>
    private static void Tint(Control button)
    {
        if (button.GetNodeOrNull<Control>("BgPanel")?.Material is not ShaderMaterial material)
            return;
        material.SetShaderParameter(HueParameter, PanelHue);
        material.SetShaderParameter(SaturationParameter, PanelSaturation);
    }

    /// <summary>
    /// The label is written directly rather than through <c>SetLocalization</c>, which
    /// looks the text up in the game's string tables. A mod has no entry there, and a
    /// missing key throws.
    /// </summary>
    private static void SetLabel(Control button) =>
        button.GetNodeOrNull<MegaLabel>("Label")?.SetTextAutoSize(Label);

    private static void SetIcon(Control button)
    {
        if (button.GetNodeOrNull<TextureRect>("Icon") is not { } icon)
            return;

        icon.Texture = GD.Load<Texture2D>(IconPath);
        icon.ClipContents = true;
        icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        icon.CustomMinimumSize = IconSize;
        icon.SetAnchorsPreset(Control.LayoutPreset.Center);
        icon.OffsetLeft = IconOffsetTopLeft.X;
        icon.OffsetTop = IconOffsetTopLeft.Y;
        icon.OffsetRight = IconOffsetBottomRight.X;
        icon.OffsetBottom = IconOffsetBottomRight.Y;
    }

    /// <summary>
    /// The Compendium wires left/right focus across the bottom row by hand, over the three
    /// tiles it knows about. Adding a fourth re-runs that wiring across every tile that is
    /// actually visible, so controller focus reaches ours instead of stopping at Run
    /// History — and so it still works on a save where Run History or the Bestiary is
    /// hidden.
    /// </summary>
    private static void LinkFocusNeighbours(NCompendiumSubmenu submenu, Control ours)
    {
        if (ours.GetParent() is not Control row)
            return;

        var tiles = row.GetChildren().OfType<NCompendiumBottomButton>().Where(tile => tile.Visible).ToList();
        for (var i = 0; i < tiles.Count; i++)
        {
            tiles[i].FocusNeighborLeft = (i > 0 ? tiles[i - 1] : tiles[i]).GetPath();
            tiles[i].FocusNeighborRight = (i < tiles.Count - 1 ? tiles[i + 1] : tiles[i]).GetPath();
            // Matching the game: the bottom row is the last row, so down goes nowhere.
            tiles[i].FocusNeighborBottom = tiles[i].GetPath();
        }

        // The game gives each of its bottom tiles an upward neighbour in the top row.
        // Ours takes the nearest one, the rightmost top tile.
        if (TopRowTiles(submenu).LastOrDefault() is { } topTile)
            ours.FocusNeighborTop = topTile.GetPath();
    }

    private static List<Control> TopRowTiles(NCompendiumSubmenu submenu) =>
        submenu.GetNodeOrNull<Control>("%BestiaryButton")?.GetParent() is Control topRow
            ? topRow.GetChildren().OfType<Control>().Where(tile => tile.Visible).ToList()
            : [];
}
