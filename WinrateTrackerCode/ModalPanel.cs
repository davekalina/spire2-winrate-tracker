using Godot;
using MegaCrit.Sts2.Core.Helpers;

namespace WinrateTracker.WinrateTrackerCode;

/// <summary>
/// A dimmed backdrop with a panel centred on it, and two ways out.
///
/// Shared by the graph and the settings popups so they cannot drift apart on how they
/// dismiss. Both close on their button and on a click anywhere outside the panel —
/// clicking away is what people try first, and it has to work before the button is found.
/// </summary>
internal sealed class ModalPanel
{
    private readonly Control _backdrop;

    private ModalPanel(Control backdrop, Control panel, Control content)
    {
        _backdrop = backdrop;
        Panel = panel;
        Content = content;
    }

    /// <summary>The centred panel. Position children against this.</summary>
    public Control Panel { get; }

    /// <summary>The panel's inner area, clear of the title and the close button.</summary>
    public Control Content { get; }

    public static ModalPanel Open(Control host, string title, float width, float height)
    {
        var backdrop = new ColorRect
        {
            Name = "WinrateModal",
            Color = StsColors.screenBackdrop,
            // Stop, so what is underneath cannot be clicked through the modal.
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        host.AddChild(backdrop);

        var panel = new Control
        {
            CustomMinimumSize = new Vector2(width, height),
            // Stop, so a click on the panel does not reach the backdrop and close it.
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        panel.SetAnchorsPreset(Control.LayoutPreset.Center);
        panel.OffsetLeft = -width / 2f;
        panel.OffsetRight = width / 2f;
        panel.OffsetTop = -height / 2f;
        panel.OffsetBottom = height / 2f;

        var background = new ColorRect
        {
            Color = NativeStyle.PanelColor with { A = 0.98f },
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        background.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        panel.AddChild(background);

        var heading = NativeStyle.Header(title);
        heading.Position = new Vector2(ContentInsetLeft, 26f);
        panel.AddChild(heading);

        var content = new Control { MouseFilter = Control.MouseFilterEnum.Pass };
        content.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        content.OffsetLeft = ContentInsetLeft;
        content.OffsetRight = -ContentInsetRight;
        content.OffsetTop = ContentInsetTop;
        content.OffsetBottom = -ContentInsetBottom;
        panel.AddChild(content);

        backdrop.AddChild(panel);
        var modal = new ModalPanel(backdrop, panel, content);

        var close = NativeStyle.TextButton("Close", modal.Close);
        close.Position = new Vector2(width - ContentInsetRight - CloseButtonWidth, 22f);
        panel.AddChild(close);

        backdrop.Connect(
            Control.SignalName.GuiInput,
            Callable.From<InputEvent>(input =>
            {
                if (input is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
                    modal.Close();
            }));

        return modal;
    }

    public const float ContentInsetLeft = 128f;
    public const float ContentInsetRight = 128f;
    public const float ContentInsetTop = 152f;
    public const float ContentInsetBottom = 152f;

    private const float CloseButtonWidth = 224f;

    public bool IsOpen => GodotObject.IsInstanceValid(_backdrop);

    public void Close()
    {
        if (GodotObject.IsInstanceValid(_backdrop))
            _backdrop.QueueFree();
    }
}
