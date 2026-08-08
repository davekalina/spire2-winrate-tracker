using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;

namespace WinrateTracker.WinrateTrackerCode;

[ModInitializer(nameof(Initialize))]
public partial class MainFile : Node
{
    /// <summary>
    /// The mod's permanent identity. It must equal the manifest id and the assembly name,
    /// because the loader loads "&lt;id&gt;.dll" from "&lt;game&gt;/mods/&lt;id&gt;/".
    /// Changing it after publishing orphans the Workshop item.
    /// </summary>
    public const string ModId = "WinrateTracker";

    /// <summary>Display name. Shown on the screen's byline.</summary>
    public const string ModName = "Winrate Tracker";

    /// <summary>Keep in step with the manifest version.</summary>
    public const string Version = "v1.2.0";

    public const string Author = "realtruegravy";

    public static MegaCrit.Sts2.Core.Logging.Logger Logger { get; } =
        new(ModId, MegaCrit.Sts2.Core.Logging.LogType.Generic);

    public static void Initialize()
    {
        new Harmony(ModId).PatchAll();
        GameText.Install();
        Logger.Info($"{ModName} {Version} initialized.");
    }
}
