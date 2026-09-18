# Project Handoff

> Written for an AI coding assistant with **zero prior context**.
> Read this file completely, then inspect the repository before changing anything.
>
> **Last session ended:** 2026-09-18, immediately after committing `cef28b4`.
> **Last action taken:** fixed tap input being swallowed by a transparent UI panel, re-framed
> the world camera, and enlarged characters. **These fixes compile but were NEVER seen running
> in the Unity editor.** Verifying them is task #1 in [Next Steps](#8-next-steps).

---

## 1. Project Goal

**DETECT** (Arabic: **ديتكت**) — an Arabic-language educational detective game for **mobile
(Android first)**, built in **Unity 6.3 LTS** with C#.

### The product
A case-based investigation game. The player is a detective who walks around top-down rooms,
examines evidence, interrogates witnesses, spots contradictions, confronts a suspect, unlocks
new evidence as a result, and finally writes a report that must be **supported by the evidence
they actually hold**.

### Core loop
```
Investigate → Connect → Test → New Information → (repeat) → Supported conclusion
```

### The teaching core — do not dilute this
The game's real subject is **evidentiary standards**: the difference between what a piece of
evidence *proves* and what it *does not prove*. Every `Evidence` record carries explicit
`proves[]` and `notProves[]` arrays that are shown to the player. The case is deliberately
built so that:
- A witness statement proves only that someone *said* something, not that it is true.
- Hearsay about a box's contents does not prove its contents.
- **Proving a suspect lied is NOT proof they committed the crime.** Accusing on that basis is
  explicitly rejected with a teaching message, not a score penalty.

### Hard requirements gathered from the user
- Arabic UI, correct right-to-left rendering with joined letterforms.
- Portrait mobile layout (1080×1920 reference).
- Full graphics — painted backgrounds, character portraits, evidence photographs. Not text-only.
- A lobby: intro cinematic, main menu, case select, settings, archive/codex.
- A GTA-style cold-open intro sequence.
- Sound effects (music was not possible — see §7).
- **Top-down walkable world** where the player moves a character between rooms.
- Movement must be **tap/click-to-move**, NOT a continuous joystick (explicit user correction).
- Intended for a training-programme showcase / competition entry.

---

## 2. Current Status

### ✅ Completed and verified by automated tests

| Area | State |
|---|---|
| Environment install (Unity, VS, Android module, Unity Hub) | Done, all verified present |
| Arabic text shaping + bidi engine | Done, **13/13 tests pass** |
| Case data model + loader + validator | Done |
| Full case logic (`CaseSession`) | Done, **66/66 tests pass** |
| Case 01 content (Arabic prose, 5 evidence, 2 characters, 7 report slots, 12 hints) | Done |
| Internal truth table / logic audit for case 01 | Done, in `Assets/Docs/` |
| Point-and-click screen mode (14 screens) | Done, compiles |
| Lobby: intro cinematic, menu, case select, settings, codex | Done, compiles |
| Procedural sound effects (10 cues + room tone) | Done, compiles |
| Save/settings persistence (`PlayerPrefs`) | Done, compiles |
| Generated art: 4 backgrounds, 4 portraits, 4 evidence, 4 brand, 3 maps, 5 map sprites | Done, in repo |
| Top-down world: rooms, collision, camera, tap-to-move | Code done, **compiles, 0 errors** |
| All C# compiles against real Unity assemblies | **0 errors** |

### ⚠️ Partially completed / NEEDS VERIFICATION

- **The walkable world has never been confirmed working visually.** The user reported it
  broken three times; three separate root causes were found and fixed (see §7). After the
  final fix (`cef28b4`) the user ran out of usage before testing. **Assume unverified.**
- **Blocker rectangles in `Assets/Resources/Maps/case01.json` are eyeballed** from the map
  PNGs, not precisely measured. They are probably approximately right but will need tuning.
  Only the archive hall was refined; **office and guard room blockers are first-draft**.
- **Nothing has ever been tested on a physical Android device.** No APK has been built.
- Visual layout of every UI screen is unverified — only logic and compilation are proven.

### ❌ Not started

- Cases 02–05 (titles and one-line concepts exist in the case-select screen as locked entries;
  **no content written**).
- **Character designer / avatar customization** (user requested; was next after movement).
- **"Reconstruction" feature** — the headline mechanic agreed with the user (see §3). Not begun.
- Line-of-sight mechanic, CCTV ghost replay, flashlight.
- Music (not possible with available tooling — see §7).
- Voice acting.
- Screen transition animations (only the intro cinematic has fades).

### 🛑 Exact stopping point

The last thing done was commit `cef28b4`, which:
1. Set `raycastTarget = false` on the world screen's transparent panel Image
   (`Assets/Scripts/UI/Screen_World.cs`) — this was blocking **100% of taps**.
2. Replaced fixed `ViewHeight` with per-room `FitToRoom()` so the camera frames the room's
   full height (`Assets/Scripts/World/WorldView.cs`).
3. Raised `PlayerHeight` 2.2 → 2.9 and NPC `spriteHeight` 2.2 → 2.9.
4. Added `ApproachPoint()` / `NearestFreePoint()` / `IsClear()` so a tap near an obstacle
   finds a reachable standing position instead of stalling.
5. Added keyboard steering (WASD / arrows) via `Walker.Steer()`.
6. Slimmed the archive-hall blockers to widen the aisles.

**After that commit**, a further housekeeping change was made (uncommitted at time of writing —
verify with `git status`): the four test scripts in `Tests/` were refreshed from newer copies
and their hard-coded `E:\GameDev\...` paths were replaced with paths derived from
`$PSScriptRoot`, so they run from any checkout. All four were re-run and pass.

---

## 3. Important Decisions

These were deliberate. **Do not reverse them without a specific reason.**

### D1 — 2D, never 3D
Decided after analysis: successful detective games (Ace Attorney, Return of the Obra Dinn,
The Case of the Golden Idol, Danganronpa, Her Story) are all 2D or stylised. 3D adds walking
time, which is dead time in a 5–8 minute mobile session. The agreed upgrade path for better
visuals is **pre-rendered 3D exported as 2D art**, not real-time 3D.

### D2 — The entire UI is built in code, not in the Inspector
`Game.cs` is the **only** MonoBehaviour the scene needs. It constructs the Canvas, EventSystem
and every screen at runtime. **Rationale:** the user is a Unity beginner; wiring ~50 Inspector
references is ~50 chances to break the build. Pressing Play just works.
**Do not convert screens to prefabs** — it would reintroduce exactly that fragility.

### D3 — Cases are data files, not code
A case is one JSON file in `Assets/Resources/Cases/`. Adding a case requires **zero code**.
Driven by the user's explicit requirement for different environments per case.

### D4 — A walkable map is an *upgrade*, never a requirement
`Assets/Resources/Maps/<caseId>.json` is optional. If absent, `Game.HasWorld` is false and the
game silently falls back to the painted point-and-click screen (`Screen_Location.cs`).
Both modes route through the **same** `CaseSession` calls, so they cannot disagree about
what the player has found. **Preserve this.**

### D5 — Arabic is solved manually and must not be "simplified"
TextMeshPro does **no** Arabic letter joining and **no** bidi. `ArabicText.Fix()` does both.
See §5 for the strict rules. This is the single most fragile part of the project.

### D6 — Tap-to-move, not a joystick
**Explicit user instruction.** Also the more robust design: there is no held input state to
lose, so a missed touch-release can never leave the character stuck. `TouchStick.cs` was
**deleted**. Do not reintroduce a virtual stick.

### D7 — All audio is synthesised in code
`Assets/Scripts/Core/Sfx.cs` generates every `AudioClip` mathematically at startup. No audio
files, no licences, nothing in the build. **User instruction: if you want to use any asset
from Freesound or a similar external source, ASK FOR APPROVAL FIRST.**

### D8 — Original IP only (legally important)
The user originally wanted the game called **True Detective** using the **HBO series poster
with Matthew McConaughey and Woody Harrelson**. This was refused: HBO trademark plus real
actors' likeness rights. The user accepted and chose the name **DETECT**.
An original logo was generated (`Assets/Resources/Brand/logo.png`).
⚠️ **The Unity project folder and product name are still literally `TrueDetective`** — this is
leftover and should eventually be renamed (see Next Steps #8). Do not ship under that name.

### D9 — Font choice is a hard technical constraint, not taste
`ArabicText.Fix()` outputs codepoints in **U+FE70–FEFF** (Arabic Presentation Forms-B).
Many popular Arabic fonts omit most of that block. Measured coverage:

| Font | U+FE70–FEFF coverage | Usable |
|---|---|---|
| Amiri | 97% | ✅ |
| IBM Plex Sans Arabic | 97% | ✅ |
| **Cairo** | **62%** | ❌ renders as empty boxes |
| **Tajawal** | **62%** | ❌ renders as empty boxes |

(97% = every *assigned* codepoint; the missing 4 are unassigned in Unicode.)
**Any new font must be checked with `Tests/check-fonts.ps1` before use.**

### D10 — The agreed headline feature: "Reconstruction" (إعادة التمثيل)
Agreed with the user as the mechanic most likely to win the competition. **Not built yet.**

> After gathering evidence, instead of filling in a report the player **stages the crime on the
> same top-down map they walked**. They place the suspect, draw a path, set a start time, and
> press play. The game animates their theory while every collected piece of evidence "watches".
> When the theory contradicts a piece of evidence, **the replay stops at that moment** and the
> contradicting evidence flashes red.
>
> e.g. theory says Samer left at 20:00; corridor camera saw him at 20:20 → replay halts at
> 20:03 with the camera icon flagged.

This is *why* the movement system exists — it makes walking an investment rather than a tax.

### D11 — Design principles baked into the content
- Wrong answers never cost lives, score, or completed scenes.
- Hints are free and escalate in 3 levels per stage.
- No timers that punish thinking.
- No "guilt meter" or accusation percentage.
- All information needed to solve the case is inside the case.

---

## 4. Files and Project Structure

**Project root:** `E:\GameDev\Projects\TrueDetective` (on the old machine)
**Git:** initialised, branch `master`, 5 commits, `.gitignore` excludes `Library/ Temp/ obj/
Build/ Builds/ Logs/ UserSettings/ *.csproj *.sln *.user`

```
TrueDetective/
├── HANDOFF.md                    ← this file
├── README.md                     Arabic run/build/extend guide. Still says "TRUE DETECTIVE"
│                                 in places; needs a rename pass.
├── Assets/
│   ├── Scripts/
│   │   ├── Core/
│   │   │   ├── ArabicText.cs     Arabic shaping + bidi. ~300 lines. DO NOT CASUALLY EDIT.
│   │   │   ├── CaseSession.cs    ALL game rules and progress. Zero Unity references —
│   │   │   │                     that is what makes it headlessly testable. ~450 lines.
│   │   │   ├── Sfx.cs            Synthesises 10 SFX cues + looping room tone.
│   │   │   ├── GameSettings.cs   PlayerPrefs: volumes, mute, text speed, solved cases.
│   │   │   └── Bootstrap.cs      [RuntimeInitializeOnLoadMethod] spawns Game if absent.
│   │   ├── Data/
│   │   │   ├── CaseData.cs       Serializable case model. Unity-free (no `using UnityEngine`).
│   │   │   ├── CaseLoader.cs     JsonUtility load + extensive validation.
│   │   │   └── MapData.cs        Walkable map model: rooms, blockers, spots. Unity-free.
│   │   ├── UI/
│   │   │   ├── Theme.cs          Colours, type scale, runtime TMP font creation.
│   │   │   ├── UIKit.cs          All UI builders. Every label goes through ArabicText.
│   │   │   ├── Art.cs            Sprite loader with Texture2D fallback + cache.
│   │   │   ├── Game.cs           THE MonoBehaviour. Canvas, screen stack, top/bottom bars,
│   │   │   │                     toast, hint modal, procedural background fallback.
│   │   │   └── Screen_*.cs       11 files, all `partial class Game`:
│   │   │                         Cinematic, Menu, Intro(newspaper+briefing), World,
│   │   │                         Location, Notebook, Interrogation, Connect, Confront,
│   │   │                         Report, Verdict
│   │   └── World/
│   │       ├── Walker.cs         Player body. Tap-to-destination + keyboard steering,
│   │       │                     stall detection, procedural walk cycle.
│   │       ├── WorldBuilder.cs   Builds one room: floor, colliders, spots, shadows.
│   │       └── WorldView.cs      Camera, tap handling, nearest-spot tracking, depth sorting.
│   ├── Editor/
│   │   ├── ProjectSetup.cs       [InitializeOnLoad] creates Main.unity, sets player settings,
│   │   │                         menu items incl. "Build Android APK".
│   │   └── TMPResources.cs       Auto-imports TMP Essential Resources (see §7 bug B3).
│   ├── Resources/
│   │   ├── Cases/case01.json     THE CASE. All Arabic prose, evidence, dialogue, report.
│   │   ├── Maps/case01.json      Walkable map: 3 rooms, blockers, spots. TUNING NEEDED.
│   │   ├── Maps/map_*.png        3 top-down room maps (axis-aligned, 1024×1024).
│   │   ├── Maps/ch_*.png         5 character sprites, backgrounds removed (transparent).
│   │   ├── Backgrounds/*.png     4 painted scenes for the point-and-click mode.
│   │   ├── Portraits/*.png       4 dialogue portraits incl. pt_samer_broken.
│   │   ├── Evidence/*.png        4 evidence photographs.
│   │   ├── Brand/*.png           logo, icon, 2 intro cards, news_photo.
│   │   └── Fonts/                Amiri-Regular, Amiri-Bold, IBMPlexSansArabic-Regular.
│   ├── Scenes/Main.unity         Auto-generated. Contains only Main Camera + TrueDetective.
│   └── Docs/TRUTH-TABLE-case01.md  Internal logic audit. NOT shown to the player.
└── Tests/
    ├── compile-check.ps1         Compiles everything with Unity's Roslyn. No licence needed.
    ├── test-playthrough.ps1      66 assertions over the whole case logic.
    ├── test-arabic.ps1           13 Arabic shaping assertions.
    ├── arabic-test-cases.txt     UTF-8 test strings (kept separate — see §5).
    └── check-fonts.ps1           Parses font cmap tables for U+FE70–FEFF coverage.
```

### Files most likely to need work next
| File | Why |
|---|---|
| `Assets/Resources/Maps/case01.json` | Blocker rectangles need tuning against the art. |
| `Assets/Scripts/World/WorldView.cs` | Camera framing / tap feel tuning. |
| `Assets/Scripts/World/Walker.cs` | Movement feel constants. |
| `Assets/Scripts/UI/Screen_World.cs` | World overlay layout. |

---

## 5. Code / Implementation State

### 5.1 Arabic text — the most fragile system

**The problem:** TextMeshPro renders glyphs left-to-right and performs **no** Arabic letter
joining and **no** bidirectional reordering.

**The solution:** `TrueDetective.Core.ArabicText.Fix(string)`:
1. Maps each Arabic letter to its isolated / initial / medial / final presentation form
   (U+FE70–FEFF) based on its neighbours, skipping combining marks.
2. Forms mandatory lam-alef ligatures (U+FEF5–FEFC).
3. Reverses the string, **keeping Latin/digit runs in reading order** so `20:20` and `أ-47`
   survive. A separator (`: - . / , % +`) stays inside a run only when a real LTR character
   sits to its left.
4. Mirrors paired glyphs `( ) [ ] { } < > « »`.

**Non-negotiable rules:**
```csharp
// CORRECT — always
UIKit.Label("name", parent, "نص عربي", ...);
UIKit.SetText(someLabel, "نص عربي");

// WRONG — never assign directly; the text will be unjoined and reversed
someLabel.text = "نص عربي";
```
`TMP_Text.isRightToLeftText` must stay **false** on every label — `ArabicText` has already
reordered, and enabling it reverses a second time.

### 5.2 Fonts are created at runtime
`Theme.Load()` does `TMP_FontAsset.CreateFontAsset(Font)` with
`AtlasPopulationMode.Dynamic`, so presentation-form glyphs rasterise on demand and no font
asset has to be generated by hand. It checks `TMP_Settings.instance == null` first and logs
an actionable message, because TMP otherwise throws an opaque NullReferenceException.

### 5.3 `CaseSession` — all the rules, zero Unity
`Assets/Scripts/Core/CaseSession.cs` has **no** `using UnityEngine`. Same for `CaseData.cs`
and `MapData.cs`. This is deliberate: it lets `Tests/test-playthrough.ps1` compile them with
the in-box .NET Framework C# compiler and run a **full headless playthrough without a Unity
licence**. **Keep these three files Unity-free.**

Key members:
- `CollectEvidence(id)` — refuses duplicates and anything still gated by `unlockedBy`.
- `RequirementMet(string)` — a requirement string is satisfied if it names collected evidence,
  a completed action, a held confrontation, or a proven contradiction. Empty = no gate.
- `TryConnect(a, b, relation, out matched)` → `Wrong | Correct | AlreadyKnown`. Order-free.
- `PendingConfrontation(charId)` / `CompleteConfrontation(cf)`.
- `SubmitReport(answers, citedEvidence)` → `ReportResult` with `Accepted`, `CorrectSlots`,
  `TotalSlots`, `MissingEvidence[]`, `PrematureAccusation`.
  **It reports how many slots are right but never which ones** — deliberate, to stop
  elimination-solving.
- `CurrentStage` — **derived from progress, not tracked**, so an unusual collection order
  cannot strand the hint system.
- `Reset()` — clears everything. **Anything you add to this class must be cleared here.**

### 5.4 Screen system
`Game.cs` holds `Dictionary<string, RectTransform> _screens`. `Show(key)` deactivates all,
activates one, calls `RefreshScreen(key)`. **Screens rebuild their contents on show** rather
than patching themselves — that is what keeps state consistent.

Registered screen keys:
`intro, world, menu, cases, settings, codex, newspaper, briefing, location, notebook,
interrogate, connect, confront, report, verdict`

`Game.InvestigationScreen` returns `"world"` when a map exists, else `"location"`. Sub-screens
return via `Back()` / `Show(InvestigationScreen, false)`.

### 5.5 The walkable world

**Coordinates.** Every room is 20×20 world units, origin at the room centre, **+Y up**.
The art is 1024×1024, so **1 world unit = 51.2 px**. To convert a pixel measurement from a
map PNG into a blocker:
```
x = (px / 1024 - 0.5) * 20
y = (0.5 - py / 1024) * 20     // note the flip: image Y grows downward
```
This formula is also recorded in the `_comment` field of `Assets/Resources/Maps/case01.json`.

**`Walker.cs`** — destination-based:
- `WalkTo(Vector2)` sets a target; `Halt()` clears it; `Steer(Vector2)` is direct keyboard
  control that cancels the target.
- `Arrived` and `Stalled` events.
- **Stall detection:** if asked to move but actual displacement < `StallSpeed` (0.55 u/s) for
  `StallTime` (0.35 s), the order is dropped and `Stalled` fires. This is what stops the body
  grinding into furniture forever.
- Walk cycle is procedural: bob + squash + lean, computed from `_artRestY` each frame so the
  offset cannot accumulate.
- Feel constants at the top of the class: `Speed 5.2`, `Accel 40`, `Drag 30`,
  `ArriveRadius 0.28`, `SlowRadius 1.5`, `BobHeight 0.075`, `BobRate 4.4`, `LeanDegrees 4.5`.

**`WorldView.cs`** — camera + input:
- `HandleTap(screenPoint)`: ignores the tap if `EventSystem.current.IsPointerOverGameObject()`.
  Finds a spot within `max(spot.radius, 1.4)` of the tapped world point; if found, walks to
  `ApproachPoint()` and acts on arrival; otherwise walks to `NearestFreePoint()`.
- `ApproachPoint(spot, from)` tries the straight-line standing position, then ±35°, ±70°,
  ±110°, ±150°, 180° until `IsClear()` passes — essential in narrow aisles.
- `IsClear(point)` uses `Physics2D.OverlapCircleNonAlloc(point, 0.42f, buffer)` and **skips
  the player's own colliders**.
- `FitToRoom(room)` sets `orthographicSize = (room.height + ViewPadding) * 0.5f`.
- `FrameOn(target)` clamps the camera to the room **but the player always wins** — if clamping
  would push the body out of frame, a strip of background shows instead.
- `SortByDepth()` sets `sortingOrder = round(-y * 100)` so lower-on-screen draws in front.
  Objects named `"shadow"` get `order - 1`.
- **Camera depth is `10f`** — it must render *after* the scene's Main Camera, which also clears
  to a solid colour. A lower value lets the Main Camera paint over the world.

**`WorldBuilder.cs`** — builds a room from `RoomData`: floor sprite scaled to `width`×`height`,
`BoxCollider2D` per blocker, four thick boundary slabs, and a `WorldSpot` per visible spot.
`Clear()` destroys everything; **callers must null their cached `WorldSpot` references** (see
bug B5 in §7).

### 5.6 Data shapes

`Assets/Resources/Cases/<id>.json` → `CaseData`:
`newspaper, briefing, startLocationId, locations[], evidence[], characters[], contradictions[],
confrontations[], actions[], report{template, slots[], requiredEvidence[]}, hints[], epilogue`

`Assets/Resources/Maps/<id>.json` → `MapData`:
`rooms[] { id, name, map, width, height, spawn{x,y}, tint, blockers[{x,y,w,h}],
spots[{id, label, at{x,y}, radius, sprite, spriteHeight, givesEvidence, opensCharacter,
travelTo, flavourText, requires}] }`

`RoomData.id` **must match** a `Location.id` in the case file.

⚠️ **JsonUtility constraints** — the whole data model obeys these:
public fields only (no properties), no dictionaries, no polymorphism, arrays not `List<>`.

### 5.7 Case 01 solution (spoilers — needed to test)
```
Truth: at 20:12 Samer put manuscript أ-47 into his personal box inside the archive room,
       then left the building at 20:20 carrying it. No exit permit was issued.
       He claims he left at 20:00 and never returned. That claim is false.

E1 custody log      → identity أ-47, present before, no exit permit.  Does NOT prove who/when.
E2 Samer statement  → proves only that he said it.
E3 corridor footage → continuous 19:45–21:00, exits 20:20 with a box. Does NOT prove contents.
E4 Nader statement  → a box left with Samer. Does NOT prove its contents (hearsay).
E5 room footage     → LOCKED. Shows him placing أ-47 in the box at 20:12.

Path: E1, E2, E3, E4 → connect E2+E3 as "contradicts" → confront Samer (CF1) →
      Samer names the room and window → ask Nader question n_q4 → E5 → report.

Report requires citing E1, E3, E5. Slot answers:
  t_place=20:12, who=سامر, code=أ-47, container=صندوقه الشخصي,
  where=غرفة الأرشيف, t_exit=20:20, permit=تصريح إخراج
```
`E5` is gated behind action `act_request_room_footage`, which is gated behind confrontation
`CF1`. **The in-fiction reason is explicit**: room cameras archive offline and are only
retrieved on a written request naming a room and a time window — knowledge the player only
gets from the confrontation. Preserve this justification; it is what stops E5 feeling arbitrary.

### 5.8 Naming conventions
- Private fields `_camelCase`; public members `PascalCase`.
- Screen builders `BuildX()` / `RefreshX()` as `partial class Game` in `Screen_X.cs`.
- Evidence ids `E1`…`E5`; contradictions `C1`; confrontations `CF1`; actions `act_*`;
  hotspots/spots `hs_*` / `go_*`; questions `<char>_q<n>`.
- Sprite names: `bg_*`, `pt_*`, `ev_*`, `map_*`, `ch_*`.
- **Code comments are in English. All player-facing strings are in Arabic.**

---

## 6. Commands Used

All PowerShell commands assume **Windows PowerShell 5.1**.
Unless stated otherwise, run from the **project root**: `E:\GameDev\Projects\TrueDetective`

### Run the full verification suite (no Unity licence required)
```bash
powershell -NoProfile -ExecutionPolicy Bypass -File Tests\compile-check.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File Tests\test-playthrough.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File Tests\test-arabic.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File Tests\check-fonts.ps1
```

Expected output:
```
==== 0 compile errors ====
==== 66 passed / 0 failed ====
==== 13 passed / 0 failed ====
ALL FONTS USABLE
```

### If Unity is installed somewhere else
```bash
powershell -NoProfile -ExecutionPolicy Bypass -File Tests\compile-check.ps1 -UnityDataDir "C:\Program Files\Unity\Hub\Editor\6000.3.24f1\Editor\Data"
```

### Open the project
```bash
"E:\GameDev\UnityHub\Unity Hub.exe"
```
Then in Unity: **True Detective → Open Main Scene**, press **Play**.
Set the Game view to **1080×1920 Portrait**.

### Read Unity's log to diagnose a runtime error without asking the user
```bash
powershell -NoProfile -Command "Get-Content \"$env:LOCALAPPDATA\Unity\Editor\Editor.log\" | Select-String -Pattern '\[World\]|\[Theme\]|\[Art\]|\[CaseLoader\]|NullReference|Exception' | Select-Object -Last 30"
```
This was used repeatedly and is the fastest way to find a runtime failure.

### Validate a case or map JSON quickly
```bash
powershell -NoProfile -Command "Get-Content 'Assets\Resources\Cases\case01.json' -Raw -Encoding UTF8 | ConvertFrom-Json | Out-Null; 'VALID'"
```

### Git
```bash
git log --oneline
git status
```

### Build an APK
Unity menu: **True Detective → Build Android APK** → output `Builds/TrueDetective.apk`
(NEVER TESTED — see §7.)

---

## 7. Bugs / Problems / Blockers

### Fixed bugs — recorded so they are not reintroduced

**B1 — `n_q4` unreachable, case unwinnable.**
Nader's question that yields E5 was gated by `onlyAfterConfront: true`, but `onlyAfterConfront`
is evaluated **per character** and the confrontation is with *Samer*. The question never
appeared. Fixed by gating on `"requires": "CF1"` instead, which `RequirementMet()` satisfies
from the confrontation set. Caught by `test-playthrough.ps1`, never by a human.
**Lesson:** cross-character gating must use `requires`, not `onlyAfterConfront`.

**B2 — Cairo font rendered everything as empty boxes.** See D9.

**B3 — `NullReferenceException` in `TMP_Settings.get_clearDynamicDataOnBuild`.**
Exact error:
```
NullReferenceException: Object reference not set to an instance of an object
  at TMPro.TMP_Settings.get_clearDynamicDataOnBuild () ... TMP_Settings.cs:149
  at TMPro.TMP_FontAsset.CreateFontAssetInstance ...
  at TrueDetective.UI.Theme.Load (System.String resourcePath) ... Theme.cs:92
```
Cause: TMP Essential Resources were never imported, so `TMP_Settings.instance` was null.
Fixed by `Assets/Editor/TMPResources.cs`, which imports
`Package Resources/TMP Essential Resources.unitypackage` automatically, plus a guard in
`Theme.Load()` that names the fix. Menu fallback: **True Detective → Import TextMeshPro Resources**.

**B4 — World rendered as a solid dark screen.**
`Game.BuildCanvas()` added an opaque `Image` to the canvas root as a backdrop. The canvas is
Screen Space Overlay, so it painted straight over the world camera. Fixed with `_rootBg` whose
`enabled` is false while the `"world"` screen is up.

**B5 — Interaction prompts stuck on screen pointing at nothing.**
`WorldBuilder.Clear()` destroys spot GameObjects, but `WorldView._nearest` still held a
reference to a destroyed object, so the prompt stayed up for a thing that could no longer be
acted on. Fixed by clearing `_nearest` and `_pending` before any rebuild.

**B6 — Player walked off-screen.**
The camera clamped to the room bounds with no guarantee the player stayed in view. Fixed in
`FrameOn()` — the room clamp is applied, then overridden so the body is always within a margin.

**B7 — Tap-to-move did nothing at all.**
The world screen's panel `Image` was transparent but **`raycastTarget` was still true**, so
`EventSystem.current.IsPointerOverGameObject()` returned true for every tap and the world
never received one. Fixed by `panelImg.raycastTarget = false` in `Screen_World.cs`.
**This is the most recent fix and is UNVERIFIED.**

**B8 — `There are no audio listeners in the scene`.**
The world camera was created without one. Fixed by adding an `AudioListener` to the Game
GameObject in `BuildCanvas()`.

### Approaches already tried that did NOT work — do not repeat

- **Skipping Unity Hub.** Unity 6 launched standalone shows the download page because licence
  management lives in the Hub. The Hub is required.
- **`https://public-cdn.cloud.unity3d.com/hub/prod/UnityHubSetup.exe`** → 404.
  The working URL is **`UnityHubSetup-x64.exe`** at the same path.
- **`https://aka.ms/vs/18/release/vs_community.exe`** → returns a **Bing search HTML page**
  with HTTP 200, not an installer. The correct channel is **`stable`**:
  `https://aka.ms/vs/18/stable/vs_community.exe`.
  **Lesson: verify a downloaded installer's `MZ` magic bytes, not just the status code.**
- **Unity batchmode without a licence** fails with:
  `[Licensing::Module] Error: 'com.unity.editor.headless' was not found.`
  It hangs rather than exiting. `Tests/compile-check.ps1` exists precisely to work around this.
- **Generating "top-down" maps without saying "axis-aligned"** produced isometric (45°-rotated)
  rooms whose walls do not match rectangular colliders. The working prompt phrasing is:
  *"Orthographic straight overhead … all four walls run exactly parallel to the edges of the
  image, NOT isometric, NOT rotated, NOT tilted, no perspective vanishing point."*
- **Bash heredocs for files containing Arabic** mangle UTF-8. Use the Write tool.
- **`sed` on Windows paths containing backslashes** repeatedly corrupted strings. Use Edit.
- **`python`/`python3`** is NOT installed on the old machine.

### Open problems / unresolved

| Problem | Notes |
|---|---|
| **Movement unverified** | The user's last words were that it still moved badly. B7 was found and fixed afterwards. **Verify first.** |
| **Blocker rectangles approximate** | Office and guard-room blockers are first-draft. |
| **Project still named `TrueDetective`** | Folder, `productName`, README. Should become DETECT. |
| **No music** | The available generation tooling refuses standalone music/SFX; its music model is restricted to another pipeline. Only TTS speech is available. Music needs an external source — **ask the user before downloading anything**. |
| **No Android build ever attempted** | First build will prompt for SDK/NDK/JDK paths. |
| **Locked cases 02–05** | Titles only, shown in the case-select screen. |
| **UI layout unverified** | Only the newspaper, menu and world screens have ever been seen. |

---

## 8. Next Steps

### 1. Verify the movement fix actually works — DO THIS FIRST
- **Where:** Unity editor, `Assets/Scenes/Main.unity`
- **What:** Open the project, press Play, skip the intro, press **ابدأ التحقيق**, advance
  through the newspaper and briefing to reach the archive hall.
- **Test both inputs:** first **WASD** (proves physics/room are sane), then **mouse click**
  (proves B7 is fixed). Click bare floor, then click directly on Samer.
- **Expected:** the room fills the screen vertically; the detective is visible and roughly
  1/7 of screen height; clicking the floor shows a pulsing amber ring and he walks there and
  stops; clicking Samer walks him over and opens the interrogation screen.
- **If it still fails:** read `Editor.log` with the command in §6 before changing code.

### 2. Tune movement feel and room geometry
- **Where:** `Assets/Scripts/World/Walker.cs` (top-of-class constants),
  `Assets/Scripts/World/WorldView.cs` (`ViewPadding`, `PlayerHeight`, `CameraLag`, `StandOff`),
  `Assets/Resources/Maps/case01.json` (blockers, spot positions).
- **What:** walk every room, note where the body catches on nothing or walks through
  furniture, and correct the blockers using the pixel→world formula in §5.5.
- **Priority:** office and guard room, whose blockers were never refined.
- **Expected:** all three rooms fully traversable; every spot reachable; no stalls.

### 3. Play the whole case end to end in the editor
- **What:** follow the solution path in §5.7. Also deliberately test: a wrong connection,
  submitting the report before holding E5 (must give the premature-accusation message),
  all three hint levels, and replay from the verdict screen.
- **Expected:** matches the 66 assertions that already pass headlessly.

### 4. Rename the project to DETECT
- **Where:** `Assets/Editor/ProjectSetup.cs` (`PlayerSettings.productName`),
  `README.md`, remaining "TRUE DETECTIVE" strings, and eventually the folder name.
- **Why:** legal (D8). **Do not ship as "True Detective".**
- **Note:** renaming the folder breaks nothing, but re-check `Tests/*.ps1` still resolve
  (they now derive paths from `$PSScriptRoot`, so they should).

### 5. Build and test an Android APK
- **Where:** Unity menu **True Detective → Build Android APK**
- **What:** first build will ask for SDK/NDK/JDK — the Android module is installed inside the
  editor, so leave **Edit → Preferences → External Tools** on "Installed with Unity".
- **Expected:** `Builds/TrueDetective.apk`. Install on a device and check touch input,
  Arabic rendering and portrait layout.

### 6. Build the Character Designer
- **What the user asked for:** customise the detective — suit colour, hat, coat, skin tone.
- **Suggested approach:** tint layers on the existing `ch_detective_*` sprites plus a portrait
  variant; store choices in `GameSettings`; add a `"designer"` screen reachable from the menu.
- **Why now:** cheap, high perceived value, and it was the agreed order after movement.

### 7. Build the Reconstruction feature — THE HEADLINE MECHANIC
- **See D10 for the full design.**
- **Suggested implementation sketch:**
  - New screen `"reconstruct"`, reusing the `WorldView` camera and room art in a
    non-interactive mode.
  - Player composes a theory: `{ actorId, startTime, waypoints[], actions[] }`.
  - Play it back on a clock; at each tick, test every collected `Evidence` against the theory
    state via declarative constraints added to the case JSON, e.g.
    `{ "evidence": "E3", "assert": "actorAt", "actor": "samer", "time": "20:20",
       "place": "corridor" }`.
  - On the first violated constraint: halt the replay, flash that evidence red, show its
    `notProves`/`proves` text.
- **This should eventually replace or sit alongside the fill-in-the-blanks report screen.**

### 8. Write case 02
- Only after the engine work above is stable. New mechanic per case (see the locked list in
  `Screen_Menu.cs`): 02 = timeline, 03 = forensics, 04 = alibi matrix, 05 = a previously
  "solved" case that was wrong.

---

## 9. Testing / Verification

### Already tested and passing (automated, no Unity licence needed)
| Suite | Result | Covers |
|---|---|---|
| `Tests/test-playthrough.ps1` | **66/66** | Intended path; wrong pair; wrong relation; premature accusation; different collection order; hint escalation; full replay reset; E5 gating |
| `Tests/test-arabic.ps1` | **13/13** | Letter joining, lam-alef ligatures, Latin/digit run preservation, bracket mirroring |
| `Tests/compile-check.ps1` | **0 errors** | Every game + editor script against real Unity assemblies |
| `Tests/check-fonts.ps1` | **ALL FONTS USABLE** | U+FE70–FEFF coverage of all 3 shipped fonts |

### Confirmed working visually in the Unity editor
- Project opens, scripts compile, the **True Detective** menu appears.
- The `Main` scene auto-generates.
- The world screen's **top bar, evidence counter, bottom bar and interaction prompt render**,
  and the interaction prompt **correctly detected proximity to a door** — proving movement and
  spot detection were functioning even while nothing was drawn.

### NOT yet verified — the next session must check these
- Whether the world renders at all after the B4/B7 fixes.
- Whether tap-to-move works.
- Whether the character sprite appears at a sensible size.
- Every UI screen's layout: notebook, connect board, confrontation, report, verdict, codex,
  settings, case select.
- The intro cinematic.
- Audio actually being audible after the B8 fix.
- Anything at all on a phone.

### How to verify a change
1. Run all four scripts in §6. All must stay green.
2. If you touched `CaseSession.cs`, `CaseData.cs` or `MapData.cs`, confirm they still have **no
   `using UnityEngine`** — otherwise `test-playthrough.ps1` stops compiling.
3. If you touched any UI, press Play and look, then read `Editor.log`.
4. Commit with a descriptive message.

---

## 10. Important Context From This Conversation

### User profile
- Speaks **Levantine/Jordanian Arabic**; prefers Arabic replies.
- **Beginner in Unity.** Wants to be told clearly which steps are the assistant's and which
  are theirs.
- Working toward a **training-programme showcase / competition**.
- Was hitting usage limits at the end — **value throughput over exploration**.

### Explicit instructions and corrections given
1. **"بدي التحرك بالكليك مش بالتحريك المستمر"** — click-to-move, not a joystick. The joystick
   was removed and `TouchStick.cs` deleted.
2. **"إذا بدك تستخدم أي إشي [من] فريساوند خذ موافقتي"** — ask before using any Freesound or
   external asset. All audio is currently synthesised.
3. **"بدي بيئات مختلفة حسب الكيس"** — environments must vary per case; this is why the case and
   map systems are fully data-driven.
4. **"بدي اشي اشبه للواقع وأقرب وحماس وتحقيق كامل"** — a complete, realistic investigation, not
   a stripped mini-game.
5. **"علوي مائل واقعي"** — top-down angled realistic art style (chosen over cartoon/silhouette).
6. **"ابني كل شي بالترتيب، خذ وقتك"** — build everything in order: movement → character
   designer → reconstruction → line of sight.
7. Name chosen: **DETECT** (they asked for something clear and close to "detect" after the
   True Detective name was refused).
8. Install everything on **E:** — the C: drive was nearly full.

### Design counsel given and accepted
- The original "connection board" idea was criticised as guessable and vague; a
  fill-in-the-blanks report was proposed and built instead. **Both now exist** — the board
  proves the contradiction mid-case, the report closes it.
- Movement was warned to be a net cost unless it carries a mechanic; the **Reconstruction**
  feature (D10) is what justifies it. This framing matters to the pitch.
- Reference material the user shared: *The Art of Game Design* (Jesse Schell), *Game Feel*
  (Steve Swink), Extra Credits, GMTK, GDC. `Game Feel` principles are visible in the
  procedural walk cycle, camera easing and acceleration curves.

### Tone/quality expectations observed
- The user values **honest diagnosis over reassurance**. Every bug was root-caused and
  explained rather than patched blindly; that was well received.
- Verifying before claiming was important — each claim of "it works" was backed by a test run.

---

## 11. Environment Setup for the New Laptop

### Required software
| Software | Version used | Notes |
|---|---|---|
| Windows | 11 Pro | Scripts are Windows PowerShell 5.1 |
| Unity Hub | 3.21.3 | **Required** — licence management lives here |
| Unity Editor | **6000.3.24f1** (Unity 6.3 LTS) | revision `4e7b9b5b6244` |
| Android Build Support | bundled module | Includes SDK/NDK/JDK |
| Visual Studio Community | 2026 (v18.10) | Optional — Unity compiles without it |
| Git | any | Repo already initialised |

### Download URLs that actually work
```
Unity Hub:      https://public-cdn.cloud.unity3d.com/hub/prod/UnityHubSetup-x64.exe
Unity Editor:   https://download.unity3d.com/download_unity/4e7b9b5b6244/Windows64EditorInstaller/UnitySetup64-6000.3.24f1.exe
Android module: https://download.unity3d.com/download_unity/4e7b9b5b6244/TargetSupportInstaller/UnitySetup-Android-Support-for-Editor-6000.3.24f1.exe
VS 2026:        https://aka.ms/vs/18/stable/vs_community.exe
```
Query other Unity versions via:
```
https://services.api.unity.com/unity/editor/release/v1/releases?limit=25&stream=LTS&platform=WINDOWS&architecture=X86_64
```

### Disk requirements
Unity Editor + Android module ≈ **14.2 GB**. Visual Studio ≈ 3.4 GB on the target drive plus
~1.5 GB that always lands on C: regardless of `--installPath`. Unity's installer also uses
~7 GB of temporary space on C: during extraction.

### Silent install examples
```bash
UnitySetup64-6000.3.24f1.exe /S /D=E:\GameDev\Unity\6000.3.24f1
UnitySetup-Android-Support-for-Editor-6000.3.24f1.exe /S /D=E:\GameDev\Unity\6000.3.24f1
vs_community.exe --add Microsoft.VisualStudio.Workload.ManagedGame --includeRecommended --installPath E:\GameDev\VS2026 --passive --norestart --wait
UnityHubSetup-x64.exe /S /D=E:\GameDev\UnityHub
```
All require **UAC elevation** — the user must approve. `/D=` must be last and unquoted.

### Manual steps only the user can do
1. **Sign in to Unity Hub** and activate a free **Personal** licence.
   *Without this, the editor will not open and batchmode fails.*
2. In Unity Hub → **⚙ Preferences → Installs → Locate** → point at
   `<install>\Unity\6000.3.24f1\Editor\Unity.exe` so the Hub does not re-download 4 GB.
3. **Projects → Add → Add project from disk** → the `TrueDetective` folder.

### Secrets / environment variables
**None are required.** The project has no API keys, no backend, no network calls, no database.
If a future feature needs one, use a placeholder such as `ANTHROPIC_API_KEY=<required>` and
have the user supply it; never commit a real value.

### Dependencies (Unity packages — already in `Packages/manifest.json`)
```
com.unity.ugui 2.0.0          ← contains TextMeshPro in Unity 6
com.unity.2d.sprite 1.0.0
+ built-in modules
```
No NuGet, npm or external C# libraries. `ArabicText.cs` deliberately has **no** third-party
Arabic dependency.

### Continue on a new machine
```bash
git clone <repo-or-copy-the-folder>
cd TrueDetective
powershell -NoProfile -ExecutionPolicy Bypass -File Tests\compile-check.ps1 -UnityDataDir "<path>\Editor\Data"
powershell -NoProfile -ExecutionPolicy Bypass -File Tests\test-playthrough.ps1
```
The other three scripts need no arguments. If Unity is not at
`E:\GameDev\Unity\6000.3.24f1\Editor\Data`, pass `-UnityDataDir`.

---

## 12. First Prompt for the New Claude Session

```
This is a continuation of an existing Unity project. I am on a new machine / new session and
you have no prior context.

Please do the following, in order:

1. Read HANDOFF.md in the project root COMPLETELY before doing anything else. It is at
   <project-root>/HANDOFF.md and was written specifically for you.

2. Inspect the actual project files before changing anything — at minimum:
   Assets/Scripts/World/WorldView.cs, Assets/Scripts/World/Walker.cs,
   Assets/Scripts/UI/Screen_World.cs, Assets/Resources/Maps/case01.json,
   and run `git log --oneline` and `git status`.

3. Tell me back, in your own words and briefly: what this project is, what state it is in,
   what the very next task is, and anything in HANDOFF.md marked UNKNOWN or
   NEEDS VERIFICATION that you think matters. Flag any place where the repository does not
   match what HANDOFF.md claims.

4. Then continue from the first unfinished item in the "Next Steps" section of HANDOFF.md.
   That is: verify the tap-to-move fix works in the Unity editor. Note that I have to run
   Unity myself — tell me exactly what to click and what to look for, and ask me to report
   back. You can read Unity's log yourself at
   %LOCALAPPDATA%\Unity\Editor\Editor.log to diagnose runtime errors without me pasting them.

5. Preserve all existing working functionality and the architectural decisions listed in
   section 3 of HANDOFF.md. Do not reintroduce a virtual joystick, do not move the UI into
   Inspector-wired prefabs, do not add `using UnityEngine` to CaseSession.cs / CaseData.cs /
   MapData.cs, and never assign Arabic text directly to TMP_Text.text.

6. After any change, run all four verification scripts from the project root and confirm
   they are still green:
     powershell -NoProfile -ExecutionPolicy Bypass -File Tests\compile-check.ps1
     powershell -NoProfile -ExecutionPolicy Bypass -File Tests\test-playthrough.ps1
     powershell -NoProfile -ExecutionPolicy Bypass -File Tests\test-arabic.ps1
     powershell -NoProfile -ExecutionPolicy Bypass -File Tests\check-fonts.ps1
   Expected: 0 compile errors, 66/66, 13/13, ALL FONTS USABLE.
   If Unity is not installed at E:\GameDev\Unity\6000.3.24f1, pass
   -UnityDataDir "<your-path>\Editor\Data" to compile-check.ps1.

7. Keep HANDOFF.md up to date as you work — update "Current Status", "Bugs / Problems /
   Blockers" and "Next Steps" whenever something changes, so another handoff is possible at
   any moment.

Reply in Arabic. I am a Unity beginner, so be explicit about which steps are yours and which
are mine.
```

---

## Handoff completeness review

Checked whether a new assistant with only this file and the repository could continue:

- ✅ Goal, scope and the non-obvious teaching premise are stated.
- ✅ Exact stopping point and what is unverified are stated.
- ✅ Every fixed bug is recorded with its root cause so it is not reintroduced.
- ✅ Failed approaches and bad URLs are recorded so time is not re-wasted.
- ✅ The pixel→world coordinate formula is written down; without it the map data is unreadable.
- ✅ The full case solution is included — required to test at all.
- ✅ Arabic and font constraints, the highest-risk area, are spelled out with hard rules.
- ✅ Test scripts were made path-independent and re-verified before writing this file.
- ✅ No secrets; none exist in this project.

**Known gaps, flagged honestly:**
- `NEEDS VERIFICATION`: whether the world renders and taps work after commit `cef28b4`.
- `NEEDS VERIFICATION`: blocker accuracy in the office and guard room.
- `UNKNOWN`: whether the generated art was produced under terms permitting commercial release —
  **the user should confirm the licence terms of their image-generation account before
  publishing or entering a competition.** Fonts are OFL and are fine.
- `UNKNOWN`: the new machine's paths. Everything here assumes `E:\GameDev\...`; pass
  `-UnityDataDir` and re-check the README if the layout differs.
