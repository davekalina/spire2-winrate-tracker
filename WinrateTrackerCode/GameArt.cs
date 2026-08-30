using Godot;
using MegaCrit.Sts2.Core.Helpers;

namespace WinrateTracker.WinrateTrackerCode;

/// <summary>
/// The game's own icons, by <see cref="ArtKey" />.
///
/// Nothing here ships with the mod. Every texture is loaded out of the installed game at
/// the path the game's own screens load it from, which is both the only lawful way to
/// show this art and the way it stays correct when a patch redraws it.
///
/// The whole thing is best-effort. A key with no texture behind it — a character this
/// build has never heard of, a rarity the game ships no art for — comes back null, and
/// every caller reserves the slot anyway so its rows stay aligned with the rows that have
/// art. A missing icon costs a column of whitespace, never a screen.
/// </summary>
internal static class GameArt
{
    /// <summary>
    /// Where each key's art lives. Paths are inner paths in the sense
    /// <see cref="ImageHelper.GetImagePath" /> means: the part after <c>res://images/</c>.
    ///
    /// The two rarity vocabularies are listed separately even where they agree, because
    /// they only mostly agree: a card's <c>Event</c> is drawn with the special-card icon,
    /// while a relic's <c>Event</c> — a different thing that happens to share the word —
    /// has no art of its own and must not borrow it.
    /// </summary>
    private static readonly Dictionary<string, string> Paths = new(StringComparer.Ordinal)
    {
        ["character/ironclad"] = "ui/top_panel/character_icon_ironclad.png",
        ["character/silent"] = "ui/top_panel/character_icon_silent.png",
        ["character/defect"] = "ui/top_panel/character_icon_defect.png",
        ["character/regent"] = "ui/top_panel/character_icon_regent.png",
        ["character/necrobinder"] = "ui/top_panel/character_icon_necrobinder.png",

        [ArtKey.Ascension] = "ui/game_over_screen/score_ascension.png",
        [ArtKey.Clock] = "packed/statistics_screen/stats_clock.png",

        ["rarity/cards/common"] = "ui/reward_screen/reward_icon_card.png",
        ["rarity/cards/uncommon"] = "ui/reward_screen/reward_icon_uncommon.png",
        ["rarity/cards/rare"] = "ui/reward_screen/reward_icon_rare.png",
        ["rarity/cards/event"] = "ui/reward_screen/reward_icon_special_card.png",

        ["rarity/relics/common"] = "ui/reward_screen/reward_icon_card.png",
        ["rarity/relics/uncommon"] = "ui/reward_screen/reward_icon_uncommon.png",
        ["rarity/relics/rare"] = "ui/reward_screen/reward_icon_rare.png",
        ["rarity/relics/shop"] = "ui/run_history/shop.png",
    };

    private static readonly Dictionary<string, Texture2D> Loaded = new(StringComparer.Ordinal);

    /// <summary>
    /// The texture for a key, or null if there is none.
    ///
    /// Cached, but re-checked rather than trusted: Godot's resource cache releases a
    /// texture once nothing in the tree references it, which happens every time the game
    /// tears a scene down. The managed wrapper survives that and its native object does
    /// not, so a plain dictionary would start handing out disposed textures a run or two
    /// into a session. This is the same trap <see cref="NativeStyle" /> documents for fonts.
    /// </summary>
    public static Texture2D? Of(string? key)
    {
        if (key is null || !Paths.TryGetValue(key, out var inner))
            return null;

        if (Loaded.TryGetValue(key, out var held) && GodotObject.IsInstanceValid(held))
            return held;

        var path = ImageHelper.GetImagePath(inner);
        try
        {
            // Asked rather than assumed: GD.Load on a path a patch has moved logs an engine
            // error and returns null, and a table redrawn every filter change would fill
            // the log with them.
            if (!ResourceLoader.Exists(path))
            {
                MainFile.Logger.Warn($"The game has no texture at {path}; '{key}' will show no icon.");
                Paths.Remove(key);
                return null;
            }

            var texture = GD.Load<Texture2D>(path);
            if (texture is null)
                return null;
            Loaded[key] = texture;
            return texture;
        }
        catch (Exception exception)
        {
            MainFile.Logger.Warn($"Could not load {path}: {exception.Message}");
            Paths.Remove(key);
            return null;
        }
    }

    /// <summary>
    /// An icon at a fixed size, laid out as a square whatever the art's own proportions
    /// are, so a column of them lines up.
    /// </summary>
    public static TextureRect? Icon(string? key, float size)
    {
        if (Of(key) is not { } texture)
            return null;

        return new TextureRect
        {
            Texture = texture,
            CustomMinimumSize = new Vector2(size, size),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        };
    }

    /// <summary>
    /// A slot of exactly <paramref name="size" />, holding the icon if there is one and
    /// nothing if there is not.
    ///
    /// Always the same width, which is the whole point: a rarity with no art would
    /// otherwise pull its label left and break the one column the eye reads down.
    /// </summary>
    public static Control IconSlot(string? key, float size)
    {
        var slot = new Control
        {
            CustomMinimumSize = new Vector2(size, size),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        };

        if (Icon(key, size) is { } icon)
        {
            icon.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            slot.AddChild(icon);
        }
        return slot;
    }
}
