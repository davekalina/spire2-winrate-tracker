# Winrate Tracker

An informational Slay the Spire 2 mod that will track win rate by character, ascension
level, and act reached.

## Status: stub

v0.1.0 loads, logs `Winrate Tracker v0.1.0 initialized`, and does nothing else. There is
no tracking logic, no persistence, and no UI. It exists so the repository, build,
install, and Workshop pipeline are all proven before any behavior is written.

See the **Intent** section of [`AGENTS.md`](AGENTS.md) for the design questions that need
answers first — chiefly where run outcomes are read from, where they are stored, and
which screen shows them.

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

Runtime diagnostics are in `%APPDATA%\SlayTheSpire2\logs\godot.log`. A successful start
logs `Winrate Tracker v0.1.0 initialized`.

## Publish to the Steam Workshop

```powershell
.\scripts\package-workshop.ps1
```

That stages `workshop/content/` and prints the `ModUploader.exe upload -w …` command to
run next. Get the uploader from
<https://github.com/megacrit/sts2-mod-uploader/releases>.

`workshop/mod_id.txt` appears after the first upload. **Commit it** — it is the only link
between this repository and the published Workshop item.

See `docs/sts2-modding.md` for the full pipeline and `workshop/README.md` for the
`workshop.json` field reference.
