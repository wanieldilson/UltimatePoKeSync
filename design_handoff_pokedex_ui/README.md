# Handoff: UltimatePoKeSync — cartoon Pokédex UI

## Overview

A full visual redesign of the UltimatePoKeSync dashboard (Avalonia desktop app, .NET 10,
`src/UltimatePoKeSync.App`). Same information architecture and same data as today — party
rail, selected Pokémon, recommendations, team score — restyled as a **comic / anime
Pokédex**: thick black ink outlines, flat offset shadows, halftone dot texture, tilted
sticker badges, on a dark base. Two new teaching-oriented views (Stats & IV/EV, Best set)
make the app explain *why* a stat or a nature is good, not just print numbers.

## About the design files

`design/UltimatePoKeSync UI.dc.html` is a **design reference written in HTML**. Open it in
a browser to see the intended result. It is a prototype of look and behaviour — **do not
port the HTML into the app**. The task is to recreate this design in the project's existing
environment: **Avalonia 11 XAML**, using the app's existing views, view-models and bindings
in `src/UltimatePoKeSync.App` (`MainWindow.axaml`, `ViewModels/`, `TypePalette.cs`,
`SpriteImage.cs`). No new UI framework, no web view.

## Fidelity

**High-fidelity.** Colours, type sizes, radii, borders, shadows and copy in this document
are final and exact. Recreate them pixel-for-pixel in XAML. Where a value is not listed,
read it off the HTML file.

The one exception: the numbers in the mock (Treecko Lv.13, 27/35 HP, IVs, EVs, the team
score of 41/70) are **sample data** — every one of them must come from the live snapshot
and the analysis engine.

---

## Design tokens

Define these once as Avalonia resources (`App.axaml` `ResourceDictionary`) and reference
them everywhere; do not hard-code hexes per control.

### Colours

| Token | Hex | Used for |
| --- | --- | --- |
| `Ink` | `#08070C` | every border, every drop shadow. The single most important token |
| `AppBg` | `#0C0B10` | window background behind the shell |
| `ShellBg` | `#16141D` | app shell interior |
| `HeaderBg` | `#1C1A26` | identity header |
| `TabBg` | `#12111A` | tab strip + content backdrop |
| `RailBg` | `#171522` | party rail column |
| `Panel` | `#231F31` | default content panel |
| `PanelAlt` | `#2A2536` / `#2C2740` | nested rows inside a panel |
| `PanelSunken` | `#1B1826` / `#171325` | note strips, bar troughs |
| `Accent` (yellow) | `#FFD23F` | primary accent, active tab, "take it" badges, HP-warning fill |
| `Alert` (red) | `#FF5B4A` | title bar, negative badges, low HP |
| `Info` (cyan) | `#45D0E0` | IV colour, secondary badges, bridge accents |
| `Good` (green) | `#7FF09A` | live indicator, positive nature modifier |
| `TextPrimary` | `#F6F2E8` | body text |
| `TextMuted` | `#9A93B0` | secondary text |
| `TextFaint` | `#6F6889` | monospace meta, disabled |
| `GrassPanel` | `#25452E` | hero panel tinted by the Pokémon's primary type |
| `WaterPanel` | `#1E2B3F` | party card tinted by type |
| `DarkPanel` | `#3A2436` | party card tinted by type |
| `GoodBg` / `BadBg` | `#1C3524` / `#4A2A26` | positive / negative callout blocks |

**Type colours** come from the existing `TypePalette.cs`, brightened for the dark base.
Use these values (keep `TypePalette` as the single source, update the constants):

`Normal #A8AAA6` · `Fighting #D8503E` · `Flying #93B1EA` · `Poison #A355A8` ·
`Ground #C19A52` · `Rock #B4A45F` · `Bug #8FA62B` · `Ghost #7468B4` · `Steel #8A93A1` ·
`Fire #E06A2E` · `Water #4A86D8` · `Grass #5CBF4A` · `Electric #C9A227` ·
`Psychic #DC5A8E` · `Ice #5DB3C4` · `Dragon #6A5EDC` · `Dark #7A6659` · `Fairy #D486BC`.

Every type chip prints its **type name** as well as its colour — the colour is never the
only signal (this preserves the rule already documented in `TypePalette.cs`).

Panel tints are the type colour desaturated onto the dark base: take the type colour at
about 18–22 % opacity over `#12111A`.

### Typography

| Role | Font | Size / weight |
| --- | --- | --- |
| Display (Pokémon name, section badges, scores, stat labels) | **Bungee** 400 | 46 / 26 / 21 / 19 / 17 / 13 / 12 px, letter-spacing .10–.14em on small caps labels |
| UI text | **Nunito** 400 / 600 / 700 / 900 | body 13–15 px, emphasis 900 |
| Numbers, paths, IDs, meta | **DM Mono** 400 / 500 | 10.5–13 px, letter-spacing .06–.09em |

All three are on Google Fonts (OFL) — ship the TTFs in `src/UltimatePoKeSync.App/Assets/Fonts`
and register them with `FontFamily="avares://UltimatePoKeSync.App/Assets/Fonts#Bungee"`.
Bungee is display-only: never use it for a paragraph.

### Shape and depth

- **Border:** `3px` solid `Ink` on small elements (chips, rows, buttons), `4px` on panels,
  the shell and the hero. Chips use `2px`.
- **Radius:** chips/pills `999px`; rows and buttons `11–14px`; panels `20px`; hero and shell
  `22–26px`; sprite frames `12–20px`.
- **Shadow:** always a **hard flat offset**, never a blur —
  `3px 3px 0 Ink` (chips, buttons), `4px 4px 0 Ink` (rail cards), `6px 6px 0 Ink` (panels),
  `7px 7px 0 Ink` (hero), `10px 12px 0 Ink` (the whole shell).
  In Avalonia: `BoxShadow="6 6 0 0 #08070C"`.
- **Halftone:** a dot field over the header, the content backdrop and the hero —
  white at 4–10 % opacity, dots 1.5–2 px on an 11–14 px grid. Implement as a tiled
  `ImageBrush` (a small PNG) or a `DrawingBrush`; it is decoration, never interactive.
- **Tilt:** section badges and party cards sit at `rotate(-3deg)` … `rotate(+4deg)`
  (`RenderTransform="rotate(-2deg)"`). Small angles only — 0.3° to 4°.
- **Section badge:** every panel is titled by a pill that straddles its top border —
  absolutely positioned at `top:-13px; left:16px`, `3px` Ink border, radius 999, Bungee 12 px,
  a filled accent background, tilted. In XAML: a `Grid` with the panel `Border` and the badge
  `Border` overlapping, badge `VerticalAlignment=Top`, `Margin="16,-13,0,0"`.

### Spacing

4 / 5 / 7 / 9 / 12 / 14 / 16 / 18 / 22 / 24 px. Panel padding 18–22 px; gap between panels
18 px; gap between rows inside a panel 9–12 px.

---

## Shell layout

Window content is a single `Border` (radius 26, 4 px Ink, shadow `10 12 0`), 1440 px wide in
the mock; it should stretch. Inside, top to bottom:

1. **Title bar** — 45 px, background `Alert #FF5B4A`, 4 px Ink bottom border. Three 13 px
   circles (`#FFD23F`, `#45D0E0`, `#8CE07A`, each 2 px Ink) on the left; app name centred in
   Bungee 14 px, letter-spacing .14em, colour `#0E0D13`. On macOS this replaces the native
   chrome (`ExtendClientAreaToDecorationsHint`); on Windows/Linux keep the same bar.
2. **Identity header** — 94 px, `HeaderBg` + halftone, 4 px Ink bottom border. Left: the app
   icon in a 58 px white circle (4 px Ink, shadow `4 4 0`), then "UltimatePoKeSync" in Bungee
   21 px `Accent` with a `2px 2px 0 Ink` text shadow, and under it the game line in DM Mono
   11.5 px `#9A93B0` — `"POKÉMON EMERALD · BPEI · mGBA"`, bound to `GameIdentity`.
   Right: the **live pill** (green dot blinking 1.8 s, "LIVE · N reads/s", green-tinted
   `#153021` fill) and the **profile switch** — two halves of one pill; the selected half is
   `Accent` on `#171420`, the other `#2A2637` on `#A49EC0`. Bound to
   `RecommendationProfileKind`.
3. **Tab strip** — 46 px, `TabBg`, 4 px Ink bottom border. Tabs: *Pokémon · Stats & IV/EV ·
   Best set · Learnset · Team*, and *Bridge* pushed to the far right. Nunito 900 13.5 px
   `#CFC7E6`, white on hover. The active tab is marked by a 5 px `Accent` bar pinned to the
   bottom edge, inset 10 px each side, radius 4 px on the top corners only (`Info` cyan for
   Bridge). No background change.
4. **Body** — two columns: a fixed **286 px party rail** (`RailBg`, 4 px Ink right border) and
   the content area (`TabBg` + halftone, 22/24 px padding, 18 px gap between panels).

### Party rail

- Header row: "PARTY" Bungee 12.5 px `#8D86A6`, and `N / 6` in DM Mono on the right.
- One card per party member: 3 px Ink, radius 16, shadow `4 4 0`, background = the member's
  type tint, tilted ≤0.6°. A **slot number badge** (26 px circle, 3 px Ink, `Accent` for the
  selected member, `#E8E3F5` otherwise) overhangs the top-left corner at `-9px, -9px`.
  Inside: 54 px sprite frame (3 px Ink, radius 12, 6 px pixel grid), then name (Nunito 900
  16 px) with `Lv.N` in DM Mono on the right, type chips, and an HP bar — 11 px tall, 2 px
  Ink, radius 999, fill **green > 50 %, `Accent` 20–50 %, `Alert` < 20 %**, with `cur/max`
  in DM Mono beside it.
- A **status badge** (`PSN`, `BRN`, `SLP`, `PAR`, `FRZ`) overhangs the top-right corner at
  `-11px`, tilted 4°, 3 px Ink, purple `#B06BE0` for poison — one colour per
  `StatusCondition`. Hidden when `Status == None`.
- Empty slots: one dashed `#2F2B40` block, "N SLOTS FREE" in DM Mono, and one 34 px dashed
  placeholder per free slot.
- Pinned to the bottom: the **team score** panel — Bungee 38 px `Accent` score over `/ 70`,
  a 14 px bar filled with a red→yellow gradient, then the three worst
  `TeamStrengthFactor`s as `−N` + explanation, red for the worst, yellow below it. Bind to
  `TeamStrength.Score`, `MaximumScore`, `WeakestFactors`.

---

## Screens

### 1. Pokémon (default)

- **Hero panel** — 4 px Ink, radius 22, shadow `7 7 0`, background = primary-type tint,
  halftone overlay plus a large soft white circle bleeding off the top-right corner.
  Left: a 190 px sprite frame (4 px Ink, radius 20, 12 px pixel grid, shadow `5 5 0`) with a
  0.5 s pop-in (`scale .9 → 1.04 → 1`, slight rotation) whenever the species changes.
  Right: name in **Bungee 46 px** white with a `4px 4px 0 Ink` shadow, a tilted `LV N` badge
  in `Accent` Bungee 15 px, the type chips, and on the far right the slot / dex number / PID
  in DM Mono. Under it the **big HP bar**: 24 px tall, 4 px Ink, radius 999, fill a vertical
  `#A6FFBC → #5CE07D` gradient, with `cur/max` in Bungee 20 px beside it. Then four fact
  chips (`3px` Ink, radius 12, translucent black fill): Nature, Ability, Item, Friendship.
- **Moves now** (left, half width) — one row per move slot: an 8–9 px type-coloured spine,
  move name Nunito 900 15 px, `type · category · power` under it, PP as `cur/max` in DM Mono.
  An empty slot is dashed and says what is coming instead (e.g. "Leech Seed is 3 levels away").
- **Matchups** (right, half width) — "TAKES DOUBLE FROM" and "SHRUGS OFF" chip groups from
  the type chart, then a sunken note that ties the weakness to the rest of the party.
- **Coming up** — the evolution card (sprite, "N levels to <species>") beside the next
  level-up moves; moves past the evolution level are dimmed to 55 % with the reason
  ("Grovyle's learnset by then").

### 2. Stats & IV/EV

- **Where each stat comes from** — one stacked bar per stat, 26 px tall, 3 px Ink,
  radius 10, segments in source order: base `#5A5473` → IV `#45D0E0` → EV `#FFD23F` →
  nature `#FF5B4A` (the nature segment is drawn at 55 % opacity when the modifier is
  *negative*, so a penalty reads as a ghost rather than a gain). Segment widths are each
  contribution as a fraction of the largest final stat on screen. To the right: the final
  value in Bungee 16 px, then a DM Mono breakdown `"40 base · IV 24 · EV 4"`. A legend above
  names the four colours. This panel is the app's main teaching moment — it must be the
  first thing on the screen.
- **IV — the luck you were dealt** (cyan panel) — six vertical wells, 74 px, 3 px Ink,
  radius 12, filled bottom-up to `iv/31`; `≥24` uses full `Info` cyan, below that a muted
  `#3A7F8C`; a perfect 31 is `Accent` yellow with "MAX" printed on it. Copy explains IVs roll
  once at capture and never change, and that a perfect IV in a stat the build never uses is
  luck you cannot spend.
- **EV — the part you control** (yellow panel) — total spent in Bungee 34 px over
  "of 510 spent · N left", a 16 px total bar, then a per-stat bar (16 px, radius 999,
  `/252`); stats with 0 EV are drawn as a dashed empty trough. Below, a "what to farm next"
  note (4 EV = +1 point at level 100) and a red warning strip for EVs spent by accident.

### 3. Best set

- A **three-column compare**: "RIGHT NOW" panel, a 60 px yellow arrow circle with the change
  count, "RECOMMENDED" panel in the type tint. Rows that differ are tinted (`BadBg` on the
  left, `GoodBg` on the right); rows that already match stay neutral.
- **Why this nature** — two blocks side by side: the current nature in `BadBg` with its
  `+10% / −10%` chips and a plain-language cost ("takes 2 Speed off it today and 21 at level
  100"), the recommended one in `GoodBg` with its argument. A closing note states that nature
  is fixed at capture, so this is advice for the next catch — never a task list.
- **Four slots, four jobs** — a 2×2 grid, one card per `BuildSlot`, each with a type spine, the
  move name, a role chip coloured per `BuildSlotRole` (SameType `Accent`, Coverage `#E0954A`,
  TeamSupport `#B06BE0`, Utility `Info`, Filler grey) and the engine's `Reason` string.
  Moves not yet obtainable are dashed and name their source ("TM39, Rustboro").
  Under the grid, "TURNED DOWN" chips list `RecommendedBuild.Alternatives` with their reason.

### 4. Learnset

- **Evolution line** — three cards in a row joined by `→` arrows carrying the evolution
  level. The current stage has a 4 px `Accent` border and a "YOU ARE HERE · LV.N" badge;
  later stages step down to 80 % and 55 % opacity. A note flags when holding evolution back
  buys a move (Treecko learns Leech Seed at 16, Grovyle at 17).
- **Level-up timeline** — a vertical rail: 15 px dots on a 4 px line, 19 px `Accent` dot for
  the next move, and the line gradient-fades from `Accent` to `#3A3450` past it. Known moves
  sit at 50 % opacity; moves past the evolution level are dimmed and labelled as belonging to
  the next stage's learnset.

### 5. Team

- Three (up to six) **party cards** in a grid: type-tinted, tilted ≤0.6°, 96 px sprite frame,
  type chips and one sentence of analysis per member.
- **Coverage wall** — all 17 Gen 3 types in a 9-column grid of small tiles: green
  (`#1F3D28` / `#A6FFBC`, "2×") where the party hits super-effectively, red
  (`#5A1F1A` / `#FFB3AA`, "gap") where nothing does, neutral `#2C2740` otherwise.
  A closing note names the gaps and the single cheapest fix.

### 6. Bridge

- **Status hero** in the water tint: blinking 20 px green dot, "Bridge is live" in Bungee
  28 px, the socket address in DM Mono `Accent`, and a read-only reassurance line. Below,
  three meta columns: game identity, last packet age, party bytes + signature.
- **"If it ever drops"** — three numbered steps (34 px `Accent` circles, Bungee): open
  mGBA's scripting window; load the script — with the path in a DM Mono `Accent` field beside
  **Reveal** (cyan) and **Copy path** (grey) buttons, both 3 px Ink with a `3 3 0` shadow;
  then play. The third step carries the "silence means an unrecognised ROM" explanation.
- When disconnected, this screen becomes the whole window (no rail, no tabs): the dot turns
  `Alert`, the heading reads "Waiting for the bridge", and the steps stay identical.

---

## Interactions & behaviour

- **Tabs** switch the content area only; the rail and header never move. Selecting a party
  card selects that member and switches to *Pokémon*.
- **Hover:** buttons and cards lift by `translate(-1px,-1px)` — the flat shadow stays put, so
  the element appears to pop off the ink. Tab labels go white. 120 ms ease-out; no colour
  fade on panels.
- **Live updates:** values are re-bound in place; only two things animate —
  the sprite pop-in on species change (0.5 s) and bar widths (200 ms ease-out on HP, EV and
  score bars). Never animate a whole panel on every poll: the party updates ~4×/s and the
  screen must stay calm.
- **Blink:** the live dot animates opacity 1 → .25 → 1 over 1.8 s, infinite. Stop it when
  disconnected.
- **Egg slots** show the card with the sprite frame replaced by an egg placeholder, no types,
  no HP bar, and the badge "EGG" — an egg cannot battle and is excluded from analysis
  (`PokemonSnapshot.CanBattle`, D-036).
- **Fainted:** HP bar empty, the card desaturated to 60 %, an `Alert` "FNT" badge.
- **Minimum window** 1180×820; below 1320 px wide the party rail collapses to 96 px
  (sprite + level only). Panels reflow from two columns to one under 1180 px.

## State

Nothing new. The existing view-model already holds: the party snapshot, the selected slot,
the profile kind, connection status, and the analysis + recommendation results. Add only:
`SelectedTab` (Pokemon | Stats | Build | Learnset | Team | Bridge), and a
`SpeciesChangedTick` used to retrigger the sprite pop-in.

## Assets

- `src/UltimatePoKeSync.App/Assets/upsync-icon.png` — existing app icon, reused in the header.
- **Sprites are placeholders in the mock.** Every sprite frame is filled at runtime from the
  ROM through the bridge (`SpriteImage.cs`). Nothing is bundled — keep it that way.
- Fonts: Bungee, Nunito, DM Mono (SIL OFL) — add to `Assets/Fonts` and to
  `THIRD_PARTY_NOTICES.md`.
- Halftone tile: generate one 14×14 PNG with a single 2 px white dot at 8 % opacity and tile it.

## Files

- `design/UltimatePoKeSync UI.dc.html` — the design reference. Open in a browser; the tabs work.
- `design/support.js` — runtime for the HTML prototype only. Not part of the design.
- `design/src/UltimatePoKeSync.App/Assets/upsync-icon.png` — icon used by the prototype.
- `PROMPT.md` — the message to paste into Claude Code to start the work.
