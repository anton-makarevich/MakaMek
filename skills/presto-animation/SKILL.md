---
name: presto-animation
description: "Use this skill whenever you add, change or debug motion in the MakaMek Avalonia client — animations, transitions, springs, easing, keyframes, timelines, stagger, pulses, glows, shakes, fades, slides, selection/highlight rings, damage numbers, turn banners, hover/press states, panel reveals, drag or pan-zoom, sprite/particle effects, or reduced-motion handling. Trigger it even when the user does not say 'Presto': any request like 'make the selection circle animated', 'this transition feels stiff', 'add a nice fade', 'animate the health bar', 'make the map pan smoothly', or any edit to an Avalonia Animation/KeyFrame resource, DispatcherTimer-driven UI motion, or a RenderTransform used for effect. Also trigger when reviewing motion code, when a control is rebuilt and animations leak, or when animations must respect accessibility settings. Do not hand-roll animation loops or XAML keyframes before consulting this skill — Presto is the project's chosen motion layer and has specific lifetime, threading and transform-ownership rules."
metadata:
  author: MakaMek project conventions
  version: "1.0"
  framework: MonacoLabs.Presto 0.1.0-preview.1 + AvaloniaUI 12.1.4
  reference-doc: docs/analysis/presto-animation-capabilities.md
  # Hidden from the model's available list; loaded on demand by id.
  # See src/MakaMek.Avalonia/AGENTS.md, which points here for motion work.
  opencode/autoinvoke: false
---

# Presto motion in MakaMek

Presto (`MonacoLabs.Presto.Avalonia`) is the animation layer for the MakaMek Avalonia client. It is a preview library whose API is settled but not frozen, so this skill encodes both the *patterns* that work in this codebase and the *verification habit* that keeps them correct.

Read `references/api-cheatsheet.md` for exact signatures and `references/makamek-patterns.md` for complete, copyable patterns. The full capability inventory, ranked use cases and the selection-ring pilot live in `docs/analysis/presto-animation-capabilities.md`.

## Before you write motion

1. **Check the dependency is actually there.** `MonacoLabs.Presto.Avalonia` is planned but may not yet be referenced. Look in `src/MakaMek.Avalonia/MakaMek.Avalonia/MakaMek.Avalonia.csproj` (and `MakaMek.Avalonia.Controls.csproj` if you animate controls there). If it is missing, say so and ask before adding a preview dependency.
2. **Verify the API against upstream.** This library moves. Fetch the section you need from `GUIDE.md` (https://github.com/stevemonaco/MonacoLabs.Presto/blob/main/GUIDE.md) or the matching spec under `docs/specs/` (https://github.com/stevemonaco/MonacoLabs.Presto/tree/main/docs/specs) rather than writing from this skill alone. The skill is a map, not the territory.
3. **Load `avalonia-ui` first** if the change also touches AXAML, styles, bindings or control templates. Use `style-avalonia-app` when adding motion resources to `MakaMek.Avalonia.Controls/Styles`.
4. **Decide the surface**: code-behind/builder for board visuals (`UnitControl`, `PathSegmentControl`, `WeaponAttackIndicatorControl`, `HexRenderControl`); attached properties/recipes for AXAML views (`Views/**`). Both are supported; pick the one that matches how the element is built.
5. **Keep game rules out of it.** Presto is presentation only. Core decides *what* happened; the UI decides *how it looks*. Never sequence game state through an animation callback (`OnComplete` must not advance the game). If a visual must follow state, drive it from an existing observable/VM and let the animation follow.

## The five rules that prevent most bugs

1. **Never share a `RenderTransform` with Presto.** `X`, `Y`, `Rotate`, `Scale`, `RotateX/Y` compose into a bridge-owned `TransformGroup` written to `RenderTransform`. MakaMek assigns `RenderTransform` manually on `UnitControl` (facing) and `PathSegmentControl`. Animate **child visuals** of those controls, never the rotated control itself. If you must animate the rotated control, animate a wrapper and keep facing on the inner element.
2. **Give every animation a lifetime.** Use `PrestoAvalonia.CreateScope(control)` (a `ControlScope` reverts on detach) or cancel/revert explicitly in `Dispose`. `PrestoAvalonia.WhenDetached` defaults to `Pause`, which stops the visual but retains the animation and its targets — not enough for controls that are rebuilt. Board controls are rebuilt in `BattleMapView.RenderMap`; dispose the old ones.
3. **Start animations on edges, not every tick.** `UnitControl` polls state at ~30 Hz and calls update methods every tick. Compare against the last applied state (an enum or bool field) and only start/cancel when it changes. Starting an animation 30×/s restarts it 30×/s and looks broken.
4. **Respect reduced motion.** Add `.WhenReduced(...)` to every builder that moves or loops, and set the flag from settings: `Presto.Defaults.ReducedMotion = settings.ReduceMotion;` (or `PrestoAvalonia.MotionPreference = MotionPreference.System`). Loops that must stop under reduced motion need an explicit reduced form (`r.Loop(false)`); `WhenReduced` builds a *new* builder that keeps timing/loop/callbacks but drops the original properties and keyframes, so restate anything the reduced state still needs (e.g. `r.Draw("0 1")` — omit it and a self-drawing stroke stays hidden).
5. **Bump the version for `src/` changes.** Any change under `src/` needs a patch bump of `VersionPrefix` in `Directory.Build.props` (`0.65.14` → `0.65.15`). Docs- and skill-only changes do not.

## Choosing the primitive

| You want | Use | Why |
|---|---|---|
| One property, one target, fire and forget | `control.Animate(b => …)` | Simplest; returns an `Animation` you can await/cancel |
| Reusable motion vocabulary | a `static readonly AnimationBuilder` field | Builders are recipes; `Animate` snapshots them, so sharing is safe |
| Several properties with different timing per property | `.X(k => k.To(…).To(…))` | Per-property keyframes |
| A staged sequence (hit → recoil → settle) | `Presto.CreateTimeline().Add(target, b => …, position)` | Labels, relative positions, `Seek` for scrubbing |
| Many elements offset in time | `Presto.Stagger(ms, new() { From = StaggerFrom.Center })` | Also works for values and timeline positions |
| Natural settle | `Eases.Spring(stiffness: …, damping: …)` / `Springs.Snappy` | Duration is derived from settling time; do not also set `Duration` |
| Hover/press/focus/state styling | `Motion` attached properties or `card.WhileHover(b => …)` | Returns to rest automatically; layers in declaration order |
| Element appears/leaves/disposes | `Presence.Enter/Exit` (XAML) or `AnimateExit` (code) | Correct classification, ghost snapshots for exits |
| Panels resizing with content | `AnimateContentSize` | Real layout size, no text rewrap |
| Elements moving because layout changed | `AnimateLayout` / `presto:Animate.Layout` | FLIP on the compositor |
| Value that changes continuously (camera, crosshair) | `CreateAnimatable` / `MotionValue` + `Follow` | One paused animation retargeted per change |
| Self-drawing line/ring | `CreateDrawable(shape)` + `.Draw(from: "0 0", to: "0 1")` | Stroke reveal without manual dash math |
| Moving along a path | `CreateMotionPath(shape)` or `(points, curviness, autoRotate)` | Returns `X`, `Y`, `Rotate` tweens |
| One-shot flourish (destroy, crit, hit) | `Recipes.Burst`, `Sparkles`, `Confetti`, `Shake`, `Pulse` | Render-thread overlays; keep counts low on mobile |
| Motion that must survive a busy UI thread | `AnimateOnCompositor` / `AnimationThread.PreferCompositor` | Only `X/Y/Rotate/Scale/Opacity`; check with `CanAnimateOnCompositor` |

## Working in this codebase

- **Board controls are code-built.** Prefer the builder API there. `UnitControl` already has the state in `UnitState`; add a private `_lastX` field for edge detection instead of new observables.
- **Hex geometry is shared.** Use `HexagonGeometry.GetCorners()` and `HexCoordinatesPixelExtensions.HexWidth/HexHeight` for any ring, highlight or path geometry so it lines up with `HexRenderControl`.
- **Presentation owns testable logic.** UI is excluded from coverage. Put pure decisions (which selection role, which motion variant, which token) in `MakaMek.Presentation` and unit-test them; leave the Avalonia layer to compose visuals. See `UnitSelectionKindResolver` in the pilot design.
- **Colors come from resources.** Read brushes via `IAvaloniaResourcesLocator` / `{StaticResource}` (`MechArmorBrush`, `MechStructureBrush`, `DestroyedBrush`, `HeatBrush`); do not hardcode hex values in animation code.
- **Existing XAML animations stay valid.** `Styles/Animations.axaml` holds `DamageLabelAnimation` and `TurnNotificationAnimation`. Replace them only as part of a deliberate change, not incidentally.
- **Mobile and Browser matter.** The app ships Desktop, Browser (WASM), Android and iOS. Presto's Browser/Android heads are less exercised than Desktop and iOS has not run upstream: verify on at least Desktop + Browser for anything that runs per frame, and prefer small particle counts and short durations.

## Anti-patterns

- Starting an animation inside the 30 Hz poll without edge detection.
- Setting `RenderTransform` on a control that Presto is animating (facing, zoom), or forgetting that shorthands act about `RenderTransformOrigin` (default top-left).
- Calling `Cancel()`/`Revert()` on the control — they belong to the returned `Animation`; keep the handle.
- Assuming `WhenReduced` keeps the original properties. It builds a *new* builder with only its own properties, so restate anything the reduced state needs (a `Draw` reveal that isn't repeated leaves the stroke invisible).
- Animating `IsVisible` for fades (layout thrash); animate `Opacity` and keep the element laid out.
- Forgetting to `Cancel()`/`Revert()` a loop on deselect, dispose, or map rebuild.
- Adding `Blur` and `Shadow` (or two clips) to the same visual — Avalonia allows one effect and one clip; it throws.
- Using `OnComplete` to advance game state or mutate Core models.
- Hardcoding durations everywhere instead of a small token set (see `references/makamek-patterns.md`).
- Writing XAML `<Animation>`/`KeyFrame` resources for new work when a Presto builder or recipe does the job with less code.

## Verify before handing back

- Rapid toggling (select/deselect, hover in/out, open/close) leaves no stuck or doubled animation.
- A control removed from the tree stops animating (check the DevTools tickable list if available).
- Reduced motion produces a static or short-fade result.
- Frame pacing is unchanged on a busy board (12+ units).
- `dotnet build MakaMek.slnx` passes and, for `src/` changes, `VersionPrefix` is bumped once.
- If you added a package or changed a public UI behaviour, note it in the PR description; if you added a reusable pattern, consider adding it to `references/makamek-patterns.md`.
