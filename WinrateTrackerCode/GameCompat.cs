using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace WinrateTracker.WinrateTrackerCode;

/// <summary>
/// The places where the game builds this mod supports disagree, answered for all of them,
/// so one DLL loads on every build the manifest admits: 0.111 (main), 0.114 (the current
/// beta), and 0.113 (the previous beta).
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
    // NHotkeyManager's binding methods have had three shapes:
    //   0.111  void Push…Binding(string hotkey, Action action)
    //          void Remove…Binding(string hotkey, Action action)
    //   0.113  void Push…Binding(string hotkey, Action action, long sequence = 0)
    //          long Remove…Binding(string hotkey, Action action)
    //   0.114  void Push…Binding(string hotkey, Node owner, Action action, long sequence = 0)
    //          long Remove…Binding(string hotkey, Node owner, Action action)
    // Optional parameters are filled in by the compiler, not the runtime, so a call
    // compiled against any one of them is a MissingMethodException on the others — which
    // on 0.113 took out ComboBox.Open and ModalPanel.Open whole: no filter drop-down,
    // graph, or settings.
    //
    // On 0.114 a binding fires only while its owner is in the tree and visible, and
    // Remove matches on owner and action together. So the owner is the popup's own
    // backdrop, and Remove must be given the same one. Builds without an owner parameter
    // ignore it.

    private static readonly MethodInfo? PushReleased = Binding("PushHotkeyReleasedBinding");
    private static readonly MethodInfo? RemoveReleased = Binding("RemoveHotkeyReleasedBinding");

    /// <summary>
    /// Bind <paramref name="action" /> to <paramref name="hotkey" /> at top priority, live
    /// while <paramref name="owner" /> is showing.
    /// </summary>
    public static void PushHotkeyReleasedBinding(
        NHotkeyManager hotkeys, string hotkey, Node owner, Action action) =>
        InvokeBinding(PushReleased, "PushHotkeyReleasedBinding", hotkeys, hotkey, owner, action);

    /// <summary>Undo <see cref="PushHotkeyReleasedBinding" />, given the same owner.</summary>
    public static void RemoveHotkeyReleasedBinding(
        NHotkeyManager hotkeys, string hotkey, Node owner, Action action) =>
        InvokeBinding(RemoveReleased, "RemoveHotkeyReleasedBinding", hotkeys, hotkey, owner, action);

    /// <summary>
    /// The public overload whose parameters start <c>(string, Action, …)</c> or
    /// <c>(string, Node, Action, …)</c>, fewest parameters first.
    /// </summary>
    private static MethodInfo? Binding(string name) =>
        typeof(NHotkeyManager)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name == name && IsBindingShape(m.GetParameters()))
            .OrderBy(m => m.GetParameters().Length)
            .FirstOrDefault();

    private static bool IsBindingShape(ParameterInfo[] parameters) =>
        parameters.Length >= 2 &&
        parameters[0].ParameterType == typeof(string) &&
        (parameters[1].ParameterType == typeof(Action) ||
         parameters.Length >= 3 &&
         parameters[1].ParameterType == typeof(Node) &&
         parameters[2].ParameterType == typeof(Action));

    private static readonly HashSet<string> Warned = [];

    /// <summary>
    /// Arguments are filled by type: the string is the hotkey, the Node the owner, the
    /// Action the action, and anything else gets its type's default. For the sequence that
    /// is 0, meaning "a fresh binding at top priority", which is exactly what the
    /// two-argument call meant before the parameter existed.
    ///
    /// A build with no usable form costs the popups their Escape/B binding, not the popups:
    /// they still close from their own button and a click outside. So a failure is logged
    /// once and never thrown, and a close always runs to the end.
    /// </summary>
    private static void InvokeBinding(MethodInfo? method, string name, NHotkeyManager hotkeys,
        string hotkey, Node owner, Action action)
    {
        if (method is null)
        {
            if (Warned.Add(name))
                MainFile.Logger.Warn($"NHotkeyManager.{name} has no known form; popups will not close on Escape.");
            return;
        }
        var parameters = method.GetParameters();
        var args = new object?[parameters.Length];
        for (var i = 0; i < parameters.Length; i++)
        {
            var type = parameters[i].ParameterType;
            args[i] =
                type == typeof(string) ? hotkey :
                type == typeof(Node) ? owner :
                type == typeof(Action) ? action :
                type.IsValueType ? Activator.CreateInstance(type) : null;
        }
        try
        {
            method.Invoke(hotkeys, args);
        }
        catch (TargetInvocationException error)
        {
            if (Warned.Add(name))
                MainFile.Logger.Warn($"NHotkeyManager.{name} failed; popups may not close on Escape.\n{error.InnerException ?? error}");
        }
    }
}
