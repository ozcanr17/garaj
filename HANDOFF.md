# HANDOFF — GARAJ

> Bu belge tamamen bağlamsız yeni bir oturum için yazıldı. Oyun içeriği ve README
> Türkçedir; bu belge teknik aktarım olduğu için İngilizce/Türkçe karışıktır.
> Son güncelleme: 2026-07-27 (web/mobil port — `Garaj.Web`, Blazor WebAssembly).

---

## -1. Session log — web/mobile port (2026-07-27)

The user asked to make the prototype playable in a browser so it could be tested
on a phone. §0/§4.5/§7 below say mobile arrives "later, via Unity" and warn
against porting the *console UI* to mobile — this session did not do that. It
added a **new** presentation layer, `src/Garaj.Web`, a Blazor WebAssembly app
that references `Garaj.Core` exactly like `Garaj.Console` does and implements
its own touch-first screens. No line of `Garaj.Core` changed. This is a
pragmatic middle step ahead of the eventual Unity port, not a replacement for
it — it exists so the user can playtest on a real phone *today* without
waiting on art/Unity work.

- New project `src/Garaj.Web` (net10.0, `Microsoft.NET.Sdk.BlazorWebAssembly`),
  added to `GARAJ.slnx`. Runs entirely client-side (WASM) — `dotnet run
  --project src/Garaj.Web` serves it; no backend/API, no server-side game state.
- `GameState.cs` is the web equivalent of `GarajApp.Program` — same calls into
  `Garaj.Core` (`DiagnosisEngine.Run`, `NegotiationEngine`, `Disassembly`,
  `SaleEngine`, `Storyteller.MaybeFire`, …), reimplemented as an explicit
  screen-state-machine instead of nested blocking console loops, because a
  browser UI can't block on `Console.ReadLine()`. `Pages/Game.razor` renders
  one screen per `GameState.Screen` value; `Shared/*.razor` holds small reusable
  bits (status bar, confidence-band bar, observation card, back button).
- **The architectural rule is preserved and re-verified for the new layer**:
  `Game.razor` and `Shared/*` read only `PlayerKnowledge`/`ConfidenceRange`.
  The one exception is `Shared/TruthRevealView.razor`, which — like
  `Program.TruthReveal` — is allowed to read `VehicleInstance.Condition`
  because it *is* the truth-reveal screen. Says so in a comment at the top.
- **Blazor pitfall hit and fixed**: a child component (`BackBar`, and initially
  `TruthRevealView`) calling a `GameState` method directly from its own
  `@onclick` does **not** cause the parent `Game.razor` to re-render — Blazor
  only auto-re-renders the component that owns the event handler. Symptom was
  silent: `State.Back()` ran correctly (verified via a temporary
  `Console.WriteLine` in the WASM console) but the screen never visibly
  changed, so tapping "Geri" looked like a dead button. Fix: give the child an
  `EventCallback` parameter and bind it from `Game.razor`'s own markup
  (`<BackBar OnClick="State.Back" />`) so Razor captures `Game.razor` as the
  `EventCallback` receiver. **If you add another child component with its own
  button, wire it the same way — never call a `GameState`-mutating method
  directly from inside a child component's `@onclick`.**
- Verified end-to-end with Playwright (installed via `pip install playwright`,
  using the pre-installed `/opt/pw-browsers/chromium` — do not
  `playwright install`) at a 390×844 mobile viewport: full buy → diagnose →
  negotiate → repair job (disassembly/bolt/torque wizard) → test drive → sell
  → truth reveal loop, plus the document desk (read + compare + challenge) and
  back-navigation from every screen. Zero JS console errors.
- `.NET SDK` was not preinstalled in this container; installed via
  `apt-get install -y dotnet-sdk-10.0` (the `dotnet-install.sh` script is
  blocked by the egress proxy policy — `builds.dotnet.microsoft.com` — so use
  the distro package instead if this happens again).

### Must also avoid (adds to §6 below)

8. **Do not let a child Razor component mutate `GameState` from its own
   `@onclick` without an `EventCallback` bound by the parent.** See the pitfall
   above — the failure mode is a button that silently does nothing.
9. **`Garaj.Web` is not the Unity port.** It's disposable the same way
   `Garaj.Console` is (per §4.5/§7) — a fast way to playtest on real hardware,
   not the final mobile client.

---

## 0. Session log — Windows bring-up (2026-07-25)

Read this first if you are on Windows. Sections 1–7 below are the game/design
handover and remain authoritative; this section only records how the prototype
was brought up on a second platform. **No game code was changed.**

### Task of this session

The project's stated goal is **cross-platform: PC + Mac now, mobile later.** The
prototype was authored and only ever run on macOS. This session's job was to make
the exact same code **build and play on Windows** (dir `C:\Users\Elessar\Desktop\garaj`)
without forking the code — i.e. prove the "portable by construction" claim.

### What was completed

- Repo cloned to `C:\Users\Elessar\Desktop\garaj` via the GitHub CLI (`gh`, already
  authenticated as `ozcanr17`).
- **Installed the .NET 10 SDK on Windows: `winget install --id Microsoft.DotNet.SDK.10`**
  (got 10.0.302). This was the only missing piece — the machine had the `dotnet`
  host and the .NET 6 *runtime* but **no SDK**, so `dotnet build` failed with
  "No .NET SDKs were found".
- Built clean on Windows: `0 Uyarı, 0 Hata` (identical to macOS).
- Verified three ways:
  1. `--balance 1000 7` — economy metrics match the targets in §6 (avg condition 54,
     4.7 defects/car, ~30% masked, 6% time bombs, ~50% profitable). Sim layer is fine.
  2. Interactive play with a fixed seed — title art, day loop, İLANLAR listing,
     vehicle-detail screen (Teşhis / satıcıya soru / pazarlık) all render.
  3. Turkish glyphs (Ş, İ, ğ, ç) and the box-drawing UI render correctly in the
     Windows terminal — the `tr-TR` uppercase handling from §5 holds on Windows too.
- **Zero source changes.** `Garaj.Core` + `Garaj.Console` are already portable .NET;
  Windows was purely a toolchain-install exercise. This is the §4 "ports unchanged"
  invariant confirmed on real hardware.

### How to run on Windows

Open a **new** PowerShell window (so the freshly-installed SDK is on PATH), then:

```powershell
cd C:\Users\Elessar\Desktop\garaj
dotnet run --project src/Garaj.Console                       # oyna
dotnet run --project src/Garaj.Console -- 12345              # sabit seed
dotnet run --project src/Garaj.Console -- --balance 1000 7   # denge raporu
```

### Where we are stuck

Nothing new is blocked. The prototype is now playable on **both** macOS and Windows.
We are still stuck on the *same* human-judgement gate as §3: the user must play
3–4 times and report whether opening the hood produces merak/heyecan/gerilim. That
playtest can now happen on either OS. **Do not build more content before that answer.**

### Next plan

Unchanged from §4. Mobile (the eventual third platform) is **not** a console port —
per §4.5 / §7 it comes via the Unity port, which absorbs `Garaj.Core` as-is and adds
a render layer. Do not try to make the console UI run on mobile.

### Pitfalls hit this session (Windows-specific)

| Pitfall | What happened | Fix |
|---|---|---|
| `dotnet` present ≠ SDK present | The `dotnet` host and .NET 6 runtime existed, so `dotnet` "worked", but `dotnet build`/`--version` failed. | Always check `dotnet --list-sdks`. Install `Microsoft.DotNet.SDK.10` via winget. |
| Stale PATH in an open shell | After the winget install, shells opened *before* it don't see the SDK on PATH. | Open a new terminal, or prefix `export PATH="/c/Program Files/dotnet:$PATH"` for the current one. |
| ~~Piped stdin never EOF-exits cleanly~~ **(FIXED 2026-07-26)** | The game looped `Geçersiz seçim` forever on stdin EOF, so scripted verification hung. This was a genuine bug, not a scripting quirk: `Console.ReadLine()` returns `null` at EOF and `Ui.Menu` re-prompted instead of exiting. | Fixed in `Ui.cs`: `ReadLineOrEof()` sets an `_eof` flag; `Menu` returns `0` (Geri), which unwinds every screen and quits cleanly. `AskMoney`/`Pause` no-op after EOF. Piped input now exits 0. Still avoid putting `sed`/`head` between the game and its output file — that buffering issue is separate and real. |

### Must absolutely avoid

1. **Do not add Windows-specific (or any OS-specific) code paths into `Garaj.Core`.**
   The whole value of this session is that the bring-up needed zero changes. Keep the
   simulation OS-agnostic; portability is a load-bearing design property, not luck.
2. **Do not assume a machine with `dotnet` can build.** Verify the SDK, not the host.
3. **Do not port the console UI to mobile.** Console is disposable (§4). Mobile = Unity.
4. Everything in §6 "Things to absolutely avoid" still applies unchanged.

---

## 1. What this project is

**GARAJ** is a second-hand car restoration / diagnosis simulation game (mobile + PC),
designed by Rıdvan Özcan. The design authority is `GARAJ_Blueprint.md`, a ~1,300-line
Turkish game design document the user supplied. It is *not* in this repo — the user
holds it. Ask for it if you need the full spec.

The blueprint describes a ~23-month, 5-person AAA-lite project: Unity 6 URP, modular
3D vehicles (~120 separable parts each) rendered through a low-resolution pixel-art
pipeline, hidden-state simulation, scam/tamper system, document cross-verification,
bolt-level disassembly, a full paint pipeline, a living market, and a storyteller.

### The single most important thing to understand

The blueprint's central pillar is **Bilgi Asimetrisi** (information asymmetry).
Section §3.3 states: *"Asla kesin sayı gösterme"* — never show an exact number, only
a confidence band, and **confidence never reaches 100%**. The closing note says the
game's competitor is not Car Mechanic Simulator; it is *uncertainty itself*.

**The user also has AI-generated UI mockups (two PNGs) that CONTRADICT this pillar.**
They display `Ön Kaput 45%`, `PARÇALAR (64/120)`, `Durum 30%`, and
`GİZLİ HASAR RİSKİ: YÜKSEK` — i.e. they show the player the truth directly. That
turns the game into exactly the thing the blueprint says it must not be.

> **The blueprint is the spec. The mockups are not.** This was the key finding of the
> first session. Do not "implement the mockups."

What the mockups *did* get right and should be kept: the top-down garage scene, the
6-step restoration strip (Söküm → Onarım → Boya → Montaj → Test → Satış), the mobile
tab structure, the pigment-mixing colour panel, and the orbital vehicle viewer with
fixed view-angle buttons.

---

## 2. What has been completed

A **playable simulation-core prototype** — no art, no Unity, no Blender. It exists to
answer the blueprint's own Faz 0 question, *"Kaputu açmak heyecan verici mi?"*,
without spending money on a 3D artist.

- ~5,800 lines of C#, .NET 10, builds clean with **0 warnings, 0 errors**
- Verified end to end by scripted playthrough (buy → repair → test drive → sell → reveal)
- Builds and plays identically on macOS and Windows, with zero OS-specific code
- The simulation core landed in `a7859a2`; for current history run `git log --oneline`
  (deliberately not pinned here — a commit count goes stale the moment anyone commits)

### Run it

Easiest — **double-click a launch script** (builds Release, then plays):
- macOS: `GARAJ-Oyna.command` (Finder double-click; opens Terminal)
- Windows: `GARAJ-Oyna.bat`

From a terminal:
```bash
cd ~/Desktop/workspace/garaj
dotnet run --project src/Garaj.Console                       # play
dotnet run --project src/Garaj.Console -- 12345              # fixed seed
dotnet run --project src/Garaj.Console -- --balance 1000 7   # economy report
dotnet run --project src/Garaj.Console -- --events 80 7      # storyteller sim
```

**On a phone** (or any browser): `dotnet run --project src/Garaj.Web --urls
http://0.0.0.0:5217`, then open `http://<your-machine-ip>:5217` from the phone
on the same Wi‑Fi. See README.md § "Web / mobil" for details. This is a new,
separate touch UI over the same `Garaj.Core` — see §-1 above.

`dotnet` was installed via Homebrew during the first session. It is **not on the
default PATH** — prefix commands with `export PATH="/opt/homebrew/bin:$PATH"`.
(The `.command` script already adds the Homebrew path itself.)

### File map

| File | Responsibility |
|---|---|
| `src/Garaj.Core/Model.cs` | Part catalogue (33 parts), `Defect`, `PartInstance`, `VehicleInstance`, documents, `Cash.RoundTo` |
| `src/Garaj.Core/Knowledge.cs` | `ConfidenceRange`, `Belief.Combine`, `PlayerKnowledge`, `Observation`, `Rng` |
| `src/Garaj.Core/Diagnosis.cs` | 11 diagnosis methods + `DocumentAnalyzer` cross-verification |
| `src/Garaj.Core/Scams.cs` | 7 tamper types, `ScamEngine` (perceived condition, tells, km decay) |
| `src/Garaj.Core/Generation.cs` | Layered procedural vehicle generation (blueprint §8.1) |
| `src/Garaj.Core/Sellers.cs` | 6 archetypes, patience, reserve price, `ProvenLiar` |
| `src/Garaj.Core/Dialogue.cs` | Seller voice: 6 archetypes × 7 stances, part-specific answers |
| `src/Garaj.Core/Flavor.cs` | ~130 sensory inspection lines, condition-banded per method |
| `src/Garaj.Core/Negotiation.cs` | Findings become citable leverage; concessions, backfires |
| `src/Garaj.Core/Documents.cs` | Papers Please document desk — player finds contradictions |
| `src/Garaj.Core/Disassembly.cs` | Dependency-graph teardown, bolt states, strip/helicoil, torque |
| `src/Garaj.Core/Storyteller.cs` | Adaptive random-event director (§8.2), 15 events, real effects |
| `src/Garaj.Core/Economy.cs` | Valuation, repair, sale, equipment, `PlayerState` |
| `src/Garaj.Console/Program.cs` | Game loop and all screens |
| `src/Garaj.Console/Ui.cs` | Confidence-band rendering, colours, menus |
| `src/Garaj.Console/Balance.cs` | `--balance` harness |

### The architectural rule (do not break this)

> **`VehicleInstance` (truth) and `PlayerKnowledge` (belief) are separate objects.
> The presentation layer must NEVER read `PartInstance.Condition`.**

`Ui.cs` and every screen read only `PlayerKnowledge`. The sole exception is the
end-of-game "Gerçek" (truth reveal) screen in `Program.TruthReveal`.

This is not a style preference — it is the game itself. It makes it structurally
impossible for a screen to leak the truth by accident. Preserve it in any Unity port.

### Mechanics implemented

- **`ConfidenceRange`** — band + confidence, capped at `0.94`. Never 100%.
- **`Belief.Combine`** — overlapping evidence narrows the band and raises confidence;
  *non-overlapping* evidence is a contradiction and **lowers** confidence. This is how
  scams become sensible to the player without being told.
- **Tampers** — each blinds specific methods, is defeated by specific methods, has a
  lifespan in km, and leaks a probabilistic tell. Decays after purchase via
  `ScamEngine.AdvanceKm`.
- **Cross-verification** — service-record km > odometer ⇒ rollback; ruhsat engine no
  ≠ block engine no ⇒ engine swap; clean tramer + thick paint ⇒ unrecorded accident.
  *The last one only fires if the player also ran the paint-thickness gauge* — a
  deliberate reward for combining methods.
- **8 owner profiles** with distinct wear signatures, never told to the player.
- **Time bombs** — 8% of cars carry a defect undiscoverable before purchase (§4.4).
- **Seller tells** — leaked when lying, but honest sellers also show false tells 12%
  of the time. Tells must never be 100% reliable.

### Systems added 2026-07-26 (design invariants to preserve)

- **Inspection flavor** (`Flavor.cs`) — each diagnosis method emits condition-banded
  sensory lines (`ObservationKind.Detail`). These **do not reveal defects**; they are
  raw material for the player's own inference. They read **perceived** condition, so
  masking corrupts them too. Never let a flavor line state a defect outright — that
  belongs to the reveal system.
- **Seller dialogue** (`Dialogue.cs`) — 6 archetypes × 7 stances. The load-bearing case
  is asking about a defect the player **already found**: the seller either concedes
  (becomes negotiation leverage) or keeps denying it — a *proven* lie. `ProvenLiar` is
  permanent and widens the seller's price flexibility.
- **Leverage negotiation** (`Negotiation.cs`) — findings/contradictions/caught-masks/
  proven-lies become citable leverage that lowers the seller's reserve. Each leverage is
  single-use, costs patience, and can backfire on Duygusal/Aceleci archetypes. **This is
  the payoff that makes diagnosis matter** — inspection buys negotiating power, not just
  information. A valid leverage must never feel wasted (floor of ₺800 concession).
- **Document desk** (`Documents.cs`) — the Papers Please mechanic. The game **does not
  announce contradictions**; the player places two documents side by side and asserts
  which fields clash. `MethodId.Belgeler` now only produces a vague "something doesn't
  add up" hint (`DocumentAnalyzer.Skim`), never the answer. Wrong assertions cost time
  and seller patience so the player can't brute-force every pair. The paint+tramer case
  still requires the player to have run the paint gauge first (method combination).
  `DocumentAnalyzer.FindContradictions` is retained but **unused** — do not wire it back
  into gameplay; it would turn the desk's puzzle into a notification.
- **Disassembly + bolt mechanics** (`Disassembly.cs`) — the second pillar, *irreversibility*
  (§6.1), finally in gameplay. To repair a part you must first remove everything blocking
  it (`PartDefinition.RequiresRemoved`, transitive). Each removal reveals a hidden bolt
  state (temiz/paslı/sıkışmış); the wrong approach **strips** the bolt, which is permanent
  — the only recovery is drill+helicoil (+2h, +₺500). Reassembly torque without a torque
  wrench can seed a future-surfacing defect. **Invariants:** bolt state is a hidden physical
  fact stored once on `PartInstance.Bolts` (never re-roll on re-entry); a stripped bolt must
  stay stripped; clean bolts auto-resolve so simple repairs stay one-tap. The ₺1.500 torque
  wrench removes torque risk — it's the cheap early upgrade the reassembly step teaches you
  to want. **Balance watch:** without the wrench, a multi-part teardown rolls a torque flaw
  per reinstalled part (Dikkatli 12% each), so deep jobs compound risk — intended, but if
  playtesting says early game feels punishing, lower the `Disassembly.Torque` flaw chances
  before touching anything else.
- **Storyteller** (`Storyteller.cs`) — adaptive random-event director (§8.2/§4.1). Fires
  from the main-menu loop via `MaybeFire`, gated on ~150 in-game minutes of activity between
  events plus a 72% roll, with per-category anti-repeat. **Adaptive weighting** (`CategoryBias`):
  wealthy players draw more Dükkan trouble (tax/theft — the blueprint's irony), struggling
  players get biased toward Müşteri/Hikâye (breathing room). 15 events across
  dükkan/müşteri/piyasa/hikâye; effects are **concrete**, not flavor — money, reputation, time,
  a temporary parts-cost multiplier (`PlayerState.PartsMultiplier`, shown in the status bar),
  and equipment loss. `Sattığın Araç Bozuldu` is gated on `PlayerState.RiskySales`, which the
  sell screen increments when you offload a car whose real defects the buyer didn't catch —
  closing the karma loop. **Invariants:** arm the clock on the first `MaybeFire` (no event on
  turn one); events must remain skippable-proof (the player always resolves one, no soft-lock);
  keep effects going through `StoryContext` so the sim harness can replay them. Verify with
  `--events N seed` (deterministic; prints category distribution and a sample log) — target
  ~50% fire rate and all four categories represented.

---

## 3. Where we are stuck

**Nothing is technically blocked.** The build is green and the loop is playable.

The project is waiting on a **human judgement call that cannot be automated**:
the user needs to play the prototype 3–4 times and report whether opening the hood
actually produces *merak / heyecan / gerilim*. The blueprint's own Faz 0 decision
point requires this answer before further investment. Do not build more content
until it exists.

---

## 4. Next plan

1. **Get playtest feedback.** Ask the user directly. If the tension isn't there, the
   design needs revision — that's the point of having built this cheaply.
2. **Document cross-verification screen.** Highest-value next feature. The blueprint's
   §2.4 "ofis masası" (drag documents side by side, mark contradictions) is the
   mechanic nobody else in this genre has, and it is currently only half-surfaced —
   the logic exists in `DocumentAnalyzer` but the UI is a flat list.
3. **Disassembly dependency graph UI.** Data already modelled in
   `PartDefinition.RequiresRemoved`; no interface yet.
4. Then, in blueprint order: paint system, mods, market sim, employees, storyteller.
5. **Unity port comes last, and does not rewrite `Garaj.Core`** — it moves in as-is,
   with Unity as a pure render layer. The console UI is disposable; the simulation is not.

---

## 5. Pitfalls we already hit — do not repeat these

| Pitfall | What happened | Fix |
|---|---|---|
| `Math.Round(decimal, -2)` | Throws `ArgumentOutOfRangeException` — .NET does not support negative digits on `decimal`. Crashed on first run. | Use `Cash.RoundTo(value, 100)` in `Model.cs`. Never negative digits. |
| Turkish uppercase | `ToUpperInvariant()` renders "SATIŞ" as `SATıŞ` and "YANILGI" as `YANıLGı`. | `Ui.Header` uses `ToUpper(CultureInfo.GetCultureInfo("tr-TR"))`. |
| Namespace collision | A `Garaj.Console` namespace collides with `System.Console`. | Console app uses `namespace GarajApp` and `using Sys = System.Console`. |
| Turkish `dotnet` CLI output | Build output is Turkish ("Oluşturma başarılı oldu"). Grepping for "Build succeeded" or "error" silently fails. | Grep for the numeric `0 Hata` / `0 Uyarı`, or check exit codes. |
| Error metric measured the midpoint | The truth-reveal listed "sandın 0-87, gerçek 81" as a mistake. It isn't — the band *contained* the truth. Wide-but-honest ≠ wrong. | Error = distance from truth to the nearest **band edge**, 0 if inside. Blind spots (never examined + actually broken) are a separate, second list. |
| Per-item probability rolls | Rolling per tamper *definition* made 79% of cars tampered; rolling per *defect* made 29% carry a time bomb. | Roll **per car first**, then pick how many. |
| Two formulas for one concept | Asking price and restored value used different age/km maths, so restored value came out *below* asking price. Nonsense. | Asking price is now derived from `Valuation.RestoredValueBand`. One source of truth. |
| Cost tiers drifting apart | `PartInstance.TrueRepairCost` and `Valuation.EstimateFor` must use **identical** condition tiers, else the player's estimate is systematically biased — that's not uncertainty, it's just a bug. | Both are commented to point at each other. Change them together. |

---

## 6. Things to absolutely avoid

1. **Never let the presentation layer read `PartInstance.Condition`.** If a screen
   needs a number, it comes from `PlayerKnowledge`. Only `TruthReveal` is exempt.
2. **Never display an exact condition figure to the player.** No `45%`. No `64/120`.
   No `GİZLİ HASAR RİSKİ: YÜKSEK`. Bands and hedged language only.
3. **Never let confidence reach 1.0.** The cap is `ConfidenceRange.MaxConfidence = 0.94f`.
   Blueprint: *"Güven asla %100 olmaz. Bu, gerginliği korur."*
4. **Never treat the mockup PNGs as the specification.** See §1.
5. **Never tune balance by intuition.** Run `--balance` over ≥3 seeds and check every
   target in the table below. The first generation model *felt* fine and produced
   4 profitable cars out of 1,000.
6. **Never make tells 100% reliable**, and never make a scam 100% undetectable
   without a probabilistic hint. Blueprint §2.3.
7. **Do not rewrite `Garaj.Core` for Unity.** It is designed to port unchanged.

### Balance targets (verify after ANY generation or pricing change)

| Metric | Target |
|---|---|
| Average true condition | 50–60 |
| Defects per car | 3–5 |
| Cars carrying a tamper | ~30% |
| Cars carrying a time bomb | 5–8% (blueprint §4.4) |
| Profitable under selective repair | ~50% |
| Margin on masked cars | must be **worse** than clean cars — otherwise scamming costs the player nothing |
| Sale payout ÷ displayed market band | **1.00×**, and 0 cars out of band |
| Cars with a catchable document contradiction | ~35–40% — below ~20% the document desk becomes dead content nobody opens |
| Median asking price vs starting capital (₺60.000) | median must stay **under** capital, or the first purchase is impossible |

The last row is a regression guard. `TrueMarketValue` once omitted the age factor that
`RestoredValueBand` applied, so restored cars sold for 3–4× the band shown to the
player. `TrueMarketValue` is now *derived from* `RestoredValueBand` specifically so the
two cannot drift. **Never reintroduce a second, independent valuation formula.**

Median margin at full asking price with *perfect* knowledge should sit near **−₺3.000**.
Profit must come only from negotiation (10–25%) and correct diagnosis. If buying blind
is profitable, the game is broken.

### Two blueprint errors already corrected — do not revert them

1. **Car values vs part prices.** The blueprint prices cars at ₺42.000 (a 2015 figure,
   copied into the mockups) while part prices are realistic for 2026 (clutch kit
   ₺3.200). At that ratio full restoration costs ~3× the car's value and every vehicle
   is economic scrap. Car base values were raised to 2026 reality (Şahin ₺145.000) and
   starting capital (raised to ₺250.000 during bring-up, then deliberately
   tightened to ₺60.000 so the first turn forces a choice between equipment and
   the car itself — see the median-vs-capital balance target above).
2. **Age treated as wear.** A 36-year-old car's parts are not 36 years old — worn parts
   get replaced when their service life ends; that is what maintenance *is*. Wear is now
   computed from km **on the individual part**, with maintenance quality modelling the
   fraction of replacements actually performed. Before this fix, average vehicle
   condition was 22/100.

---

## 7. Scope reality check

The blueprint is honest that this is an AAA-lite project (§14). What AI assistance can
and cannot do here:

- **Can build:** the entire simulation layer, data schemas, dependency graphs, diagnosis,
  scam engine, document logic, procedural generation, market sim, and all UI code.
- **Cannot build:** the art. §9.4 estimates ₺1,000,000+ for 25 vehicles. That is a real
  human cost and no amount of AI removes it. The blueprint's own MVP (§14) — *3 vehicles,
  full diagnosis + scam system, full disassembly + paint, one shop, no story* — remains
  the right target.
