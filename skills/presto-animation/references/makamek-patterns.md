# MakaMek motion patterns

Concrete patterns for the MakaMek Avalonia client. Code is illustrative of the target shape — adapt names to the file you are editing and verify API details against the Presto guide.

## 0. One motion module

Put shared builders and tokens in one place so the preview dependency has a single seam and motion stays consistent:

`src/MakaMek.Avalonia/MakaMek.Avalonia/Motion/MakaMekMotion.cs`

```csharp
using MonacoLabs.Presto;

namespace Sanet.MakaMek.Avalonia.Motion;

internal static class MakaMekMotion
{
    // Durations: fast feedback, standard transition, dramatic beat.
    public const double Fast = 140;
    public const double Normal = 260;
    public const double Slow = 480;

    public static readonly EaseValue EnterEase = Eases.OutQuart;
    public static readonly EaseValue ExitEase = Eases.InQuad;

    public static AnimationBuilder PanelIn { get; } = Presto.Build()
        .Opacity(from: 0, to: 1)
        .Y(from: 12)
        .Duration(Normal)
        .Ease(EnterEase)
        .WhenReduced(r => r.Opacity(from: 0, to: 1).Duration(Fast));

    public static AnimationBuilder PanelOut { get; } = Presto.Build()
        .Opacity(to: 0)
        .Y(to: 8)
        .Duration(Fast)
        .Ease(ExitEase)
        .WhenReduced(r => r.Duration(1));
}
```

Why a module: every call site then uses the same durations/eases, reduced-motion behaviour is defined once, and a future Presto upgrade or replacement touches one file.

## 1. Animated selection outline (`SelectionHex`)

Shipped in #1624. See the full design in `docs/analysis/presto-animation-capabilities.md` §5.

The motion vocabulary lives in one module, `src/MakaMek.Avalonia/MakaMek.Avalonia/Controls/SelectionHexMotion.cs`
(internal, with `InternalsVisibleTo` for `MakaMek.Avalonia.Tests`), so the builders and the geometry can be
tested without a board. The visual itself is a control of its own,
`src/MakaMek.Avalonia/MakaMek.Avalonia/Controls/SelectionHex.cs`, so `UnitControl` only tells it which role the
unit plays — a board control that keeps growing a method per animation becomes unreadable.

```csharp
// UnitControl
private readonly SelectionHex _selectionHex;

// ctor
_selectionHex = new SelectionHex(ownerBrush, targetBrush);
Children.Add(_selectionHex);   // behind the unit art

// poll
_selectionHex.Update(state.SelectionKind);

// Dispose
_selectionHex.Dispose();
```

`SelectionHex` owns the three paths, the scope and the animations:

```csharp
private readonly Path _hex;
private readonly Path _echo;
private readonly Path _pulse;
private readonly ControlScope _scope;
private Animation? _hexAnimation;
private Animation? _echoAnimation;
private Animation? _pulseAnimation;
private UnitSelectionKind _kind = UnitSelectionKind.None;

// ctor
_scope = PrestoAvalonia.CreateScope(this);
_hex = SelectionHexMotion.CreateHex(SelectionHexMotion.Inset, 4, ownerBrush);
_echo = SelectionHexMotion.CreateHex(SelectionHexMotion.Inset, 2, ownerBrush);
_pulse = SelectionHexMotion.CreateHex(SelectionHexMotion.Inset, 2, ownerBrush);
PrestoAvalonia.CreateDrawable(_hex);   // start in the draw state at "0 0"
```

Rules that matter:

- **Edge detection.** `Update(kind)` is called from the 30 Hz poll. Compare with `_kind` and return when
  unchanged. The role is part of the polled `UnitState`, so a role change always wakes the poll up.
- **A role change is not an entrance.** Only the transition from `None` plays the entrance; switching between
  selected / attacker / target re-colours the outline and swaps the pulse, it does not spring it in again.
- **Animate children, not the control.** `UnitControl.RenderTransform` is reserved for facing.
- **Base `Opacity = 0`.** Fade in/out instead of toggling `IsVisible`, so layout never changes.
- **Hexagon geometry.** `HexagonGeometry.GetCorners()` + `HexCoordinatesPixelExtensions` keep the outline aligned
  with the map.
- **Scope + Dispose.** `_scope.Revert()` in `Dispose`, and dispose old `UnitControl`s in `BattleMapView.RenderMap`
  before rebuilding.

```csharp
public static readonly AnimationBuilder Enter = Presto.Build()
    .Opacity(from: 0, to: 1)
    .Scale(from: 0.82, to: 1)
    .Draw(from: "0 0", to: "0 1")
    .Ease(Eases.Spring(stiffness: 180, damping: 18))
    // WhenReduced builds a NEW builder: restate Draw or the stroke stays at "0 0" and is invisible.
    .WhenReduced(r => r.Opacity(from: 0, to: 1).Draw("0 1").Duration(120).Ease(Eases.None));

public static readonly AnimationBuilder Exit = Presto.Build()
    .Opacity(to: 0)
    .Scale(to: 1.12)
    .Duration(160)
    .Ease(Eases.OutQuad)
    .WhenReduced(r => r.Opacity(to: 0).Duration(80).Ease(Eases.None));
```

Keep the returned `Animation` handles — `Cancel()`/`Revert()` are on the animation, not on the `Path`. `Cancel()`
leaves the values where they are (the outline fades out from where it is), `Revert()` restores the resting state
(the echo and the pulse).

**A pulse that grows out of an outline has to leave it.** The outline strokes a 4 px line on the hexagon's path,
so a 2 px pulse on the same path starts *under* that line and only shows as a slightly brighter outline until it
scales past it. Give the pulse a real travel (`Scale 1 → 1.22` on a 100 px hex is about 10 px) and enough opacity
to survive the fade, and give the path the size of the whole hex (`Width`/`Height`) so a relative
`RenderTransformOrigin` of one half is the centre of the hexagon rather than the centre of its bounding box.

```csharp
public static readonly AnimationBuilder IdlePulse = Presto.Build()
    .Scale(from: 1, to: 1.22)
    .Opacity(from: 0.6, to: 0)
    .Duration(1800)
    .Ease(Eases.OutQuad)
    .Loop()
    .WhenReduced(r => r.Loop(false).Opacity(to: 0).Duration(1).Ease(Eases.None));
```

The pure role decision belongs in Presentation, not in the control:

```csharp
public enum UnitSelectionKind { None, Selected, Attacker, Target }

// src/MakaMek.Presentation/UiStates/UnitSelectionKindResolver.cs
public static UnitSelectionKind Resolve(IUnit unit, IUnit? selected, IUnit? attacker, IUnit? target)
{
    if (unit.IsOutOfCommission) return UnitSelectionKind.None;
    if (ReferenceEquals(attacker, unit)) return UnitSelectionKind.Attacker;
    if (ReferenceEquals(target, unit)) return UnitSelectionKind.Target;
    return ReferenceEquals(selected, unit) ? UnitSelectionKind.Selected : UnitSelectionKind.None;
}
```

The combat roles reach the resolver through `ICombatSelectionRoles`, which `WeaponsAttackState` implements, so the
rule is testable without a game:

```csharp
public interface ICombatSelectionRoles
{
    IUnit? Attacker { get; }
    IUnit? SelectedTarget { get; }
}
```

**Headless caveat.** The headless platform reports `Geometry.ContourLength` as `0`, so the self-drawing stroke
cannot be exercised in a headless test — the dash is only written on a platform that measures the contour
(Skia: Desktop, Browser, Android, iOS). Test the geometry, the entrance, the exit, the pulse and the reduced
forms; check the draw-in on a real platform.

## 2. Damage / event labels

Current code awaits a 5-second XAML fade, one label per event, stacked vertically. A Presto version reads better and clears faster:

```csharp
private void CreateDamageLabel(string text, IBrush background)
{
    var label = new TextBlock
    {
        Text = text,
        Foreground = Brushes.White,
        FontWeight = FontWeight.Bold,
        Background = background,
        HorizontalAlignment = HorizontalAlignment.Center,
    };
    _eventsPanel.Children.Add(label);

    label.Animate(b => b
        .Opacity(from: 0, to: 1)
        .Y(from: 6, to: 0)
        .Scale(from: 0.85, to: 1)
        .Duration(220)
        .Ease(Eases.OutBack(1.6))
        .WhenReduced(r => r.Opacity(from: 0, to: 1).Duration(120)))
        .Then(_ => label.Animate(b => b
            .Opacity(to: 0)
            .Y(to: -18)
            .Duration(900)
            .Delay(900)
            .Ease(Eases.InQuad))
            .Then(_ => _eventsPanel.Children.Remove(label)));
}
```

Notes:

- Keep the label in the panel while it fades; remove only after the animation completes.
- If several events arrive at once, stagger the labels: `Presto.Animate(labels, b => b.Y(-18).Delay(Presto.Stagger(70)))`.
- Do not block the poll loop awaiting the animation; `Then` is the continuation.

## 3. HUD panels and menus (XAML)

For AXAML views, prefer the declarative layer over code-behind:

```xml
<Border Classes="unit-details"
        presto:Presence.Enter="{presto:Fade Direction=Up, Distance=12}"
        presto:Presence.Exit="{presto:Fade Direction=Down}"
        presto:Animate.ContentSize="{presto:Reflow}">
    <StackPanel>
        <Button Content="{Binding FireLabel}"
                presto:Motion.Hover="{presto:Lift}"
                presto:Motion.Press="{presto:PressIn}" />
    </StackPanel>
</Border>
```

- `Presence.Enter/Exit` for appearance and removal (exits play on a ghost, so removed content leaves gracefully).
- `Motion.Hover/Press/Focus` for button feel; states return to rest automatically.
- `Animate.ContentSize` when the panel grows/shrinks with content (no text scaling, no rewrap).
- `Animate.Layout` on containers whose position changes because siblings appear/disappear.

## 4. Health, heat and other numeric readouts

`ProgressBar.Value` is an `AvaloniaProperty`. For a value that changes rarely, animate it directly:

```csharp
_armorBar.Animate(b => b
    .Property(RangeBase.ValueProperty, currentArmor)
    .Duration(320)
    .Ease(Eases.OutCubic)
    .WhenReduced(r => r.Duration(1)));
```

But `UnitControl` calls `UpdateHealthBars` from the 30 Hz poll. Starting a new `Animate` per tick restarts the tween every tick and the bar never moves. For poll-driven values use a continuously retargeted value instead — `Set` retargets an existing animation rather than restarting it:

```csharp
// ctor: one animatable per bar
_armorValue = _armorBar.CreateAnimatable(new AvaloniaAnimatableParams
{
    [RangeBase.ValueProperty] = new(),
    Ease = Springs.Snappy
});

// UpdateHealthBars (every tick is fine — Set retargets, it does not restart)
_armorBar.Maximum = maxArmor;
_armorValue.Set("Value", currentArmor);
```

Same rule for anything else that changes every frame (camera zoom, crosshair): `CreateAnimatable` or `MotionValue`, never a fresh `Animate` per change.

```csharp
_zoom = control.CreateAnimatable(new() { ["Scale"] = new(), Ease = new Spring(stiffness: 120, damping: 18) });
_zoom.Set("Scale", targetScale);
```

## 5. Movement path reveal (`PathSegmentControl`)

`PathSegmentControl` draws segments with a `Path`. Presto can reveal the route in travel order:

```csharp
foreach (var segment in segments)
{
    var shape = segment.Shape;                       // Avalonia Shape
    PrestoAvalonia.CreateDrawable(shape)
        .Animate(b => b.Draw(from: "0 0", to: "0 1")
            .Duration(180)
            .Ease(Eases.OutQuad)
            .Delay(index * 60));
}
```

The path controls are rebuilt with the map, so give them the same scope/Dispose treatment as `UnitControl`.

## 6. Destruction and hit feedback

```csharp
// One-shot burst anchored on the unit control.
Recipes.Burst(unitControl, count: 18, color: Colors.OrangeRed);

// Screen-space emphasis on the hit unit.
unitControl.Animate(Recipes.Shake(intensity: 1.2));
```

- Particles render in a window overlay on the render thread and ignore hit testing — no layout cost, but they still cost fill rate. Keep `count` low on mobile/Browser and skip them under reduced motion (`Recipes` handles the reduced fallback).
- For unit art, `FilteredImage` + `Tint`/`Grayscale` gives a cheap "destroyed" look without new assets.

## 7. Map camera (investigate before implementing)

`Transformable` (`control.CreateTransformable(...)`) or `Animatable` + wheel events can drive pan/zoom, but the map currently relies on `MapTransformCalculator` and pixel→hex conversion (`HexCoordinatesPixelExtensions.FromPixel`) for click handling. If you animate a render transform on the map host, hit-testing must use the same transform, or clicks will land on the wrong hex.

Before writing code: read `docs/analysis/canvas-optimisations-analysis.md`, trace `BattleMapView.OnMapContentClicked`, and decide whether Presto drives the existing transform values or replaces them. Prefer animating the values `MapTransformCalculator` already owns.

## 8. Lifetime checklist for board visuals

`BattleMapView.RenderMap` clears `MapCanvas.Children` and rebuilds `UnitControl`s. Anything that loops must die with the control:

```csharp
// BattleMapView.RenderMap
if (_unitControls is { } old)
{
    foreach (var control in old) control.Dispose();
}
_unitControls = ViewModel?.Units
    .Select(u => new UnitControl(u, (IImageService<Bitmap>)ViewModel.ImageService, ViewModel))
    .ToList();
```

And in `UnitControl.Dispose`:

```csharp
public void Dispose()
{
    HideMovementPath();
    _subscription.Dispose();
    _selectionScope?.Revert();
}
```

`PrestoAvalonia.WhenDetached = WhenDetached.Revert` is a reasonable app-wide safety net once the pilot is stable; it reverts animations whose controls leave the visual tree.

## 9. Testing

UI code is excluded from coverage, so do not try to unit-test animations. Test the decisions instead:

- Extract role/variant selection (`UnitSelectionKindResolver`) into `MakaMek.Presentation` and cover it with xUnit + Shouldly.
- Keep animation builders free of branching where possible; if a builder choice depends on state, expose the choice as a pure function and test that.
- Verify visual behaviour manually on Desktop + Browser; use the Presto DevTools overlay (Debug) to confirm no stray tickables remain after deselect/rebuild.

## 10. Accessibility

- Wire one setting: `Presto.Defaults.ReducedMotion = settings.ReduceMotion` (or bind `presto:Motion.Preference` on the window).
- Give every looping builder a reduced form that stops the loop, not just a shorter duration.
- Ensure information is never conveyed by motion alone — the selection ring must still be visible (static) under reduced motion.
