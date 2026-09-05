using Godot;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Platform;

namespace WinrateTracker.WinrateTrackerCode;

/// <summary>
/// The name filter on the Cards and Relics tabs.
///
/// Those two tables are the only ones on the screen with hundreds of rows, and the only
/// ones where you usually arrive knowing what you are looking for. Rarity and a minimum
/// narrow them by kind; this narrows them by name.
///
/// It is a plain <see cref="LineEdit" /> wearing the combo boxes' skin, because the game
/// has no drop-in search field to duplicate — its card library uses an
/// <c>NMegaLineEdit</c>, which is a script on a scene node, and a mod assembly cannot
/// declare a Godot script type. What that script does is four lines of input handling, and
/// those are reproduced here against the same game APIs: it is the part that matters,
/// because it is what makes the field usable on a controller.
/// </summary>
internal sealed class SearchBox
{
    private const int FontSize = 20;
    private const int PaddingX = 14;
    private const float MinWidth = 160f;

    /// <summary>
    /// How long typing has to stop before the tables are rebuilt.
    ///
    /// Every keystroke used to redraw the whole tab, and on the Cards tab with no character
    /// filter that is several hundred rows torn down and built again — for a search that is
    /// about to change on the next letter anyway. Long enough to swallow a burst of typing,
    /// short enough that finishing a word feels like it filtered as you typed.
    /// </summary>
    private const double SettleSeconds = 0.2d;

    private readonly LineEdit _field;
    private readonly Godot.Timer _settle;

    public SearchBox(float height)
    {
        _field = new LineEdit
        {
            CustomMinimumSize = new Vector2(MinWidth, height),
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            FocusMode = Control.FocusModeEnum.All,
            MouseDefaultCursorShape = Control.CursorShape.Ibeam,
            CaretBlink = true,
            // The clear button is the only way back to the whole list without deleting
            // character by character, and on a pad it is not reachable at all — so cancel
            // does the same thing; see OnInput.
            ClearButtonEnabled = true,
        };

        _field.AddThemeFontOverride("font", NativeStyle.BodyFont);
        _field.AddThemeFontSizeOverride("font_size", FontSize);
        _field.AddThemeColorOverride("font_color", NativeStyle.CellColor);
        _field.AddThemeColorOverride("font_placeholder_color", NativeStyle.ColumnHeaderColor);
        _field.AddThemeColorOverride("caret_color", NativeStyle.HeaderColor);
        _field.AddThemeStyleboxOverride("normal", Box(lit: false));
        _field.AddThemeStyleboxOverride("focus", Box(lit: true));

        // Rebuilding on a timer rather than on the keystroke. The rebuild is heavy enough
        // on a long list to be felt while typing, and every intermediate letter produces a
        // table nobody looks at.
        _settle = new Godot.Timer { OneShot = true, WaitTime = SettleSeconds };
        _settle.Connect(Godot.Timer.SignalName.Timeout, Callable.From(() => Changed?.Invoke()));
        _field.AddChild(_settle);

        _field.Connect(
            LineEdit.SignalName.TextChanged,
            Callable.From<string>(_ => _settle.Start()));
        _field.Connect(Control.SignalName.GuiInput, Callable.From<InputEvent>(OnInput));
    }

    /// <summary>The control the focus chain walks to.</summary>
    public Control Root => _field;

    /// <summary>Raised on every keystroke. The screen renarrows the list as you type.</summary>
    public event Action? Changed;

    public string Text => _field.Text.Trim();

    /// <summary>What the field says when it is empty — "Search cards", "Search relics".</summary>
    public void SetPlaceholder(string text) => _field.PlaceholderText = text;

    /// <summary>
    /// Empty the field. Used when the tab changes: the two tabs are different lists and a
    /// name typed for one is rarely a name in the other.
    /// </summary>
    public void Clear()
    {
        if (!_field.IsValid() || _field.Text.Length == 0)
            return;
        _field.Text = "";
        Settled();
    }

    /// <summary>
    /// Rebuild now rather than on the timer. Emptying the field is not typing — there is no
    /// next letter coming — and waiting to show the whole list back reads as a stall.
    /// </summary>
    private void Settled()
    {
        if (_settle.IsValid())
            _settle.Stop();
        Changed?.Invoke();
    }

    private static StyleBoxFlat Box(bool lit) => new()
    {
        BgColor = new Color(1f, 1f, 1f, 0.04f),
        BorderColor = lit ? NativeStyle.FocusColor : NativeStyle.BorderColor,
        BorderWidthLeft = 2,
        BorderWidthTop = 2,
        BorderWidthRight = 2,
        BorderWidthBottom = 2,
        CornerRadiusTopLeft = 8,
        CornerRadiusTopRight = 8,
        CornerRadiusBottomLeft = 8,
        CornerRadiusBottomRight = 8,
        ContentMarginLeft = PaddingX,
        ContentMarginRight = PaddingX,
        ShadowColor = lit ? NativeStyle.FocusColor with { A = 0.4f } : new Color(0, 0, 0, 0),
        ShadowSize = lit ? 12 : 0,
    };

    /// <summary>
    /// Make the field work on a controller, the way the game's own line edit does.
    ///
    /// A focused text field on a pad is useless without two things: something that starts
    /// text entry — and raises the on-screen keyboard, which is the only way to type on a
    /// Deck or a couch — and something that stops it again. Select does the first. Cancel
    /// does the second, and has to swallow the event: unhandled, it would reach the back
    /// button and dismiss the whole screen from under the field being edited.
    ///
    /// Cancel on a field that is <em>not</em> being edited is left alone, so it still backs
    /// out of the screen from there.
    /// </summary>
    private void OnInput(InputEvent input)
    {
        if (input is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true }
            || input.IsActionPressed(MegaInput.select))
        {
            Open();
            return;
        }

        if (!input.IsActionPressed(MegaInput.cancel) || !_field.IsEditing())
            return;

        // Cancel clears before it closes. On a pad the clear button cannot be reached, so
        // without this a search typed by mistake could only be undone one character at a
        // time — and an empty field is what the player wants nine times out of ten.
        if (_field.Text.Length > 0)
            _field.Text = "";
        _field.Unedit();
        _field.GetViewport()?.SetInputAsHandled();
        PlatformUtil.CloseVirtualKeyboard();
        Settled();
    }

    private void Open()
    {
        _field.Edit();
        PlatformUtil.OpenVirtualKeyboard();
    }
}
