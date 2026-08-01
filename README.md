# Winrate Tracker

An informational Slay the Spire 2 mod that reads your own run history and reports how
often you actually win — rolling and all-time, split by month, patch, run block, and
character.

It adds a **Win Rates** tile to the Compendium, beside Statistics and Run History.

## The screen

Three tabs, cycled with the controller triggers like the native Statistics screen:

| Tab | What it answers |
| --- | --- |
| **Overview** | Record, win rate, current and longest streak, a rolling win rate over the last 10/25/50/100 runs, which act you lose in, and what kills you |
| **Splits** | The same archive cut four ways — by month, by patch, and into 10-run and 50-run blocks — each newest first, each carrying its own record and the cumulative win rate as of its end |
| **Characters** | Per-character record all time, over the last 50, and over the last 10 — plus a character-by-month grid |

Every table on **Splits** has a **Show Graph** button: a bar per period for the wins in
it, with the all-time win rate drawn over the top. Close it with its button or by
clicking anywhere outside.

Patches group by minor version, so a patch and its hotfixes (`v0.108.0`, `v0.108.1`)
report as one line rather than two small samples.

A filter row above the tabs narrows the whole screen by **ascension**, by **character**,
and by **time window** (all time, or the last 7/14/30/45/60/90/120 days). It opens on Ascension 10,
every character, all time, and remembers what you chose until the game closes. The window
is measured back from your most recent run rather than from the clock, so it does not
quietly empty itself while the game is left open.

## What counts as a run

An abandoned run counts as a loss. Quitting a run you were losing is not a different
outcome from losing it, and letting abandons vanish is the easiest way to flatter a win
rate without noticing. The exception is an abandon on the first floor, which is a reroll
rather than a run — the **gear beside the tabs** (or Settings → Mod Settings) drops those,
and is on by default.

Two rules are not adjustable and are stated on the screen. Co-op runs are always
excluded, because a shared win is not the same evidence about your play as a solo one.
And blocks are counted from the oldest run forward, so a block always covers the same ten
runs no matter how many you play afterwards — which is what lets each row carry a
meaningful running average.

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
logs `Winrate Tracker v0.6.0 initialized`.

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

## Licence

MIT — see [LICENSE](LICENSE).

That covers this mod's own source. It does not cover Slay the Spire 2, which is the
property of Mega Crit. The mod compiles against the game's assemblies and loads the
game's own scenes and textures at runtime from the player's installed copy; none of
that is redistributed here.
