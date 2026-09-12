# InventoryTweaks

A 7 Days To Die mod for **Undead Legacy**. Three small quality-of-life fixes for UL's backpack:

- **Sorting scrolls to the top.** Clicking a sort button re-sorts and jumps the list back to the
  first page, so the sorted result is actually in view. Also in loot containers and vehicle storage.
- **Right-click a sort button to lock it.** The button lights up, the backpack sorts, and from then
  on it re-sorts every time you open the inventory. Right-click it again to unlock. The lock is
  remembered across restarts.
- **New and changed items stay highlighted until you look at them.** Anything that lands in
  your backpack, or any stack whose count changes, gets a coloured frame that goes away when you
  hover the cell or close the inventory. Stackables also show how much the count moved: `+ 47`,
  `++ 47`, `+++ 47` (or `-`, `--`, `---`) in front of the number.

## Installing

Download the zip from [Releases](https://github.com/johnrando/ul-inventorytweaks/releases) and
extract it into the game's `Mods/`. The mod folder is the root of the archive, so it lands as:

```
Mods/InventoryTweaks/
├── ModInfo.xml
└── InventoryTweaks.dll
```

**Undead Legacy is required.** The mod patches UL's own inventory window; without UL it logs a
warning at startup and does nothing. Load order does not matter, and nothing needs building.

## Console commands

`it` prints the menu and changes nothing — `inventorytweaks` is an alias. Every line names the
command that changes it and says what it is for, so the menu is also the reference:

```
InventoryTweaks is ON
  it on|off                 : [ >on< | off ]                    - master switch
  it scroll                 : [ >on< | off ]                    - sorting scrolls the backpack to the top
  it containers             : [ >on< | off ]                    - ...and loot / vehicle windows too
  it lock {mode}            : [ >none< | weight | price | group | name ] - locked sort - or right-click a sort button
  it autosort               : [ >on< | off ]                    - re-sort on open while locked
  it highlight              : [ >on< | off ]                    - frame new or changed items until hovered
  it color {r,g,b,a}        : 255,200,60,255
  it markers                : [ >on< | off ]                    - +/- before a changed stack count
```

Three more: `it clear` drops every highlight, `it info` prints the same block with the patch
state and counters added, and `it reset` zeroes those counters. **Changes are saved** — see
[Settings file](#settings-file).

## What it does, precisely

**Sort to top.** UL's backpack is a 300-cell grid shown eight rows at a time; its scrollbar is
really a page number. After any of UL's sorts, the page is set back to 0. Nothing about the sort
itself changes.

**Locked sort.** Right-click reaches a different event from the one UL's sorter listens on, so UL's
own left-click behaviour is untouched. The sort on open is UL's own sort, run through UL's own
sorter, so slots you have locked with the slot-lock key stay put and the search filter is honoured
exactly as a left-click would. It runs only when it cannot get in your way:

- on a real open, not when UL closes and reopens the window as you switch between the crafting,
  character and other tabs;
- when nothing is on your cursor;
- when the sort buttons are not greyed out by UL's *Shuffled Backpack* debuff.

**New-item highlight.** The bag only reports "something changed", so the mod keeps a fingerprint
of every slot (item, quality, seed) plus the count you last acknowledged, and diffs the bag on
each change. Items that merely moved — by a sort, a drag, a shift-click — carry their state with
them; merging two stacks adds their acknowledged counts together and splitting one divides it, so
only stock that came from (or went) outside the backpack registers as a change. A stack nobody
had before is highlighted with no marker.

A highlight clears when you hover the cell, or when you close the inventory — a real close, not
UL's close-and-reopen as you switch tabs. Anything that arrives while the inventory is closed is
still highlighted when you next open it. Being on screen does nothing by itself; a cell you never
hover stays highlighted until you close the window.

**Count markers.** For a stackable whose count differs from what you last acknowledged, the
number is prefixed: one `+` or `-` for a change of a single item, three once the stack has reached
three times the acknowledged count (or fallen to a third of it), two for anything in between.
Hovering resets the acknowledged count to the current one.

Hover, selection, drag and UL's search overlay all draw over the frame unchanged.

## Defaults

| Setting | Default |
|---|---|
| scroll to top on sort | on |
| ...in loot / vehicle windows | on |
| locked sort | none |
| re-sort on open while locked | on |
| new-item highlight | on |
| highlight colour | 255,200,60,255 |
| count markers | on |

## Settings file

Every setting survives a restart. A change made with `it`, or by right-clicking a sort button, is
written straight out to:

```
%APPDATA%/7DaysToDie/InventoryTweaks/settings.txt
```

— the game's own user data folder, next to `Saves`, rather than `Mods/InventoryTweaks/`, so
updating the mod does not take your settings with it. `it info` prints the full path and whether
the last read or write worked.

It is plain `key = value` text, one line per setting, each naming the command that sets it:

```
enabled       = on               # it on|off
scroll        = on               # it scroll
containers    = on               # it containers
lock          = none             # it lock {none|weight|price|group|name} - or right-click a sort button
autosort      = on               # it autosort
highlight     = on               # it highlight
color         = 255,200,60,255   # it color {r,g,b,a}
markers       = on               # it markers
```

Edit it by hand with the game closed — it is rewritten whenever a setting changes. A line that
will not parse is logged and ignored rather than fatal, and deleting the file brings back the
defaults above (which live in `Settings.cs`).

## Undead Legacy

**Required.** Tested against **UL 2.7.30**. Every patch targets a UL class by name and reports
whether it applied in the startup log and in `it info`; a UL update that renames one of them
degrades to a log line, not a crash. The mod warns, but still installs, when it sees a UL build
outside the range it was tested on.

## Limitations

- **Highlights are not saved.** They live in memory and reset when the game is restarted. The
  sort lock is saved.
- **Backpack only.** Items that land straight in the toolbelt are not highlighted.
- Highlights are per client. On a dedicated server the mod runs on each player's own machine and
  only sees that player's backpack.

## Building

Requires the .NET SDK; there are no NuGet dependencies. The mod builds in place inside the game
install, against the game's own assemblies and the installed `Mods/UndeadLegacy/UndeadLegacy.dll`.

```
dotnet build src/InventoryTweaks/InventoryTweaks.csproj -c Release
```

That restages `dist/InventoryTweaks/`, ready to copy into `Mods/`. To also build the release archive:

```
dotnet build src/InventoryTweaks/InventoryTweaks.csproj -c Release -t:Package
```

That writes `release/InventoryTweaks-<version>-<date>.zip`, taking the version from `ModInfo.xml`.
Neither `dist/` nor `release/` is tracked — the zip is published as a GitHub Release instead.

## License

MIT — see `LICENSE`.
