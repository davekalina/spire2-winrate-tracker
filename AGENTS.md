# Winrate Tracker — repository instructions

This repository is one Slay the Spire 2 mod. These instructions apply throughout it.
A more specific `AGENTS.md` may add to or override them for its directory.

`docs/sts2-modding.md` describes how the game's mod loader, manifest, and Steam Workshop
pipeline work. Those are platform facts. This file is how I want the work done.

## This mod

| | |
| --- | --- |
| Mod id | `WinrateTracker` |
| Display name | Winrate Tracker |
| Installs to | `<game>/mods/WinrateTracker/` |
| Manifest | `WinrateTracker.json` |
| Version is also printed in | `WinrateTrackerCode/MainFile.cs` |
| Gameplay | Informational only; `affects_gameplay` is `false` |
| Dependencies | None yet |

### What it does

Adds a **Win Rates** tile to the Compendium bottom row, opening a four-tab screen —
Overview, Blocks, Characters, Months — over the player's own run history, narrowed by a
filter row (ascension, character, abandoned runs).

### How it is built, and why

- **Run outcomes are read, not observed.** The game writes a `.run` file per finished run
  under the profile's `saves/history`. `RunArchive` reads that directory directly rather
  than hooking run-end, so runs from before the mod was installed count and there is no
  state of our own to keep in sync. It also means nothing is persisted: there is no mod
  save file, and none is wanted.
- **The files are parsed by hand, not through `SaveManager.LoadRunHistory`.** That path
  runs save migrations and deserializes the whole run — deck, relics, every per-floor
  stat block — to answer a question that needs a dozen scalars. `RunParser` reads the raw
  JSON instead. The archive already spans schema v8 and v9 and the fields it reads have
  been stable across both.
- **The screen is a second instance of the native Statistics screen**, contents replaced.
  It has to be a real `NSubmenu` to go on the submenu stack, and a mod assembly cannot
  declare one — a subclass of a Godot script type needs a registered script, which only
  the game's own scenes have. Borrowing the scene also inherits its back button, Escape
  and controller dismissal, scroll gradient, scrollbar, and trigger tab cycling.
- **Anything game-independent lives in its own file** and is linked into
  `WinrateTracker.Tests`: `RunRecord`, `RunParser`, `RunFilter`, `WinrateReport`,
  `Format`, `ReportTables`. `ReportTables` decides the exact text of every cell, so the
  whole screen's contents are assertable without launching the game. Keep it that way —
  when adding a table, add it there, not in the renderer.

Settled decisions worth not relitigating:

- **Co-op runs are always excluded.** A shared win is not the same evidence about your
  play as a solo one. The screen says so rather than dropping them silently.
- **Abandoned runs are excluded by default**, because abandoning is a decision to stop,
  not a loss. The filter row can include them.
- **Blocks are anchored at the oldest run**, not the newest, so a block always covers the
  same ten runs and the cumulative column means something.

### Surfaces to audit

Any directional change must be applied across all of these.

- The Compendium tile (`CompendiumTilePatch`) — label, icon, tint, focus neighbours.
- The filter row (`FilterBar`) — the three paginators and the summary line under them.
- All four tabs (`ReportTables`) — a wording, rounding, or column change belongs in every
  table it applies to, not just the one that was reported.
- The empty and loading states (`WinrateScreen.EmptyMessage`, `SummaryText`).

## Mod UI: match the game

Treat the game's existing UI as the design system for this mod.

- Prefer duplicating or instantiating native game scenes, controls, textures,
  frames, hover tips, labels, buttons, selection outlines, and animations.
- Use the game's `MegaLabel`/`MegaRichTextLabel` fonts and theme values. Do not
  introduce generic Godot controls, arbitrary fonts, improvised colors, or
  custom panel styling when an equivalent game element exists.
- Before creating a UI element, inspect the installed game/decompiled source
  and find the closest native screen or widget to use as the reference.
- Preserve native interaction behavior: hover animation, click handling,
  focus behavior, tooltips, dismissal, input capture, and selection feedback.
- If a native element is duplicated outside its original container, reset all
  inherited anchors, offsets, scale, rotation, minimum size, and mouse filters,
  then explicitly center its label after it has a real layout size.
- Selection state must be visually unmistakable and should reuse the relevant
  native selection outline or reticle. Do not layer multiple independent
  selection indicators.
- Prefer native hover tips for contextual details instead of persistent tiny
  overlays. Persistent information belongs in the screen's native information
  strip or another established game surface.

UI work is not complete until it has been inspected in-game at the target
resolution and checked for overlap, centering, clipping, input leakage, hover
behavior, and consistency with adjacent native UI.

## Dense information: use structure and alignment

When presenting several related values, optimize for scanning.

- Prefer tables or explicit columns over prose-like runs of labels and values.
- Keep row labels left-aligned and numeric values right-aligned.
- Give numeric columns enough fixed or proportional width that values line up
  against the right side of their containing panel.
- Center controls within their rows and center text within button bounds.
- Use concise visible labels when space is limited and put the longer
  explanation in a native tooltip.
- Avoid placing important text over detailed card art. Use native dark
  backgrounds, information strips, or tooltip frames for legibility.

## Directional changes apply across the whole mod

A product or visual direction is a mod-wide rule unless the request explicitly
limits it to one screen.

Do not fix only the screenshot or screen where the inconsistency was reported.
Search for every implementation of the old wording, style, calculation, or
interaction and update or intentionally exempt each one. Keep shared behavior
in shared helpers where practical so screens cannot silently diverge.

For every directional change, audit all equivalent surfaces of this mod — see
**Surfaces to audit** above — including buttons, labels, selection state, hover
tips, persistent overlays, bottom information text, empty states, and
keyboard/controller dismissal.

## Version, verification, and GitHub

Every new mod version must be committed and pushed to this repository during the
same task.

1. Keep the manifest version and any displayed/logged version in sync.
2. Update user-facing documentation when behavior or controls change.
3. Build the mod and run its test suite before committing.
4. Check `git diff --check` and review the complete scoped diff.
5. Commit only the intended project files; preserve unrelated and untracked
   user files.
6. Push the active branch to GitHub and report the commit hash.

Do not describe a version as complete if it exists only in the working tree.
A local game deployment is separate from a GitHub release. Always deploy a
verified mod build when the game is not running, even if the user did not
separately request deployment. Verify the installed manifest and DLL after
copying. If the game is running, do not terminate it to replace a locked DLL;
report that deployment is pending instead.

## Steam Workshop

The `workshop/` directory is this mod's uploader workspace. See
`docs/sts2-modding.md` for what each file means.

- Never change the manifest `id` once the mod has been published. It is the
  Workshop item's identity, the install folder name, and the DLL filename.
- `workshop/mod_id.txt` is created by the uploader on the first publish and must
  be committed. Losing it orphans the published item and the next upload creates
  a duplicate.
- Stage uploads with `scripts/package-workshop.ps1`. It rebuilds Release and
  populates `workshop/content/`. Do not hand-copy files, and do not ship the
  `.pdb`.
- Fill in `changeNote` in `workshop/workshop.json` before every update; it is
  the changelog subscribers see.
- Bump the manifest version and push to GitHub before uploading, so the
  published version always corresponds to a commit.
