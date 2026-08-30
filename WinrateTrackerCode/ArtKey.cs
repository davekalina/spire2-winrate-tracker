namespace WinrateTracker.WinrateTrackerCode;

/// <summary>
/// Which piece of the game's art a row wants, named rather than loaded.
///
/// The tables decide that a Necrobinder row shows the Necrobinder icon; only the renderer
/// knows that this means <c>res://images/ui/top_panel/character_icon_necrobinder.png</c>.
/// Keeping the decision here and the path in <see cref="GameArt" /> is what lets the whole
/// contents of every table be asserted in tests, which run without the game assembly and
/// with no Godot resource loader to ask.
///
/// A key that no art answers is simply null. Every caller reserves the slot anyway, so a
/// rarity the game ships no icon for leaves its rows aligned with the ones that do.
/// </summary>
internal static class ArtKey
{
    public const string CharacterPrefix = "character/";
    public const string RarityPrefix = "rarity/";

    /// <summary>The ascension flame, badged over a character icon in the run tip.</summary>
    public const string Ascension = "ascension";

    /// <summary>A clock, beside how long a run took.</summary>
    public const string Clock = "clock";

    /// <summary>
    /// The five playable characters. Matched on the display name the run files reduce to —
    /// <c>CHARACTER.NECROBINDER</c> becomes <c>Necrobinder</c> — lowercased, which is
    /// exactly how the game names the icon files. A character this build has never heard of
    /// gets no icon rather than a broken one.
    /// </summary>
    public static string? Character(string? character) =>
        string.IsNullOrWhiteSpace(character)
            ? null
            : CharacterPrefix + character.Trim().ToLowerInvariant();

    /// <summary>
    /// A card or relic rarity.
    ///
    /// The table matters because the two vocabularies collide on one word: a card's
    /// <c>Event</c> rarity is drawn with the game's special-card icon, while a relic's
    /// <c>Event</c> has no art of its own and must not borrow the card one — they are
    /// different things that happen to share a name.
    /// </summary>
    public static string? Rarity(string table, string? rarity)
    {
        if (string.IsNullOrWhiteSpace(rarity) || rarity == GameData.UnknownRarity)
            return null;
        return RarityPrefix + table + "/" + rarity.Trim().ToLowerInvariant();
    }
}
