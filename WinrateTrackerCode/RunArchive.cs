using Godot;
using MegaCrit.Sts2.Core.Saves;

namespace WinrateTracker.WinrateTrackerCode;

/// <summary>
/// The run history on disk, read once and kept for the session.
///
/// The archive here is around 500 files and 28 MB, which takes roughly half a second to
/// read and parse — far too long to do on the main thread while a screen is opening. So
/// the read happens on a thread pool thread and the screen shows a loading line until it
/// lands.
///
/// Parsed records are cached by file name, so opening the screen a second time only
/// touches runs finished since the last read. The cache is never invalidated for a file
/// that still exists: a <c>.run</c> file is written once when the run ends and is not
/// edited afterwards.
/// </summary>
internal static class RunArchive
{
    private static readonly Dictionary<string, RunRecord> Cache = [];
    private static readonly SemaphoreSlim Gate = new(1, 1);

    /// <summary>Every run read so far, oldest first. Empty until the first load finishes.</summary>
    public static IReadOnlyList<RunRecord> Runs { get; private set; } = [];

    /// <summary>Files that would not parse. Surfaced on the screen so a bad file is not silent.</summary>
    public static int UnreadableFiles { get; private set; }

    /// <summary>Set once a load has completed, successfully or not.</summary>
    public static bool HasLoaded { get; private set; }

    /// <summary>Why the archive is empty, or null if it loaded.</summary>
    public static string? FailureReason { get; private set; }

    /// <summary>
    /// The absolute run history directory for the current profile.
    ///
    /// The game addresses saves through Godot's <c>user://</c> filesystem and joins the
    /// path with <see cref="System.IO.Path.Combine" />, which on Windows mixes in a
    /// backslash. Separators are normalised before globalising so the result is a real OS
    /// path that <see cref="System.IO" /> can enumerate off the main thread.
    ///
    /// Must be called on the main thread — it reads the current profile from SaveManager.
    /// </summary>
    public static string? ResolveHistoryDirectory()
    {
        try
        {
            var userPath = SaveManager.Instance
                .GetProfileScopedPath($"{UserDataPathProvider.SavesDir}/history")
                .Replace('\\', '/');
            return ProjectSettings.GlobalizePath(userPath);
        }
        catch (Exception exception)
        {
            MainFile.Logger.Warn($"Could not resolve the run history directory: {exception.Message}");
            return null;
        }
    }

    /// <summary>
    /// Read anything new and republish <see cref="Runs" />.
    ///
    /// Safe to call whenever the screen opens; concurrent calls queue rather than reading
    /// the directory twice.
    /// </summary>
    /// <param name="directory">From <see cref="ResolveHistoryDirectory" />, resolved on the main thread.</param>
    public static async Task RefreshAsync(string? directory)
    {
        await Gate.WaitAsync().ConfigureAwait(false);
        try
        {
            Load(directory);
        }
        finally
        {
            HasLoaded = true;
            Gate.Release();
        }
    }

    /// <summary>The directory the cache was filled from, so a profile switch is noticed.</summary>
    private static string? _loadedFrom;

    private static void Load(string? directory)
    {
        // Each save profile has its own history directory. Switching profiles has to
        // empty the cache, or the new profile is shown the old one's runs — which looks
        // like the screen simply failing to update.
        if (!string.Equals(_loadedFrom, directory, StringComparison.OrdinalIgnoreCase))
        {
            Cache.Clear();
            UnreadableFiles = 0;
            Runs = [];
            _loadedFrom = directory;
        }

        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            FailureReason = "No run history was found for this profile.";
            Runs = [];
            return;
        }

        string[] paths;
        try
        {
            paths = Directory.GetFiles(directory, "*.run");
        }
        catch (Exception exception)
        {
            MainFile.Logger.Warn($"Could not list the run history directory: {exception.Message}");
            FailureReason = "The run history could not be read.";
            Runs = [];
            return;
        }

        var present = new HashSet<string>(paths.Length);
        var unreadable = 0;

        foreach (var path in paths)
        {
            var name = Path.GetFileName(path);
            present.Add(name);
            if (Cache.ContainsKey(name))
                continue;

            RunRecord? record;
            try
            {
                record = RunParser.Parse(name, File.ReadAllText(path));
            }
            catch (Exception exception)
            {
                MainFile.Logger.Warn($"Could not read run history file {name}: {exception.Message}");
                record = null;
            }

            if (record is null)
                unreadable++;
            else
                Cache[name] = record;
        }

        // Forget runs whose files are gone: the game prunes the oldest files when the
        // cloud quota is reached, and a report that keeps counting them would drift.
        foreach (var stale in Cache.Keys.Where(name => !present.Contains(name)).ToList())
            Cache.Remove(stale);

        UnreadableFiles = unreadable;
        FailureReason = null;
        Runs = Cache.Values.OrderBy(run => run.StartTime).ToList();
    }

    /// <summary>Every ascension present in the archive, highest first. Drives the filter.</summary>
    public static IReadOnlyList<int> KnownAscensions() =>
        Runs.Select(run => run.Ascension).Distinct().OrderByDescending(ascension => ascension).ToList();

    /// <summary>Every character present in the archive, alphabetically. Drives the filter.</summary>
    public static IReadOnlyList<string> KnownCharacters() =>
        Runs.Select(run => run.Character)
            .Where(character => !string.IsNullOrEmpty(character))
            .Distinct()
            .OrderBy(character => character, StringComparer.Ordinal)
            .ToList();
}
