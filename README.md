# Winrate Tracker

An informational Slay the Spire 2 mod that reads your own run history and reports how
often you actually win — rolling and all-time, sliced by run block, character, and month.

It adds a **Win Rates** tile to the Compendium, beside Statistics and Run History.

## The screen

Four tabs, cycled with the controller triggers like the native Statistics screen:

| Tab | What it answers |
| --- | --- |
| **Overview** | Record, win rate, current and longest streak, a rolling win rate over the last 10/25/50/100 runs, which act you lose in, and what kills you |
| **Blocks** | 10-run and 50-run blocks, newest first, each with its own record and the all-time win rate as of the end of that block |
| **Characters** | Per-character record, win rate, that character's own last ten runs, and average act — plus a character-by-month grid |
| **Months** | Per-month record with average floors, act, elites, and run length |

A filter row above the tabs narrows the whole screen by **ascension**, by **character**,
and by whether **abandoned** runs count. It opens on Ascension 10, every character,
finished runs only, and remembers what you chose until the game closes.

Two rules are not adjustable and are stated on the screen. Co-op runs are always
excluded, because a shared win is not the same evidence about your play as a solo one.
And blocks are counted from the oldest run forward, so a block always covers the same ten
runs no matter how many you play afterwards.

## Where the numbers come from

The game already records every finished run as a `.run` file under your profile's
`saves/history`. This mod reads those directly — the same files the Run History screen
pages through — so there is nothing to enable before a run counts, and runs played before
the mod was installed are included.

Reading roughly 30 MB of run files takes long enough to stutter a screen transition, so it
happens off the main thread and the screen fills in when it lands. Parsed runs are cached
for the session, so opening the screen again is instant.

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
logs `Winrate Tracker v0.3.0 initialized`.

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
