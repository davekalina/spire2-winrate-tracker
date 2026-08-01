using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;

namespace WinrateTracker.WinrateTrackerCode;

/// <summary>
/// The screen's gamepad scheme, and the reasoning behind which physical control does what.
///
/// The game's own bindings, from <c>ControllerConfig.DefaultControllerInputMap</c>:
///
/// <list type="bullet">
/// <item>bumpers → <c>viewDeckAndTabLeft</c> / <c>viewExhaustPileAndTabRight</c></item>
/// <item>triggers → <c>viewDrawPile</c> / <c>viewDiscardPile</c></item>
/// <item>d-pad → <c>up</c> / <c>down</c> / <c>left</c> / <c>right</c></item>
/// </list>
///
/// So the triggers cycle tabs and the bumpers page the focused filter, which is the
/// opposite of what the action names suggest — the game puts tabbing on the bumpers. That
/// is deliberate here and matches how this screen is laid out: the tabs are one row and the
/// filters another, and reaching for a trigger to change the page reads better than
/// reaching for it to nudge a value.
///
/// <b>Scrolling is on the right stick only.</b> The left stick cannot be used: the game's
/// controller strategy emits <c>dPadUp</c> alongside <c>lStickUp</c> for every left-stick
/// direction, so a UI that answers the left stick answers the d-pad too, and the two
/// cannot be told apart. The right stick emits nothing but its own actions.
/// </summary>
internal sealed class GamepadControls : IDisposable
{
    /// <summary>Pixels per second of scroll while a stick is held.</summary>
    private const float ScrollSpeed = 1400f;

    private const double PollInterval = 1d / 60d;

    private static readonly AccessTools.FieldRef<NScrollableContainer, float>? TargetScroll =
        SafeFieldRef();

    private readonly Action _tabLeft;
    private readonly Action _tabRight;
    private readonly Action _valueLeft;
    private readonly Action _valueRight;
    private readonly NScrollableContainer? _scroll;
    private readonly Godot.Timer _stickPoll;
    private bool _bound;

    public GamepadControls(
        Node host,
        NScrollableContainer? scroll,
        Action tabLeft,
        Action tabRight,
        Action valueLeft,
        Action valueRight)
    {
        _scroll = scroll;
        _tabLeft = tabLeft;
        _tabRight = tabRight;
        _valueLeft = valueLeft;
        _valueRight = valueRight;

        // Godot gives a mod no per-frame hook without a script, and a stick has to be
        // polled rather than waited on: holding it should keep scrolling.
        _stickPoll = new Godot.Timer { WaitTime = PollInterval, Autostart = true };
        _stickPoll.Connect(Godot.Timer.SignalName.Timeout, Callable.From(PollSticks));
        host.AddChild(_stickPoll);
    }

    /// <summary>
    /// Take over the trigger and bumper hotkeys. Only the most recently pushed binding for
    /// a hotkey fires, so this displaces whatever else claims them while the screen is up,
    /// and must be released the moment it is not.
    /// </summary>
    public void Bind()
    {
        if (_bound)
            return;
        _bound = true;
        var manager = NHotkeyManager.Instance;
        manager?.PushHotkeyPressedBinding(MegaInput.viewDrawPile, _tabLeft);
        manager?.PushHotkeyPressedBinding(MegaInput.viewDiscardPile, _tabRight);
        manager?.PushHotkeyPressedBinding(MegaInput.viewDeckAndTabLeft, _valueLeft);
        manager?.PushHotkeyPressedBinding(MegaInput.viewExhaustPileAndTabRight, _valueRight);
    }

    public void Unbind()
    {
        if (!_bound)
            return;
        _bound = false;
        var manager = NHotkeyManager.Instance;
        manager?.RemoveHotkeyPressedBinding(MegaInput.viewDrawPile, _tabLeft);
        manager?.RemoveHotkeyPressedBinding(MegaInput.viewDiscardPile, _tabRight);
        manager?.RemoveHotkeyPressedBinding(MegaInput.viewDeckAndTabLeft, _valueLeft);
        manager?.RemoveHotkeyPressedBinding(MegaInput.viewExhaustPileAndTabRight, _valueRight);
    }

    private void PollSticks()
    {
        if (_scroll is null || !_scroll.IsValid() || !_scroll.IsVisibleInTree() || TargetScroll is null)
            return;

        var direction = (Input.IsActionPressed(Controller.rStickUp) ? 1f : 0f)
            - (Input.IsActionPressed(Controller.rStickDown) ? 1f : 0f);
        if (direction == 0f)
            return;

        // Positive moves the content down the screen, which reads as scrolling up.
        TargetScroll(_scroll) += direction * ScrollSpeed * (float)PollInterval;
    }

    /// <summary>
    /// The scroll target is private and there is no public way to move it — the container
    /// only scrolls itself from input it handles, and it ignores controller input whenever
    /// anything holds focus, which on this screen is always. If the field is ever renamed
    /// this returns null and stick scrolling goes quiet rather than taking the screen down.
    /// </summary>
    private static AccessTools.FieldRef<NScrollableContainer, float>? SafeFieldRef()
    {
        try
        {
            return AccessTools.FieldRefAccess<NScrollableContainer, float>("_targetDragPosY");
        }
        catch (Exception exception)
        {
            MainFile.Logger.Warn($"Stick scrolling is unavailable: {exception.Message}");
            return null;
        }
    }

    public void Dispose()
    {
        Unbind();
        if (GodotObject.IsInstanceValid(_stickPoll))
            _stickPoll.QueueFree();
    }
}
