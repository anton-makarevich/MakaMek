# Presto Animation Framework — Capabilities for MakaMek

Evaluation of [MonacoLabs.Presto](https://github.com/stevemonaco/MonacoLabs.Presto) (MIT, `0.1.0-preview.1`) and an inventory of what it can do for the MakaMek Avalonia client, plus a concrete design for the first pilot: an animated unit selection ring in `UnitControl`.

Sources reviewed: `README.md`, `GUIDE.md` (the full manual), `docs/ARCHITECTURE.md`, `docs/specs/core/*` and `docs/specs/avalonia-bridge/*`, the `samples/PrestoGallery` reference pages (`Pages/`) and showcase pages (`Showcase/Pages/`), and the `samples/Tempo` app.

---

## 1. Verdict

Presto is a very good fit for MakaMek's UI layer, for four reasons:

1. **Exact platform match.** Presto targets `net10.0` and is built against **Avalonia 12.1.4** — the same versions pinned in `Directory.Build.props` (`AvaloniaVersion=12.1.4`, `net10.0`). No version conflict.
2. **Code-first API.** MakaMek builds its board visuals in C# (`UnitControl`, `PathSegmentControl`, `WeaponAttackControl`, `HexRenderControl`), not in XAML. Presto's builder (`control.Animate(b => b.X(…).Ease(…))`) is designed for exactly that, with typed extension methods instead of string property names.
3. **It replaces hand-rolled animation code, not the app architecture.** Presto is additive: an `Animation` is just a timer that writes Avalonia properties at `BindingPriority.Animation`. Nothing else in the app has to change.
4. **Breadth.** Beyond tweens it ships springs, timelines, stagger, presence/lifecycle, state motion, drag/pan-zoom, scroll observers, path drawing/morphing, image filters, particles, render-thread scenes, and a set of ready-made controls.

The main risks are maturity (self-described as experimental, AI-created, API not frozen) and platform coverage (Windows fully exercised; Browser/Android partially; iOS compiles but has not run). See §6.

---

## 2. Shape of the library

| Package | Contents | MakaMek relevance |
|---|---|---|
| `MonacoLabs.Presto` | Framework-free engine: clock, timers, animations, timelines, tweens, values, eases, springs, stagger, scopes, particles, animatable, motion values | Could drive view-model/state timing too, but MakaMek's timing belongs in Core; use only for UI |
| `MonacoLabs.Presto.Avalonia` | Frame pump, `AvaloniaProperty` targets, transform/effect shorthands, codecs (colors, brushes, geometry, thickness…), declarative layer (`Animate.*`, `Presence`, `Motion`, `Connected`), gestures, shapes, controls, recipes | The package MakaMek would reference |
| `MonacoLabs.Presto.Avalonia.Lottie` | `LottieView` + `PlayLottie` recipe (SkiaSharp.Skottie) | Optional; only if we adopt Lottie assets |
| `MonacoLabs.Presto.Avalonia.DevTools` | In-app animation inspector (freeze/step/slow, live tickable list) | Debug-only aid |

Key mechanics to know before writing code:

- **Frame pump.** `PrestoAvalonia.EnsureInitialized()` (called by every entry point) installs `AvaloniaFrameSource`, which re-requests `TopLevel.RequestAnimationFrame` only while the engine has children. Animations cost nothing when nothing is running.
- **Write priority.** Values are written at `BindingPriority.Animation`, so `Revert()` uncovers the control's own style/binding/local value exactly. This is why Presto composes with MVVM instead of fighting it.
- **Transforms are bridge-owned.** `X`, `Y`, `Rotate`, `Scale` compose into a `TransformGroup` the bridge owns in `RenderTransform`. **Never also assign `RenderTransform` manually on the same visual** — MakaMek does this on `UnitControl` (facing) and on `PathSegmentControl`, so animate child visuals there, not the rotated control itself.
- **Detached visuals.** `PrestoAvalonia.WhenDetached` defaults to `Pause`; set it to `Revert` or use a `ControlScope` if an animation must be torn down when its control leaves the tree.
- **Scopes.** `PrestoAvalonia.CreateScope(control)` returns a `ControlScope` that reverts everything created inside it when the control detaches. This is the clean lifetime story for code-built board controls.

---

## 3. Capability inventory

Legend — **Value** is for MakaMek specifically; **Maturity** is my read of the feature's coverage in the repo's tests/specs/samples.

### 3.1 Timing and choreography

| Capability | API | Value for MakaMek | Maturity |
|---|---|---|---|
| Tweens with full playback control | `Animate`, `Pause/Resume/Play/Reverse/Seek/Restart/Complete`, `Progress`, `Speed`, `Then()`, `await` | Combat feedback, banners, panels; awaitable animations simplify `async void` handlers | High |
| Keyframes | `.X(k => k.To(200, 500).To(80, 300))`, `.Keyframes(k => k.Frame(…)/.At(…))` | Multi-stage hits, recoil → settle, damage number arc | High |
| Timelines | `Presto.CreateTimeline().Add(target, b => …, position).Label(…)`, `Seek`, `Sync` | Whole attack choreography: attacker flash → projectile → impact → damage → fall; scrubbable for replays | High |
| Stagger | `Presto.Stagger(ms, new() { From = Center, Grid = [7,4] })` | Unit list entrances, damage-number cascades, menu items | High |
| Composition | `Replace`, `Overlap`, `Additive` | Additive for layered shake/pulse without fighting the base motion | Medium |
| Scopes / defaults | `Scope`, `ControlScope`, `Presto.Defaults` | Per-control lifetime; app-wide default duration/ease | High |
| Reduced-motion-aware builders | `.WhenReduced(b => …)`, `Presto.Defaults.ReducedMotion`, `ReduceAll` | Accessibility without branching at every call site | High |

### 3.2 Motion quality

| Capability | API | Value | Maturity |
|---|---|---|---|
| anime.js ease set | `Eases.OutQuint`, `InOutBack(1.7)`, `OutElastic`, `Steps`, `CubicBezier`, `FromPath`, `Wiggle`, `Rough`, `SlowMo` | Better feel than the two XAML `Animation` resources currently in `Styles/Animations.axaml` | High |
| Springs | `Eases.Spring(mass, stiffness, damping, velocity)`, `Springs.Gentle/Snappy/Bouncy/Stiff` | Snappy UI, natural settle; spring duration is computed from settling time | High |
| Per-target function values | `.Y((target, i, total) => …)` | Fan-out layouts without manual loops | High |
| Relative and unit values | `"+=140"`, `"*=1.5"`, `"1turn"`, `"75%"` | Adjust motion relative to current state (e.g. nudge from wherever the unit is) | High |

### 3.3 Animatable property coverage

| Capability | Value | Notes |
|---|---|---|
| Transform shorthands (`X`, `Y`, `Rotate`, `Scale`, `ScaleX/Y`, `RotateX/Y`, `Perspective`) | Map pan, unit facing, card tilt | Bridge owns `RenderTransform` |
| `Opacity`, `Width`, `Height`, `Margin`, `Padding` | Panel reveals, collapsing HUD sections | `Width`/`Height` move in device-pixel steps with layout rounding |
| `Background`, `Foreground`, `BorderBrush` (solid ↔ gradient, any color space) | Owner-tint transitions, selection color changes, damage flashes | `k.Color(from, to, ColorSpace.Oklch)` for perceptual mixes |
| `BorderThickness`, `CornerRadius`, `BoxShadow`, `Point`, `Size`, `Rect`, `Geometry` | HUD bars, hex rings, ring morphs | Geometry morph is `PrestoAvalonia.MorphTo` |
| Effects: `Blur`, `Shadow*`, `ClipInset`, `ClipCircle`, `MaskSweep` | Reveals, focus, damage vignette, ghosting | One effect and one clip per visual |
| Pseudo-properties: `Draw`, `Data` | Self-drawing range rings, LOS lines, hex outlines | `Shape` only |
| Arbitrary `AvaloniaProperty` by name or typed reference | Anything not covered above | `.Property(Border.BorderBrushProperty, …)` |

### 3.4 Declarative XAML layer

MakaMek views are AXAML, so these are directly usable in `src/MakaMek.Avalonia/MakaMek.Avalonia/Views/**` and `MakaMek.Avalonia.Controls/**`.

| Capability | API | Value |
|---|---|---|
| Lifecycle animations | `presto:Presence.Initial/Enter/Reveal/Exit/Arrive`, `Mode="Wait"` | Screen and panel entrances/exits, list rows, ghosts on removal |
| State motion | `presto:Motion.Hover/Press/Focus/State/States/LocalStates`, `StaggerChildren`, `Order` | Hover/press polish on action buttons, unit tiles, toolbar; state driven by a VM enum (`Motion.SetState(control, status)`) |
| One-shot triggers | `presto:Animate.OnAttached/OnTapped/OnChanged/Watch` | Banner pop, notification bump, count-up on value change |
| Layout transitions (FLIP) | `presto:Animate.Layout`, `Animate.ItemLayout`, `{presto:Reflow}` | Reflowing HUD panels, unit lists, action menus — on the render thread |
| Content-size transitions | `presto:Animate.ContentSize` | Expanding/collapsing unit details without scaling text |
| Connected (shared element) | `presto:Connected.Key/Motion/Anchor` | Unit tile → unit detail flight between screens |
| Page transitions | `PrestoPageTransition` (`IPageTransition`), `{presto:PageSlide}` etc. | Only where the host accepts `IPageTransition`; Sanet.MVVM navigation would need a check |
| Markup recipes | `{presto:Fade Direction=Up, Stagger=60}`, `{presto:Slide}`, `{presto:Pop}`, `{presto:Blur}`, `{presto:Shake}`, `{presto:Lift}`, `{presto:Shimmer}` | Consistent motion vocabulary with no code-behind |

### 3.5 Interaction, gestures, scrolling

| Capability | API | Value | Maturity |
|---|---|---|---|
| Draggable | `PrestoAvalonia.CreateDraggable(control, new DraggableParams { Container, Snap, DropGroup, DropHit, OnDrop… })` | Reworking deployment/lobby lists, drag-to-reorder units, drag a unit onto a hex | High (the gallery board demo is a full kanban with drop zones) |
| Drag-to-reorder | `Reorder`, `{presto:Reorder}` | Unit roster ordering, weapon order | Medium |
| Transformable (pan/pinch-zoom/rotate) | `control.CreateTransformable(new TransformableParams { … })` | **Map camera**: replaces/complements `MapTransformCalculator` pan-zoom | Medium |
| Magnetic pull | `Magnetic` | Drop targets that pull the pointer | Low |
| Press ripple | `presto:Ripple.Enabled`, `Color`, `Bounded`, `StateLayer` | Material-style feedback on action buttons; render-thread drawn | High |
| Scroll observer / scrolling | `ScrollObserver`, `Scroll.SmoothWheel`, `ScrollToItem`, `ScrollSnap` | Long unit/weapon lists, game log auto-scroll | Medium |
| Gesture arbitration | — | Required once drag and pan coexist; documented | Medium |

### 3.6 Shapes, text, media

| Capability | API | Value |
|---|---|---|
| Stroke draw-in | `PrestoAvalonia.CreateDrawable(shape)`, `.Draw(from: "0 0", to: "0 1")` | Hex selection ring, range/LOS lines, movement path reveal |
| Motion path (shape or points) | `CreateMotionPath(shape)` / `(points, curviness, autoRotate)` → `X`/`Y`/`Rotate` tweens; `MotionPathGeometry` | Projectile flight along the board, LRM arc, drift |
| Geometry morph | `.Data(PrestoAvalonia.MorphTo(geometry))` | Range-ring shape changes, icon morphs, faction emblems |
| Text splitting / reveals | `TextSplitter`, `TextReveal`, `{presto:CharCascade}`, `LineRise/LineMask/WordFade` | Turn banners, phase announcements |
| Text on a path, glyph outlines, warps | `TextPath`, `GlyphShards`, `TextWarp` | Title/logo effects (low priority) |
| Count-up / odometer | `Recipes.CountUp`, `Recipes.RollDigits` | Damage totals, BV, initiative, heat readouts |
| Marquee / split-flap | `Marquee`, `SplitFlap` | Game log ticker, initiative board |
| Image filters | `FilteredImage` (`Grayscale`, `Sepia`, `Brightness`, `HueRotate`, `Tint`, `Duotone`…) | Destroyed/shutdown unit portraits, faction tinting, heat shimmer |
| Pixel transitions | `Dissolve`, `Pixelate`, `Ripple`, `SourceTransition` | Damage/destruction transitions on unit art |
| Sprite sheets / sequences | `SpriteSheet`, `ImageSequence`, `PlayFrames`, `sheet.Play(fps)` | Explosion, jump-jet, muzzle-flash animations from sprite strips |
| Raster distortion | `Distort` | Heat-haze over the board (expensive; opt-in) |
| Lottie | `LottieView`, `PlayLottie`, `PlaySegment`, render-thread playback | Optional asset pipeline |
| Particles | `Particles`, `Recipes.Confetti/Sparkles/Burst`, `ParticleScene`, `ParticleFormation` | Destruction bursts, sparks, smoke, crit-hit emphasis — drawn on the render thread in an overlay, no layout cost |

### 3.7 Render thread, layout and performance

| Capability | API | Value |
|---|---|---|
| Compositor animations for existing controls | `AnimateOnCompositor`, `AnimationThread.PreferCompositor`, `CanAnimateOnCompositor` | Keeps a spinner/pulse moving while the UI thread is busy (AI turns, network) |
| Compositor scenes | `CompositorScene` + `CompositorCanvas` | Custom render-thread drawing (ambient board effects) with its own engine |
| Layout transitions | `AnimateLayout`, `AnimateSharedElement`, `Animate.ItemLayout` | FLIP moves survive a blocked UI thread |
| Content-size animation | `AnimateContentSize` | True-size panel growth, text never rewraps |
| Detach policy | `WhenDetached.Continue/Pause/Revert` | Prevents work for off-screen board elements |
| Diagnostics | `EngineDiagnostics`, `PrestoDiagnostics`, DevTools overlay | Finding runaway animations; Debug-only |

### 3.8 Ready-made controls

`Pager` + `PagerIndicator`, `Sheet`/`Drawer`, `ToastHost`, `Modal`, `Accordion`/`Pane`, `Selection` indicator, `Swipe` actions, `PullToRefresh`, `Flip` card, `SplitFlap`, `ProgressRing`, `SubmitButton`, `Marquee`.

MakaMek-specific candidates: `ToastHost` for transient game messages, `Sheet` for mobile unit details, `Selection` for list/tab indicators, `ProgressRing`/`SubmitButton` for connect/lobby flows, `Accordion` for record-sheet sections.

---

## 4. Ranked first uses for MakaMek

| # | Use | Effort | Payoff | Notes |
|---|---|---|---|---|
| 1 | **Unit selection ring** (pilot, §5) | S | High — most-seen UI element | Code-only, no XAML restructure |
| 2 | **Damage/event labels** in `UnitControl.ProcessEvents` | S | High | Replace the 5 s XAML fade with a rise + fade + scale, staggered when several land at once |
| 3 | **Action menu / HUD panels** (`Presence` + `Motion`) | M | High | Hover/press states, entrance/exit ghosts, `AnimateContentSize` for expanding sections |
| 4 | **Destruction & hit feedback** (`Recipes.Shake`, `Burst`, `SpriteSheet`, `FilteredImage.Tint`) | M | High | Immediate game feel; keep particle counts low on mobile |
| 5 | **Movement path reveal** (`CreateDrawable` on `PathSegmentControl`) | M | Medium | Draw the path segment-by-segment, then a travelling dot |
| 6 | **Turn/phase banner** (`TextReveal`, `CountUp`, `Presence`) | S | Medium | `TurnNotificationBanner` already animates opacity in XAML |
| 7 | **Map camera** (`Transformable` or `Animatable` + wheel) | L | High | Must coexist with the existing click→hex hit-testing and `MapTransformCalculator`; investigate before committing |
| 8 | **Attack choreography** (timeline: flash → projectile → impact → damage) | L | High | Needs server event timing; Presto timelines are scrubbable |
| 9 | **Unit roster / lobby** (`Reorder`, `Stagger`, `Selection`) | M | Medium | Drag-to-reorder + animated selection indicator |
| 10 | **Shared-element navigation** (unit tile → detail) | M | Medium | Requires hosting views in a `ContentControl`; check Sanet.MVVM navigation first |

---

## 5. Pilot design — animated selection ring in `UnitControl`

### 5.1 Goals

- Make the selected unit unmistakable at a glance on a crowded board.
- Distinguish the three roles the current border conflates: **selected**, **attacker**, **target**.
- Feel responsive (spring snap), alive (subtle sonar echo), and never distracting (slow, low-contrast idle motion).
- Cost nothing when no unit is selected; degrade to a simple fade under reduced motion.
- Stay inside the existing architecture: no changes to Core/Presentation behaviour, UI logic kept thin.

### 5.2 Current state

`UnitControl` builds a `Border` (`selectionBorder`) with `CornerRadius = Width / 2` and toggles `IsVisible` from a 60 fps poll:

```csharp
private void UpdateSelectionBorder(UnitState state, Border selectionBorder)
{
    selectionBorder.IsVisible = state.SelectedUnit == _unit
        || _viewModel.CurrentState is WeaponsAttackState attackState &&
        (attackState.Attacker == _unit || attackState.SelectedTarget == _unit);
}
```

Problems: an ellipse, not a hexagon; binary on/off; one visual for three different meanings; `IsVisible` changes drive layout; and it is re-evaluated 30×/s.

### 5.3 Proposed visuals

Three stacked visuals inside the `UnitControl` `Grid`, all `IsHitTestVisible = false`:

| Visual | Type | Base state | Purpose |
|---|---|---|---|
| `_selectionRing` | `Path` (hexagon, stroke only) | `Opacity = 0` | The ring itself; springs in, self-draws its stroke |
| `_selectionEcho` | `Path` (same geometry, thinner) | `Opacity = 0` | One expanding "sonar" pulse per selection change |
| `_selectionPulse` | `Path` (same geometry, 2 px) | `Opacity = 0` | Slow breathing loop while selected |

Ring geometry comes from the existing `HexagonGeometry.GetCorners()` (already framework-neutral and used by the map), inset a few pixels:

```csharp
private static Geometry CreateHexRingGeometry(double inset)
{
    var corners = HexagonGeometry.GetCorners();
    var cx = HexCoordinatesPixelExtensions.HexWidth / 2;
    var cy = HexCoordinatesPixelExtensions.HexHeight / 2;
    var scale = 1 - inset / (HexCoordinatesPixelExtensions.HexHeight / 2);

    var geometry = new StreamGeometry();
    using var ctx = geometry.Open();
    ctx.BeginFigure(ToPoint(corners[0]), isFilled: false);
    for (var i = 1; i < corners.Length; i++) ctx.LineTo(ToPoint(corners[i]));
    ctx.EndFigure(true);
    return geometry;

    Point ToPoint((double X, double Y) p) =>
        new(cx + (p.X - cx) * scale, cy + (p.Y - cy) * scale);
}
```

### 5.4 Motion design

| Moment | Motion | Builder |
|---|---|---|
| Selected | Spring in: `Opacity 0→1`, `Scale 0.82→1`, stroke draws `0 0 → 0 1` | `Eases.Spring(stiffness: 180, damping: 18)` |
| Selected (idle) | `_selectionPulse`: `Scale 1→1.06`, `Opacity .35→0`, 1800 ms loop | `Eases.InOutSine` |
| Attacker | Owner tint, steady ring, no pulse | — |
| Target | Hostile tint (`DestroyedBrush`/red), faster pulse (1100 ms), slightly thicker stroke | — |
| Deselected | `Opacity → 0`, `Scale → 1.12`, 160 ms | `Eases.OutQuad` |
| Reduced motion | 120 ms fade only, no scale, no echo, no loop | `WhenReduced` |

### 5.5 Code shape

New/changed members in `src/MakaMek.Avalonia/MakaMek.Avalonia/Controls/UnitControl.cs`:

```csharp
using MonacoLabs.Presto;   // Presto.Build, Eases, Animation, AnimationBuilder, PrestoAvalonia, ControlScope

private const double SelectionRingInset = 6;

private readonly Path _selectionRing;
private readonly Path _selectionEcho;
private readonly Path _selectionPulse;
private ControlScope? _selectionScope;
private Animation? _ringAnimation;
private Animation? _echoAnimation;
private Animation? _pulseAnimation;
private UnitSelectionKind _selectionKind = UnitSelectionKind.None;

// Reusable recipes: a builder is a recipe; Animate() takes a fresh snapshot each time.
// Note the reduced variant repeats Draw: WhenReduced builds a new builder with only its
// own properties, so anything the reduced state needs must be restated there.
private static readonly AnimationBuilder RingIn = Presto.Build()
    .Opacity(from: 0, to: 1)
    .Scale(from: 0.82, to: 1)
    .Draw(from: "0 0", to: "0 1")
    .Ease(Eases.Spring(stiffness: 180, damping: 18))
    .WhenReduced(r => r.Opacity(to: 1).Draw("0 1").Duration(120).Ease(Eases.None));

private static readonly AnimationBuilder RingOut = Presto.Build()
    .Opacity(to: 0)
    .Scale(to: 1.12)
    .Duration(160)
    .Ease(Eases.OutQuad)
    .WhenReduced(r => r.Opacity(to: 0).Duration(80).Ease(Eases.None));

private static readonly AnimationBuilder Echo = Presto.Build()
    .Scale(from: 1, to: 1.22)
    .Opacity(from: 0.55, to: 0)
    .Duration(650)
    .Ease(Eases.OutQuad)
    .WhenReduced(r => r.Opacity(to: 0).Duration(1));

private static readonly AnimationBuilder IdlePulse = Presto.Build()
    .Scale(from: 1, to: 1.06)
    .Opacity(from: 0.35, to: 0)
    .Duration(1800)
    .Ease(Eases.InOutSine)
    .Loop()
    .WhenReduced(r => r.Loop(false).Opacity(to: 0).Duration(1));
```

Construction (once, in the ctor, before `Render()`):

```csharp
_selectionScope = PrestoAvalonia.CreateScope(this);   // reverts on detach

_selectionRing  = CreateRing(inset: SelectionRingInset, thickness: 4);
_selectionEcho  = CreateRing(inset: SelectionRingInset, thickness: 2);
_selectionPulse = CreateRing(inset: SelectionRingInset + 2, thickness: 2);

Children.Insert(0, _selectionRing);      // inserted first …
Children.Insert(0, _selectionEcho);      // … then echo over it …
Children.Insert(0, _selectionPulse);     // … then pulse on top; all three stay behind the unit image
```

`CreateRing` sets `Data = CreateHexRingGeometry(inset)`, `Stroke = owner brush`, `StrokeThickness`, `StrokeLineJoin = PenLineJoin.Round`, `Fill = null`, `Opacity = 0`, `IsHitTestVisible = false`, `Stretch = Stretch.None`, top-left alignment (the geometry is already in hex-local pixels), `RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative)` so `Scale` pulses around the hex centre, and calls `PrestoAvalonia.CreateDrawable(path)` once so the shape starts in the draw state at `"0 0"` (hidden).

Selection changes replace `UpdateSelectionBorder` with edge-detected state:

```csharp
private void UpdateSelectionVisuals()
{
    var kind = ResolveSelectionKind();
    if (kind == _selectionKind) return;      // called ~30×/s from the poll
    _selectionKind = kind;

    StopSelectionAnimations();

    if (kind == UnitSelectionKind.None)
    {
        _ringAnimation = _selectionRing.Animate(RingOut);   // fade from wherever it is
        return;
    }

    var brush = RingBrush(kind);
    _selectionRing.Stroke = brush;
    _selectionEcho.Stroke = brush;
    _selectionPulse.Stroke = brush;
    _selectionRing.StrokeThickness = kind == UnitSelectionKind.Target ? 5 : 4;

    _selectionScope!.Execute(_ =>
    {
        _ringAnimation = _selectionRing.Animate(RingIn);
        _echoAnimation = _selectionEcho.Animate(Echo);
        if (kind != UnitSelectionKind.Attacker)
        {
            _pulseAnimation = _selectionPulse.Animate(kind == UnitSelectionKind.Target
                ? IdlePulse.With(b => b.Duration(1100))
                : IdlePulse);
        }
    });
}

private void StopSelectionAnimations()
{
    // Cancel() leaves values where they are; Revert() restores the local base (Opacity 0).
    _ringAnimation?.Cancel();
    _echoAnimation?.Revert();
    _pulseAnimation?.Revert();
    _ringAnimation = _echoAnimation = _pulseAnimation = null;
}

private UnitSelectionKind ResolveSelectionKind()
{
    if (_unit.IsOutOfCommission) return UnitSelectionKind.None;
    if (_viewModel.CurrentState is WeaponsAttackState attack)
    {
        if (attack.Attacker == _unit) return UnitSelectionKind.Attacker;
        if (attack.SelectedTarget == _unit) return UnitSelectionKind.Target;
    }
    return _viewModel.SelectedUnit == _unit ? UnitSelectionKind.Selected : UnitSelectionKind.None;
}
```

Two API facts this code depends on, both easy to get wrong:

- `Cancel()` / `Revert()` are members of `Animation`, **not** of the control. Keep the returned `Animation` handles (as above); `_selectionRing.Cancel()` does not compile.
- `WhenReduced` discards the builder's properties, so the reduced `RingIn` restates `Draw("0 1")` and the reduced `RingOut` restates `Opacity(to: 0)`. A reduced builder with timing only animates nothing — without the repeated `Draw` the stroke stays at `"0 0"`, and without the repeated `Opacity` the ring never disappears.

Rapid toggling is the one rough edge: `RingIn` starts from `Opacity 0` / `Scale 0.82`, so a deselect immediately followed by a reselect snaps to the entrance pose. If that shows up in practice, either animate `to:`-only and let `Echo` carry the pop, or keep the handles and reverse the outgoing animation instead of restarting it.

`Dispose` gains `StopSelectionAnimations()` and `_selectionScope?.Revert();` alongside `_subscription.Dispose()` — the handles cover the unscoped `RingOut`, the scope covers everything created inside `Execute` and any animation still running if the control is detached without `Dispose`.

The pure part — "which role does this unit play?" — moves to Presentation so it can be unit-tested (UI is excluded from coverage):

```csharp
// src/MakaMek.Presentation/UiStates/UnitSelectionKind.cs
public enum UnitSelectionKind { None, Selected, Attacker, Target }

// src/MakaMek.Presentation/UiStates/UnitSelectionKindResolver.cs
public static class UnitSelectionKindResolver
{
    public static UnitSelectionKind Resolve(IUnit unit, IUnit? selected, IUiState state) => …
}
```

`UnitControl.ResolveSelectionKind()` becomes a one-line call. Tests cover: no selection, selected, attacker wins over selected, target, destroyed unit, non-combat phase.

### 5.6 Why not animate the `UnitControl` itself

`UnitControl` assigns `RenderTransform = new RotateTransform(torsoFacing * 60, 0, 0)` every poll tick to show facing. Presto's `Scale`/`Rotate` shorthands own the same `RenderTransform`, so animating the control would be overwritten on the next tick (or would overwrite the facing). Animate the ring children instead. This is a general rule for this codebase: **the control that carries facing/rotation is off-limits to transform shorthands; animate its children.**

### 5.7 Lifetime and churn

`BattleMapView.RenderMap` rebuilds every `UnitControl` on each map render and never disposes the previous list. With looping animations that becomes a leak (paused, but retained). The pilot should:

1. `Dispose()` the old `_unitControls` before replacing them in `RenderMap`.
2. Rely on `ControlScope` + `Dispose` to cancel the loop.
3. Optionally set `PrestoAvalonia.WhenDetached = WhenDetached.Revert` app-wide as a safety net.

### 5.8 Phased delivery

| Phase | Scope | Risk |
|---|---|---|
| 1 | Hex ring + spring in/out + role colors, opacity/scale only | Low; validates package wiring and lifetime |
| 2 | Self-drawing stroke (`Draw`), sonar echo, idle pulse | Medium; `Draw` leaves dash state — verify `Revert()` restores it |
| 3 | Reduced-motion tuning, DevTools check for stray tickables | Low |
| 4 | Optional: reuse the same ring for movement destination / weapon target preview | Medium |

### 5.9 Verification checklist

- Select/deselect quickly 10×: no stuck ring, no double ring, no flicker.
- Select a unit, then trigger a full map rebuild: old ring stops (no tickable leak in DevTools).
- Weapons phase: attacker ring steady, target ring pulses faster.
- Destroyed unit: no ring.
- Reduced motion on: only a 120 ms fade.
- 60 fps unchanged with 12 units on screen (DevTools frame stats).
- Browser head: verify animations run (render thread is the UI thread there).
- Android head: verify no jank when a ring + particles are on screen.

---

## 6. Risks and constraints

| Risk | Detail | Mitigation |
|---|---|---|
| **Preview quality** | `0.1.0-preview.1`; author calls it experimental; API not frozen | Pin the exact version; keep usage behind a thin helper layer so an upgrade is localized |
| **Platform coverage** | Windows fully exercised; Browser/Android partly; iOS compiles, not run | Pilot on Desktop + Browser + Android before broad adoption; keep a reduced-motion/fallback path |
| **Transform ownership** | Bridge owns `RenderTransform` for `X/Y/Rotate/Scale` | Never combine with manual `RenderTransform` on the same visual (UnitControl, PathSegmentControl) |
| **One effect/clip per visual** | Blur + shadow, or `ClipInset` + `ClipCircle`, on one visual throws | Put effects on inner/outer wrappers |
| **UI thread by default** | Only `X/Y/Rotate/Scale/Opacity` fit the compositor | Use `AnimationThread.PreferCompositor` for spinners/pulses; expect fallback |
| **Engine cost** | Every running animation ticks; a 60 fps poll per unit already exists | Edge-detect state changes; animate only what changed; scope/cancel on detach |
| **Rebuild churn** | `RenderMap` recreates controls | Dispose old controls; `ControlScope`; `WhenDetached.Revert` |
| **Testing** | UI is excluded from coverage | Extract pure logic (selection kind, motion token selection) to `MakaMek.Presentation` and test there |
| **License** | MIT — compatible with GPL-3.0-only | Keep the copyright notice in the dependency list |
| **Docs drift** | Presto's `GUIDE.md`/specs are the source of truth and change | Consult the guide/spec before coding; see the `presto-animation` skill |

---

## 7. Decision summary

- Adopt `MonacoLabs.Presto.Avalonia` as the animation layer for the Avalonia client, pinned to `0.1.0-preview.1`, added to `MakaMek.Avalonia.csproj` (and `MakaMek.Avalonia.Controls` if controls there animate).
- Start with the selection ring pilot (§5), then damage labels, then HUD/menu motion.
- Do **not** move game timing or rules-driven sequencing into Presto; keep it a presentation concern.
- Keep a thin `Motion/` helper folder in the Avalonia project (shared builders, tokens, reduced-motion wiring) so the preview dependency has one seam to swap.
