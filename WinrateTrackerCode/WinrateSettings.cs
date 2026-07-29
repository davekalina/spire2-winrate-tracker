using Godot;

namespace WinrateTracker.WinrateTrackerCode;

/// <summary>
/// The mod's own settings, kept in the game's user directory.
///
/// The game's <c>ModSettings</c> only records which mods are enabled — there is no
/// per-mod settings store to write into and no API for one — so this holds the choices
/// that have to outlive a session, in a small file of its own.
/// </summary>
internal static class WinrateSettings
{
    private const string Path = "user://winrate_tracker_settings.cfg";
    private const string Section = "winrate_tracker";
    private const string IgnoreEarlyAbandonsKey = "ignore_early_abandons";

    private static bool _loaded;
    private static bool _ignoreEarlyAbandons = true;

    /// <summary>
    /// Whether abandons before floor 2 are left out entirely.
    ///
    /// On by default. Every other abandon counts as a loss — quitting a run you were
    /// losing is not a different outcome from losing it — but an abandon on the first
    /// floor is a reroll of a starting hand, and counting it as a loss says something
    /// about the seed rather than about the play.
    /// </summary>
    public static bool IgnoreEarlyAbandons
    {
        get
        {
            Load();
            return _ignoreEarlyAbandons;
        }
        set
        {
            Load();
            if (_ignoreEarlyAbandons == value)
                return;
            _ignoreEarlyAbandons = value;
            Save();
            Changed?.Invoke();
        }
    }

    /// <summary>Raised when a setting changes, so an open screen can redraw.</summary>
    public static event Action? Changed;

    private static void Load()
    {
        if (_loaded)
            return;
        _loaded = true;

        var file = new ConfigFile();
        if (file.Load(Path) != Error.Ok)
            return;
        _ignoreEarlyAbandons = (bool)file.GetValue(Section, IgnoreEarlyAbandonsKey, true);
    }

    private static void Save()
    {
        var file = new ConfigFile();
        file.Load(Path);
        file.SetValue(Section, IgnoreEarlyAbandonsKey, _ignoreEarlyAbandons);
        if (file.Save(Path) != Error.Ok)
            MainFile.Logger.Warn("Could not write the Winrate Tracker settings file.");
    }
}
