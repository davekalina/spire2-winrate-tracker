using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;

namespace WinrateTracker.WinrateTrackerCode;

/// <summary>
/// Lets this mod hear when one of its own paginators is paged.
///
/// <c>NPaginator</c> reports a change by calling its own <c>protected virtual
/// OnIndexChanged</c>, which the game's own paginators override. A mod cannot override it
/// — a subclass declared in a mod assembly has no registered Godot script — so the change
/// has to be observed from outside.
///
/// The hooks are <c>PageLeft</c> and <c>PageRight</c> rather than <c>OnIndexChanged</c>
/// itself: the base <c>OnIndexChanged</c> is an empty method and empty methods are the
/// ones the JIT is most likely to inline out from under a patch. Paging is the only way
/// the player changes a paginator; this mod's own programmatic <c>SetIndex</c> calls
/// notify themselves directly.
///
/// Only paginators registered through <see cref="Listen" /> are affected, so the settings
/// screen's paginators behave exactly as before.
/// </summary>
[HarmonyPatch(typeof(NPaginator))]
internal static class PaginatorPatch
{
    private static readonly Dictionary<ulong, Action<int>> Listeners = [];

    private static readonly AccessTools.FieldRef<NPaginator, int> CurrentIndex =
        AccessTools.FieldRefAccess<NPaginator, int>("_currentIndex");

    private static readonly AccessTools.FieldRef<NPaginator, List<string>> Options =
        AccessTools.FieldRefAccess<NPaginator, List<string>>("_options");

    public static void Listen(NPaginator paginator, Action<int> onChanged) =>
        Listeners[paginator.GetInstanceId()] = onChanged;

    public static void Forget(NPaginator paginator) =>
        Listeners.Remove(paginator.GetInstanceId());

    /// <summary>Replace the options a paginator pages through.</summary>
    public static void SetOptions(NPaginator paginator, IEnumerable<string> options)
    {
        var list = Options(paginator);
        list.Clear();
        list.AddRange(options);
    }

    public static int IndexOf(NPaginator paginator) => CurrentIndex(paginator);

    /// <summary>The option text at an index, or empty if the list is shorter than that.</summary>
    public static string OptionAt(NPaginator paginator, int index)
    {
        var options = Options(paginator);
        return index >= 0 && index < options.Count ? options[index] : "";
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(NPaginator.PageLeft))]
    private static void AfterPageLeft(NPaginator __instance) => Notify(__instance);

    [HarmonyPostfix]
    [HarmonyPatch(nameof(NPaginator.PageRight))]
    private static void AfterPageRight(NPaginator __instance) => Notify(__instance);

    private static void Notify(NPaginator paginator)
    {
        if (Listeners.TryGetValue(paginator.GetInstanceId(), out var onChanged))
            onChanged(CurrentIndex(paginator));
    }
}
