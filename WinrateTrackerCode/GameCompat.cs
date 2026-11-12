using System.Reflection;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace WinrateTracker.WinrateTrackerCode;

/// <summary>
/// The places where the game builds this mod supports disagree, answered for all of them,
/// so one DLL loads on every build the manifest admits: 0.111 and 0.113 (the beta). Main
/// is still 0.107.1, below min_game_version, so for now that means the beta only.
///
/// Each member here exists because a direct call compiled against one build fails on the
/// other. That failure is not a compile error anyone sees. It is a
/// <see cref="MissingMethodException" /> thrown when the runtime first compiles the
/// <em>calling</em> method — so the whole method fails, not just the line — and Godot
/// catches it, logs it, and carries on. Anything that differs between builds goes through
/// here, by name, and takes whichever form the running game has.
/// </summary>
internal static class GameCompat
{
    // ---- Hotkey bindings ------------------------------------------------------------
    //
    // 0.113 gave NHotkeyManager's bindings a priority sequence: Push takes a trailing
    // `long sequence = 0` and Remove returns the sequence it removed. Optional parameters
    // are filled in by the compiler, not the runtime, so a call compiled against either
    // signature is a MissingMethodException on the other — which on 0.113 took out
    // ComboBox.Open and ModalPanel.Open whole: no filter drop-down, graph, or settings.

    private static readonly MethodInfo? PushReleased = Binding("PushHotkeyReleasedBinding");
    private static readonly MethodInfo? RemoveReleased = Binding("RemoveHotkeyReleasedBinding");

    /// <summary>Bind <paramref name="action" /> to <paramref name="hotkey" /> at top priority.</summary>
    public static void PushHotkeyReleasedBinding(NHotkeyManager hotkeys, string hotkey, Action action) =>
        InvokeBinding(PushReleased, "PushHotkeyReleasedBinding", hotkeys, hotkey, action);

    /// <summary>Undo <see cref="PushHotkeyReleasedBinding" />.</summary>
    public static void RemoveHotkeyReleasedBinding(NHotkeyManager hotkeys, string hotkey, Action action) =>
        InvokeBinding(RemoveReleased, "RemoveHotkeyReleasedBinding", hotkeys, hotkey, action);

    private static MethodInfo? Binding(string name) =>
        typeof(NHotkeyManager)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name == name)
            .Where(m => m.GetParameters() is [{ } a, { } b, ..] &&
                a.ParameterType == typeof(string) && b.ParameterType == typeof(Action))
            .OrderBy(m => m.GetParameters().Length)
            .FirstOrDefault();

    private static bool _warnedMissing;

    /// <summary>
    /// Any parameter past the first two gets its type's default. For 0.113's sequence that
    /// is 0, meaning "a fresh binding at top priority", which is exactly what the
    /// two-argument call meant before the parameter existed.
    ///
    /// A build with neither form costs the popups their Escape/B binding, not the popups:
    /// they still close from their own button and a click outside.
    /// </summary>
    private static void InvokeBinding(MethodInfo? method, string name, NHotkeyManager hotkeys,
        string hotkey, Action action)
    {
        if (method is null)
        {
            if (!_warnedMissing)
                MainFile.Logger.Warn($"NHotkeyManager.{name}(string, Action) is missing; popups will not close on Escape.");
            _warnedMissing = true;
            return;
        }
        var parameters = method.GetParameters();
        var args = new object?[parameters.Length];
        args[0] = hotkey;
        args[1] = action;
        for (var i = 2; i < parameters.Length; i++)
            args[i] = parameters[i].ParameterType.IsValueType
                ? Activator.CreateInstance(parameters[i].ParameterType)
                : null;
        method.Invoke(hotkeys, args);
    }
}
