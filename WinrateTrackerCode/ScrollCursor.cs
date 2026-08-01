using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;

namespace WinrateTracker.WinrateTrackerCode;

/// <summary>
/// Makes the table body a place the cursor can go, below the filter row.
///
/// The tables hold no focus stops of their own — <c>WinrateScreen.SealScrollContent</c>
/// takes them off, and they would drag the scroll position about if they had them, since
/// <c>NScrollableContainer</c> re-centres on whatever inside it takes focus. So there is
/// nothing down there for the d-pad to move to. This adds one thing: an empty control that
/// stands for the whole body, takes the focus when the player presses down out of the
/// filters, and turns up and down into scrolling for as long as it holds it.
///
/// Pressing up at the top of the tables gives the focus back to the filter row, which is
/// the only way out — the body scrolls until it cannot, and then releases.
///
/// The container's own controller scrolling is not used. It only runs while <em>nothing</em>
/// holds focus (<c>_Input</c> checks <c>GuiGetFocusOwner</c>), so it cannot tell being on
/// the tables from being on nothing at all, and there would be no way to know when to hand
/// the cursor back.
/// </summary>
internal sealed class ScrollCursor
{
    /// <summary>Matches <c>NScrollableContainer._controllerScrollAmount</c>, so a press here moves the tables as far as one moves the game's own.</summary>
    private const float StepSize = 400f;

    /// <summary>Slack when asking whether the tables are already at the top.</summary>
    private const float TopTolerance = 0.5f;

    private static readonly AccessTools.FieldRef<NScrollableContainer, float>? TargetScroll = SafeFieldRef();

    private readonly NScrollableContainer _scroll;
    private Control? _returnTo;

    /// <summary>
    /// <paramref name="returnRow" /> is the row the cursor came from and goes back to. Each
    /// of its controls is watched so the cursor returns to the one it left, rather than to
    /// the start of the row.
    /// </summary>
    public ScrollCursor(Control host, NScrollableContainer scroll, IReadOnlyList<Control> returnRow)
    {
        _scroll = scroll;
        _returnTo = returnRow.FirstOrDefault();

        foreach (var control in returnRow)
        {
            var remembered = control;
            control.Connect(Control.SignalName.FocusEntered, Callable.From(() => _returnTo = remembered));
        }

        // Empty and sizeless. It is a focus stop and nothing else: the tables are already
        // drawn, and a control laid over them would only get in the way of the mouse.
        Proxy = new Control
        {
            Name = "ScrollCursor",
            FocusMode = Control.FocusModeEnum.All,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        Proxy.Connect(Control.SignalName.GuiInput, Callable.From<InputEvent>(OnInput));
        host.AddChild(Proxy);

        // Nowhere to go but back up, and that is decided in OnInput rather than by a
        // neighbour, because it depends on how far the tables have scrolled.
        Proxy.FocusNeighborTop = Proxy.GetPath();
        Proxy.FocusNeighborBottom = Proxy.GetPath();
        Proxy.FocusNeighborLeft = Proxy.GetPath();
        Proxy.FocusNeighborRight = Proxy.GetPath();
        Proxy.FocusNext = Proxy.GetPath();
        Proxy.FocusPrevious = Proxy.GetPath();
    }

    /// <summary>The focus stop that stands for the table body. The filter row points down at this.</summary>
    public Control Proxy { get; }

    private void OnInput(InputEvent input)
    {
        if (input.IsActionPressed(MegaInput.down))
        {
            Proxy.AcceptEvent();
            Scroll(-StepSize);
        }
        else if (input.IsActionPressed(MegaInput.up))
        {
            Proxy.AcceptEvent();
            if (AtTop)
                _returnTo?.TryGrabFocus();
            else
                Scroll(StepSize);
        }
    }

    /// <summary>
    /// The scroll target runs from 0 at the top down to a negative limit, so anything at or
    /// above 0 is the top. Unreadable targets count as the top, which means up still hands
    /// the cursor back rather than stranding it.
    /// </summary>
    private bool AtTop =>
        TargetScroll is null || !_scroll.IsValid() || TargetScroll(_scroll) >= -TopTolerance;

    /// <summary>
    /// Positive scrolls up, matching the container's own <c>ProcessControllerEvent</c>. It
    /// clamps the target back into range every frame, so overshooting the bottom springs
    /// back rather than running off.
    /// </summary>
    private void Scroll(float amount)
    {
        if (TargetScroll is null || !_scroll.IsValid())
            return;
        TargetScroll(_scroll) += amount;
    }

    /// <summary>
    /// The scroll target is private, and there is no public way to move it: the container
    /// only scrolls itself from input it handles, and it ignores controller input whenever
    /// anything holds focus — which, on this screen, is always. If the field is ever renamed
    /// this returns null and the tables simply stop scrolling on a pad, rather than taking
    /// the screen down with them.
    /// </summary>
    private static AccessTools.FieldRef<NScrollableContainer, float>? SafeFieldRef()
    {
        try
        {
            return AccessTools.FieldRefAccess<NScrollableContainer, float>("_targetDragPosY");
        }
        catch (Exception exception)
        {
            MainFile.Logger.Warn($"Scrolling on a controller is unavailable: {exception.Message}");
            return null;
        }
    }
}
