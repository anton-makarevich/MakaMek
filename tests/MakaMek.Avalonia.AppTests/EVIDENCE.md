# Harness evidence log

This project is a large, dull chunk of test infrastructure. The case for carrying it has to be made
with evidence, not assertion, so every use of it gets a row below — including the uses that found
nothing, because a harness that only ever agrees with the existing tests is not worth its weight.

**Keep this file updated in the same commit as the test that produced the entry.** A log written
later is a log nobody trusts.

## What this harness does that the existing Avalonia tests cannot

`tests/MakaMek.Avalonia.Tests` constructs views directly — `new BattleMapView()`, `new
TurnNotificationBanner()`. Nothing there boots `App`, builds the real service graph, or navigates.
So no test in that project can see a defect that only appears once the application is assembled.

It has to be a separate project: `AvaloniaTestApplication` is assembly-wide, the existing project
claims it for a stub `TestApp`, and only one headless platform may exist per assembly.

## Columns

| Date | Feature under test | What the existing tests said | What the harness showed | Would a human have caught it first? |
|---|---|---|---|---|
| 2026-09-29 | *(motivating case, not a harness catch)* initiative winner banner, PR #1534 | 10 tests green; mutation showed **1 of 14** could detect the defect | n/a — harness did not exist yet. The maintainer found it by playing the game: *"I don't see Initiative results banners, the movement phase one comes right after the initiative phase announcement, nothing in between"* | Yes, and he did. That is the cost this harness is meant to remove |
| 2026-09-29 | harness itself — boot and render | n/a | Real `App` boots headlessly, builds its `ServiceProvider`, and `MainWindow` renders a frame with varying pixels. No blockers in `RegisterDesktopServices` | n/a |
| 2026-09-29 | banner binding chain, `BattleMapView` to `TurnNotificationBanner` | control tests build the banner standalone; view model tests never render. Nothing covered the join | Binding resolves and the queue reaches the control inside the real view, with a view model from the real service graph | No. This is a gap neither side's tests reach |
| 2026-09-29 | *(negative result, recorded deliberately)* can the harness catch a broken binding path? | n/a | **No, and it does not need to.** Typing `TurnNotificationsTypo` into `BattleMapView.axaml` fails the **build**: this project sets `AvaloniaUseCompiledBindingsByDefault` and the view declares `x:DataType`, so a bad path is a compile error, not a silent runtime failure | The compiler catches it first |
| 2026-09-29 | bundled units load with no network, via `LocalFolderResourceStreamProvider` over the repo's `data/` folder | nothing covered this; the app's configured source is remote | 199 `.mmux` units load in-process, so a harness game needs no network | n/a |
| 2026-09-29 | **the harness's own async helper** | the test was green | **The helper was vacuous.** An impossible assertion inside `Run(Func<Task>)` passed. Fixed by wrapping the body to return a value, which forces the `Func<Task<T>>` overload. Mutation now fails as it should | No. Only a mutation check found it |
| 2026-09-29 | **initiative winner banner, PR #1534, in a real two bot game** | 14 unit tests green after the ordering fix; the maintainer still saw no banner | **Second defect, and the one he reported.** The winner is never announced at all. `BattleMapViewModel` subscribes with `Game.Commands.ObserveOn(_dispatcherService.Scheduler)`, so `ProcessCommand` runs after the command arrives. The server's auto roll publishes the whole initiative burst synchronously, so by the time the deferred `DiceRolledCommand` handler runs, `TurnPhase` is already `Movement` and the `when TurnPhase == Initiative` guard is false | **No.** He found the symptom by playing; the cause was only visible with a real game running |
| 2026-09-30 | layer 8 HUD views, `NullToBooleanConverter` in `UnitRecordSheet` | 332 Avalonia tests green, including ones that build `UnitRecordSheet` | **Crash on launch.** The converter was registered in the tests' stub `TestApp` but not in the real app, and the repo registers converters like this one locally per control. Constructing `BattleMapView` under the real `App` threw `KeyNotFoundException: Static resource 'NullToBooleanConverter' not found`. Fixed by declaring it in the control's own resources, the way its sibling panels do | No. The stub app hid it, so every view test passed |
| 2026-09-30 | map controls drawer button sizing | nothing measures rendered geometry | **Inconsistent tap targets.** The zoom buttons were text buttons sized to their glyph: `+` 28x27, `-` 25x27, between 40x40 icon buttons. Replaced with `ActionButton` and new zoom icons, and `MapControlsDrawer_UsesOneButtonSize` now asserts one square size | Yes, by eye, once it was on screen. The maintainer asked to see it precisely because of this class of thing |
| 2026-09-30 | squad card, `UnitActionHintConverter` against `UnitStatusTextConverter` | both converters unit tested, each correct on its own | **The same word twice.** An immobile unit rendered `Immobile` from the status label and `Immobile` again from the action hint. The two converters overlap on destroyed, shut down and immobile. The hint now returns empty for those and reports only weapon readiness | No. Neither converter is wrong alone; only the rendered card shows the overlap |
| 2026-09-30 | squad card armor and structure bars | n/a | **Bars drew outside their cells.** The Fluent `ProgressBar` carries a fixed minimum width, so in a narrow star column it overflowed: measured at x=-68 with width 200 in a 70px cell, painting over the percentage label beside it. Fixed with `MinWidth="0"` and stretch | Yes, visibly, once rendered with real units |
| 2026-09-30 | squad bar and inspection drawer with a real game | presentation tests cover the view models; no test rendered them | Binding chain holds: four units join, four cards render, the drawer follows `InspectedUnit` without touching `SelectedUnit`, and a pinned drawer survives `NotifySelectedUnitChanged` on screen | n/a |
| 2026-09-30 | map controls drawer alignment | nothing measures rendered position | **The column did not line up.** Drawer children stretch to the widest item (the 145pt export button) and an `ActionButton` draws its 40pt circle at the left edge of its box, so icons sat at x=943 while the toggle sat at x=1052. `MapControlsDrawer_LinesUpWithItsToggle` now asserts one shared left edge | Yes, and the user spotted it in the screenshot before the test existed |
| 2026-09-30 | the battle map itself under the HUD | n/a | Two things were needed to render it: the deploy command silently no-ops without `PlayerId` (`OnDeployUnit` returns at `player == null`), and hex tiles decode off the UI thread so a single settle captures bare canvas. With the repo's own `grasslands.mmtx` wired in as a local terrain provider, the map draws and the HUD can be judged over real terrain | n/a |
| 2026-09-30 | squad bar width against the map controls drawer | converter unit tests passed against the first version | **The gap stayed open after the drawer closed.** The bar reserves space by reading the drawer's rendered width, and a hidden control keeps the bounds it was last arranged with, so the width stayed 145 and the bar never got it back. Visibility is now part of the binding. The rendering test caught it; the converter tests could not, because they were written against the same wrong assumption | No. Both the converter and its tests assumed a collapsed control measures zero |
| 2026-09-30 | squad card fields and bar scrolling | card tests check click behaviour, not layout | **The movement summary was cut at every width.** The detail line wanted 262pt in about 215, so `W5/R8 MP` always rendered as `W5/R…`. `SquadCard_ShowsEveryFieldInFull` compares each label's arranged width against what it asked for, which catches any field being squeezed rather than just this one. Wheel scrolling, clamping at both ends, and bringing the active unit into view are covered by rendering tests; all three were mutation checked | Yes, the user spotted the clipping in a screenshot |
| 2026-09-30 | squad bar scrollbar and drag panning | n/a | **Two of these tests were vacuous when first written and only mutation showed it.** The scrollbar-guard test raised its pointer events on the ScrollViewer, which never invokes that control's own tunnel handler, and then dragged in the direction that clamps at offset zero, so it passed with the guard removed. Raised on the real source and dragged the other way, it fails without the guard as it should. Worth repeating: a rendering test that synthesises input proves nothing until it has been made to fail |
## Rules for entries

- Record what the **existing** tests said first. The argument for this project is the gap between
  that and reality, so an entry without it proves nothing.
- If the harness found nothing, say so. Those rows are the honest denominator.
- Quote the maintainer verbatim where a defect was reported by him — that is the strongest evidence
  that the gap is real and not self-assessed.
- Note where a defect was reachable only by rendering or navigating. Those are the rows that justify
  a headless *application* harness rather than more view tests.

## The dispatch trap, stated precisely

`HeadlessUnitTestSession.Dispatch` will silently swallow every assertion failure in async work unless
the body returns a value.

- `Dispatch(async () => { ... })` — swallowed. Binds to the `Action` overload, becomes `async void`.
- `Dispatch(typedFuncTaskVariable)` — **also swallowed.** Typing the delegate does not save you; this
  was verified by mutation on this project's own helper, which reported green against an impossible
  assertion.
- `Dispatch(async () => { await body(); return true; })` — propagates. Returning a value is what
  selects the `Func<Task<T>>` overload.

Earlier vault guidance said the API "has no `Func<Task>` overload". That is the wrong diagnosis: the
overload resolution is the problem, and the cure is returning a value. Any new async headless test
must be mutation-checked once before it is trusted.

## Why the unit tests could not have found that

The view model tests call `game.HandleCommand(...)` synchronously and read `Game.TurnPhase` on the
next line, so the guard always holds. The scheduler hop exists only once `BattleMapViewModel` is
subscribed the way the application subscribes it. **Any `when` clause that reads live game state
inside a command handler has this hazard**, because the handler is deferred and the state is not.
That is a class of defect, not a one off, and it is reachable only by running the assembled app.

The test lives on a combined branch rather than here: this project is cut from `main`, and
`TurnNotificationKind.Initiative` only exists on the feature branch. That is the intended shape.
The harness is infrastructure to be merged into a feature branch when verifying that feature.

## Test isolation: why view models are built with a per test dispatcher

Symptom: a test that ran a real game passed alone and failed when run with the others, and failed
*completely* - the view model received no commands at all, while the game itself published
everything correctly.

Cause: `AvaloniaDispatcherService.Scheduler` returns the static `AvaloniaScheduler.Instance`, which
binds to whichever dispatcher first uses it. Constructing a `BattleMapViewModel` binds it. The
headless session isolates the application per test, so the second test to construct one subscribes
through a scheduler bound to a dispatcher that no longer exists, and `ObserveOn` delivers nothing.
Proved with a probe: scheduling a trivial action on that scheduler and pumping never ran it.

Fix: build view models with `ActivatorUtilities.CreateInstance<BattleMapViewModel>(services,
dispatcher)` passing a `TestDispatcherService` whose scheduler is built from the dispatcher's own
synchronization context, captured per instance inside the dispatch. Two properties matter and both
are load bearing. Delivery stays genuinely **deferred**, because deferred delivery is what caused the
defect this harness found and an immediate scheduler would hide it. And it still runs on the **UI
thread**, so a view model bound to a real view can raise collection changes without tripping
Avalonia's thread affinity checks. An `EventLoopScheduler` gives the first property and loses the
second, which breaks the moment a test renders.

Stable across five consecutive runs, and the mutation still fails, so the test is both reliable and
still sensitive.

**Two wrong turns worth recording**, because both looked convincing:

1. Disabling xUnit parallelism. It is not a race between tests, it is a static bound once.
2. Pumping the dispatcher at `SystemIdle` priority. One run went green and I called it fixed. It was
   test ordering luck: raising the wait budget afterwards made things *worse*, which is what proved
   the condition never becomes true rather than arriving late. A fix that only sometimes works is
   indistinguishable from a fix, for exactly one run.

## What this harness is *not* for

Compiled bindings are on by default in `MakaMek.Avalonia` and the views declare `x:DataType`, so a
misspelled binding path is a build error. Do not justify this project on catching those; the compiler
is faster and more reliable at it. The gap it covers is narrower and harder to reach: whether an
announcement actually arrives on screen, in what order, and whether the assembled application
behaves once DI, navigation and rendering are all involved. Those are invisible to the compiler and
to view model tests alike.
