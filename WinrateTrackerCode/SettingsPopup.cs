using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;

namespace WinrateTracker.WinrateTrackerCode;

/// <summary>
/// The mod's settings, reachable from the gear beside the tabs.
///
/// The same settings are in Settings → Mod Settings, where a player looks for a mod's
/// options — but a setting that changes what the table in front of you counts should not
/// require leaving the table to reach. Both surfaces read and write
/// <see cref="WinrateSettings" />, so neither can hold a stale value.
/// </summary>
internal sealed class SettingsPopup
{
    private const string TickboxScene = "screens/card_library/card_library_tickbox";
    private const float PanelWidth = 720f;
    private const float PanelHeight = 380f;
    private const float RowHeight = 48f;

    private readonly ModalPanel _modal;

    private SettingsPopup(ModalPanel modal) => _modal = modal;

    public static SettingsPopup Show(Control host)
    {
        var modal = ModalPanel.Open(host, "Winrate Tracker settings", PanelWidth, PanelHeight);

        var rows = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Pass };
        rows.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        rows.AddThemeConstantOverride("separation", 12);
        modal.Content.AddChild(rows);

        var ignoreEarly = SceneHelper.Instantiate<NLibraryStatTickbox>(TickboxScene);
        ignoreEarly.CustomMinimumSize = new Vector2(0, RowHeight);
        ignoreEarly.FocusMode = Control.FocusModeEnum.All;
        ignoreEarly.Ready += () =>
        {
            ignoreEarly.SetLabel("Ignore floor-1 abandons");
            ignoreEarly.IsTicked = WinrateSettings.IgnoreEarlyAbandons;
        };
        ignoreEarly.Toggled += tickbox => WinrateSettings.IgnoreEarlyAbandons = tickbox.IsTicked;
        rows.AddChild(ignoreEarly);

        rows.AddChild(NativeStyle.Note(
            "An abandoned run counts as a loss. A run abandoned on the first floor is a "
            + "reroll rather than a run, and is left out entirely."));

        // The modal opens with Close focused; down reaches the setting and up returns, so
        // a gamepad can get at every control in here.
        modal.LinkFocusBelowClose(ignoreEarly);

        return new SettingsPopup(modal);
    }

    public void Close() => _modal.Close();
}
