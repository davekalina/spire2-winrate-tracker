using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Relics;

namespace WinrateTracker.WinrateTrackerCode;

/// <summary>
/// The real card, or the real relic, for a row on the Cards and Relics tabs.
///
/// A table of win rates by card is a list of names, and a name is not much help unless you
/// already remember what the card does. The game can draw the card itself — so it draws
/// it, from its own scene, with its own art and its own wording, rather than this mod
/// paraphrasing four hundred cards into tooltips that would go stale on the next patch.
///
/// Cards use <c>scenes/ui/card_hover_tip.tscn</c>, which is the game's own composition of
/// a card at the size a hover tip wants it. Relics have no equivalent scene — a relic is
/// just an icon in the game's UI — so their preview is assembled here from the relic node
/// and the model's own name and description.
///
/// All of it is best effort. Anything that cannot be built comes back null and the row
/// simply has no preview, which is what the tables did before.
/// </summary>
internal static class GamePreview
{
    private const string CardTipScene = "ui/card_hover_tip";
    private const string RelicScene = "relics/relic";

    /// <summary>How wide the relic preview is drawn. Cards size themselves from their scene.</summary>
    public const float RelicWidth = 420f;

    private const float RelicIconSize = 72f;
    private const int RelicNameFontSize = 26;
    private const int RelicBodyFontSize = 21;

    private static Dictionary<string, CardModel>? _cards;
    private static Dictionary<string, RelicModel>? _relics;

    /// <summary>
    /// The preview for a key, or null if there is nothing to draw.
    ///
    /// The whole thing is wrapped: this reaches into the game's card scene and its model
    /// database, and a patch that moves either should cost the tables their previews and
    /// nothing else. See the mod's note on Godot swallowing exceptions thrown in
    /// <c>_Ready</c> — a half-built card would otherwise be a silently blank tip.
    /// </summary>
    public static Control? Of(string? key)
    {
        if (key is null)
            return null;

        try
        {
            if (key.StartsWith(ArtKey.CardPreviewPrefix, StringComparison.Ordinal))
                return Card(key[ArtKey.CardPreviewPrefix.Length..]);
            if (key.StartsWith(ArtKey.RelicPreviewPrefix, StringComparison.Ordinal))
                return Relic(key[ArtKey.RelicPreviewPrefix.Length..]);
        }
        catch (Exception exception)
        {
            MainFile.Logger.Warn($"Could not build a preview for '{key}': {exception.Message}");
        }
        return null;
    }

    /// <summary>Whether a preview can be built at all, so a row only listens when there is one.</summary>
    public static bool Exists(string? key) => key is not null && Model(key) is not null;

    private static object? Model(string key)
    {
        try
        {
            if (key.StartsWith(ArtKey.CardPreviewPrefix, StringComparison.Ordinal))
                return CardModels().GetValueOrDefault(key[ArtKey.CardPreviewPrefix.Length..]);
            if (key.StartsWith(ArtKey.RelicPreviewPrefix, StringComparison.Ordinal))
                return RelicModels().GetValueOrDefault(key[ArtKey.RelicPreviewPrefix.Length..]);
        }
        catch (Exception exception)
        {
            MainFile.Logger.Warn($"Could not read the model database: {exception.Message}");
        }
        return null;
    }

    /// <summary>
    /// Models by the token a run file records — <c>SHIV</c>, not <c>CARD.SHIV</c>, which is
    /// the same key <see cref="GameText" /> matches rarities on.
    ///
    /// Built on first use rather than at start-up: the model database fills itself from
    /// static constructors across the whole game assembly, and reading it while the mod is
    /// still initialising would see whatever happens to have registered by then.
    /// </summary>
    private static Dictionary<string, CardModel> CardModels() =>
        _cards ??= ModelDb.AllCards
            .GroupBy(card => card.Id.Entry, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

    private static Dictionary<string, RelicModel> RelicModels() =>
        _relics ??= ModelDb.AllRelics
            .GroupBy(relic => relic.Id.Entry, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

    /// <summary>
    /// The card, drawn by the game.
    ///
    /// The model is assigned from the node's <c>Ready</c> rather than straight away: setting
    /// it calls the card's own <c>Reload</c>, and before <c>_Ready</c> has run the labels and
    /// textures that reload writes into do not exist yet.
    /// </summary>
    private static Control? Card(string id)
    {
        if (!CardModels().TryGetValue(id, out var model))
            return null;

        var tip = SceneHelper.Instantiate<Control>(CardTipScene);
        if (tip.GetNodeOrNull<NCard>("%Card") is not { } card)
        {
            tip.QueueFree();
            return null;
        }

        // A mutable instance, never the canonical template. The database hands out
        // immutable models, and a card node given one draws the game's own "Broken Card —
        // if you can read this, there is a bug" placeholder rather than the card. This is
        // the same conversion the game's own CardHoverTip does for the same reason.
        var instance = model.IsMutable ? model : model.ToMutable();
        card.Ready += () => card.Model = instance;
        tip.MouseFilter = Control.MouseFilterEnum.Ignore;
        return tip;
    }

    /// <summary>
    /// The relic: its icon, its name, and what it does. Assembled rather than borrowed —
    /// the game draws a relic as a bare icon and puts the words in its generic tip stack,
    /// which is wired to run state this screen has none of.
    /// </summary>
    private static Control? Relic(string id)
    {
        if (!RelicModels().TryGetValue(id, out var model))
            return null;

        var column = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        column.AddThemeConstantOverride("separation", 10);

        var relic = SceneHelper.Instantiate<NRelic>(RelicScene);
        relic.CustomMinimumSize = new Vector2(RelicIconSize, RelicIconSize);
        relic.MouseFilter = Control.MouseFilterEnum.Ignore;
        relic.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        var instance = model.IsMutable ? model : model.ToMutable();
        relic.Ready += () => relic.Model = instance;

        column.AddChild(HoverTip.Row(
            14,
            relic,
            HoverTip.Line(GameData.RelicName(id), NativeStyle.CellColor, RelicNameFontSize, bold: true)));

        // The description only. Flavour text is a line of atmosphere, and for the relics
        // the game has not finished it is a red "details will be revealed in the future"
        // placeholder that reads as a fault in this screen rather than in the relic.
        if (Text(model.DynamicDescription) is { } description)
            column.AddChild(HoverTip.Paragraph(
                description, NativeStyle.CellColor, HoverTip.TextWidth(RelicWidth), RelicBodyFontSize));

        return column;
    }

    /// <summary>
    /// A localised string as words.
    ///
    /// Two passes, and both are needed. <c>GetFormattedText</c> resolves the game's own
    /// template holes — <c>{Cards:plural:card|cards}</c> and friends — which the raw text
    /// leaves standing; then the colour tags come out, because those are BBCode for the
    /// game's rich-text label and this tip measures its own wrapping. Showing the raw string
    /// put "[blue]{Cards}[/blue]" on screen, which reads as a broken mod.
    ///
    /// Null when the table has nothing under the key, and a relic whose description will not
    /// resolve simply shows its name.
    /// </summary>
    private static string? Text(MegaCrit.Sts2.Core.Localization.LocString? line)
    {
        try
        {
            var text = line?.GetFormattedText();
            if (string.IsNullOrWhiteSpace(text))
                return null;
            text = Markup.Replace(text, string.Empty).Trim();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// A BBCode tag: <c>[blue]</c>, <c>[/blue]</c>, <c>[img=32]</c>. Deliberately not a
    /// general HTML-ish matcher — it must not eat a square bracket a relic's own text uses.
    /// </summary>
    private static readonly System.Text.RegularExpressions.Regex Markup =
        new(@"\[/?[a-zA-Z][^\[\]]*\]", System.Text.RegularExpressions.RegexOptions.Compiled);
}
