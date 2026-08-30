# Winrate Tracker

A Slay the Spire 2 mod that reads your own run history and reports how often you actually
win — lately, and split by month, by patch, by run block, and by character.

It adds a **Win Rates** tile to the Compendium, beside Statistics and Run History.

![Home](images/01-WinrateTracker-Overview.jpg)

## What it does

Five tabs.

**Home** answers "how am I doing lately" without scrolling: your last fifty runs as the
headline figure, a trend chart behind it, the last ten runs one by one — hover any of them
for the whole run — and each character's recent form beside the others. Pressing a
character narrows the whole screen to them.

**Splits** cuts the same archive up by month, by patch and in fifty-run blocks, and will
graph any of them, with two more tables for time of day. **Characters** compares the five
against each other. **Cards** and **Relics** rank everything you have picked up by how
often the runs that took it went on to win.

Every table that reports a win rate draws it against your own average, so a number is
always a comparison rather than a figure on its own.

One filter row narrows the whole screen by ascension, character and time window; the pick
tabs add a minimum and a rarity on the end of the same row. It opens on the highest
ascension you have played.

The numbers come from the `.run` files the game already writes for every finished run, so
nothing needs enabling first and runs from before you installed the mod are included.
Reading them happens off the main thread and is cached, so the screen opens without a
stutter.

Everything works with mouse and keyboard or with a gamepad, and every filter is a single
stop on the pad.

## Installing

Subscribe on the [Steam Workshop][workshop]. Requires Slay the Spire 2 v0.110 or newer.

## Building

See [docs/development.md](docs/development.md).

## Licence

MIT — see [LICENSE](LICENSE).

That covers this mod's own source. It does not cover Slay the Spire 2, which is the
property of Mega Crit. The mod compiles against the game's assemblies and loads the game's
own scenes and textures at runtime from the player's installed copy; none of that is
redistributed here.

[workshop]: https://steamcommunity.com/sharedfiles/filedetails/?id=3775014144
