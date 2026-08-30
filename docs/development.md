# Building and publishing

## Build

```powershell
dotnet build .\WinrateTracker.csproj
dotnet test .\WinrateTracker.Tests\WinrateTracker.Tests.csproj
```

`Sts2PathDiscovery.props` finds the game through the Steam registry keys. If it cannot,
copy `Directory.Build.props.example` to `Directory.Build.props` and set `Sts2Path`.

Building copies `WinrateTracker.json`, `WinrateTracker.dll`, and `WinrateTracker.pdb` into
`<game>/mods/WinrateTracker/`. Pass `-p:SkipModInstall=true` to build without installing.
Close the game first, or the DLL will be locked.

Runtime diagnostics are in `%APPDATA%\SlayTheSpire2\logs\godot.log`, and a successful
start logs the mod's name and version. Read that file before anything else when the screen
misbehaves: Godot catches exceptions thrown inside signal handlers and `_Ready`, logs
them, and carries on, so a broken screen usually reports nothing on screen and everything
in there.

## Tests

Anything that does not touch Godot or the game's assemblies lives in its own file and is
linked into `WinrateTracker.Tests` — parsing, filtering, the aggregations, and the exact
text of every table cell. That means the whole screen's contents can be asserted without
launching the game, and it is worth keeping that way.

## Checks worth running before shipping

Three failures in this mod's history were invisible to the compiler and only showed up as
a screen that silently did nothing:

1. **Harmony targets and reflected field names.** `PatchAll` throws on a missing method
   but nothing catches a wrong field name. A `MetadataLoadContext` pass over `sts2.dll`
   settles both.
2. **Scene casts.** A scene's *root* must carry the script being cast to —
   `screens/paginator` does not. Check the scripted *children* too: `Node.GetParent<T>()`
   is a hard cast, and some widgets use it on their parent.
3. **Resource paths.** `GameArt` and `NativeStyle` load the game's own textures and font
   variations by path, and a path is not checked by the compiler. `GameArt` asks
   `ResourceLoader.Exists` before loading and drops the key on a miss, so a moved texture
   costs an icon rather than a screen — but check the paths against the shipped `.pck`
   after a game update rather than waiting to notice a blank column.
4. **The game log**, as above.

## Publish to the Steam Workshop

```powershell
.\scripts\package-workshop.ps1
```

That stages `workshop/content/` and prints the `ModUploader.exe upload -w …` command to
run next. Get the uploader from
<https://github.com/megacrit/sts2-mod-uploader/releases>.

Bump `version` in `WinrateTracker.json` **and** `Version` in `WinrateTrackerCode/MainFile.cs`
together, write a real `changeNote` in `workshop/workshop.json`, and push to GitHub before
uploading, so the published version always corresponds to a commit.

`workshop/mod_id.txt` is the only link between this repository and the published Workshop
item. Losing it orphans the item and makes the next upload a duplicate.

See `docs/sts2-modding.md` for the platform reference and `workshop/README.md` for the
`workshop.json` field reference.
