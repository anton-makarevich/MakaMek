# Presto API cheat sheet

Condensed from `GUIDE.md` and the feature specs of MonacoLabs.Presto `0.1.0-preview.1`. Verify anything you rely on against upstream before shipping — the API is not frozen.

Upstream: <https://github.com/stevemonaco/MonacoLabs.Presto>
Manual: <https://github.com/stevemonaco/MonacoLabs.Presto/blob/main/GUIDE.md>
Specs: <https://github.com/stevemonaco/MonacoLabs.Presto/tree/main/docs/specs>

## Setup

```xml
<PackageReference Include="MonacoLabs.Presto.Avalonia" Version="0.1.0-preview.1" />
```

```csharp
// Optional: everything installs the bridge on first use.
public static AppBuilder BuildAvaloniaApp() =>
    AppBuilder.Configure<App>().UsePlatformDetect().UsePresto();
```

```csharp
using MonacoLabs.Presto;                    // Presto, Eases, Animation, AnimationBuilder, Recipes
using MonacoLabs.Presto.Compositor;         // AnimationThread, AnimateOnCompositor, CompositorScene
using static MonacoLabs.Presto.PrestoKeys;  // typed property keys (X, Rotate, …)
```

XAML namespace:

```xml
xmlns:presto="https://github.com/stevemonaco/MonacoLabs.Presto"
```

## Targets

- Any `AvaloniaObject`: `control.Animate(...)`, `Presto.Animate(control, ...)`.
- Many targets: pass an `IEnumerable` (`Presto.Animate(panel.Children, b => ...)`).
- Plain objects (view models): properties by name; call `Presto.RegisterTarget<T>()` once for trimming/AOT.
- Typed property: `.Property(Border.BorderBrushProperty, value)`.
- By name, including attached: `.Property("Canvas.Left", 100)`.

## Builder

```csharp
var a = control.Animate(b => b
    .X(320)                                   // to-only
    .Y(from: -40, to: 0)                      // from → to
    .Rotate("1turn")                          // deg / rad / turn
    .Scale(1.4)
    .Opacity(from: 0, to: 1)
    .Background("#ff6b6b")                    // any CSS color string
    .BorderThickness(new Thickness(10))
    .Duration(600)
    .Delay(120)
    .Ease(Eases.OutQuint)
    .Loop()
    .Alternate());
```

Terminals: `.Animate(target)`, `.Set(target)` (instant), `.ToParams()`.

`Scale` and `Rotate` act about `RenderTransformOrigin`, which Avalonia defaults to `0,0` (top-left). Set `RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative)` on anything that should pulse or spin around its centre.

Copying: `.Clone()` (bare copy), `.With(b => b.Delay(200))` (varies a copy). `Animate`/`Set` snapshot the builder, so a `static readonly` builder is safe to share.

Timing/playback: `.Duration`, `.Delay`, `.Ease`, `.Loop()`, `.Loop(LoopCount)`, `.LoopDelay`, `.Alternate()`, `.Reversed()`, `.Autoplay()`, `.FrameRate(fps)`, `.PlaybackRate`, `.PlaybackEase`, `.Composition(Composition.Additive)`, `.Priority`, `.Id`, `.WhenReduced(b => …)`.

Callbacks: `.OnBegin`, `.OnBeforeUpdate`, `.OnUpdate`, `.OnRender`, `.OnLoop`, `.OnPause`, `.OnComplete`.

Values: numbers, `"+=140"`, `"*=1.5"`, `"75%"`, `"1turn"`, colors, brushes, gradients, `Thickness`, `CornerRadius`, `Point`, `Size`, `Rect`, `Geometry`, `BoxShadows`, `(target, i, total) => …`.

## Animation control

```csharp
Animation a = control.Animate(...);
a.Pause(); a.Resume(); a.Play(); a.Reverse(); a.Restart(); a.Seek(250);
a.Progress = 0.5; a.Speed = 2; a.Cancel(); a.Revert(); a.Complete();
await a;                        // or a.Then(_ => …)
```

`Cancel()` stops and leaves the current values. `Revert()` stops and restores the values underneath (style/binding/local). `Revert` is what you want when the motion is over and the control should return to its resting state.

## Eases

```csharp
Eases.None; Eases.OutQuad; Eases.InOutQuint; Eases.OutInBounce;   // properties
Eases.Out(3); Eases.InOutBack(1.7); Eases.OutElastic(1, .3); Eases.Steps(5);
Eases.CubicBezier(.17, .67, .83, .67);
Eases.Linear(0, "0.25 75%", 1);   // method: params LinearStop[] (double or "value position")
Eases.Spring(mass: 1, stiffness: 100, damping: 10, velocity: 0);
Eases.FromPath("M0,0 C0.3,0 0.4,1.2 0.7,1.1 C0.85,1.05 0.9,1 1,1");
Eases.Wiggle(6, WiggleType.Anticipate); Eases.Rough(20, 1); Eases.SlowMo(.5, .8);
Eases.Irregular(10, 1); Eases.ExpoScale(1, 4);
EaseParser.Parse("outElastic(1.2, .4)");
Springs.Gentle; Springs.Snappy; Springs.Bouncy; Springs.Stiff;
```

`Eases.Linear` is **not** a property — it takes `params LinearStop[]`, and `LinearStop` has implicit conversions from `double` and from a `"value position"` string. Use `Eases.None` for a true identity ease.

A `Spring` computes its own settling duration; setting `Duration` alongside it is pointless. Strings work anywhere an ease is accepted (`Ease = "inOutQuint"`). Avalonia `Easing` converts both ways via `PrestoEasing`.

## Keyframes

```csharp
// Per property
.X(k => k.To(200, duration: 500, ease: Eases.OutQuad).To(80, duration: 300).To(0, 600))

// Shared frames, by order
.Keyframes(k => k
    .Frame(f => f.Y(-40).Rotate(-20))
    .Frame(f => f.X(220).Ease(Eases.OutQuad))
    .Frame(f => f.X(0).Rotate(0).Duration(600)))

// Shared frames, by percentage
.Keyframes(k => k
    .At(0, f => f.X(0).Scale(1))
    .At(60, f => f.Scale(1.6))
    .At(100, f => f.X(0).Scale(1)))
```

## Timeline

```csharp
var tl = Presto.CreateTimeline(new() { Defaults = new() { Duration = 600, Ease = Eases.OutQuad } })
    .Add(a, x => x.X(300), 0)                       // absolute ms
    .Add(b, x => x.X(300))                          // appended
    .Add(c, x => x.X(300), "-=400")                 // relative to end
    .Add(d, x => x.X(300), "<")                     // previous child's end ("<<" = start)
    .Label("flash", "+=200")
    .Add(e, x => x.Rotate(360), "flash")
    .Add(new TimerParams { Duration = 400, OnUpdate = t => Show(t) }, 20)
    .Call(_ => Log("half way"), "flash")
    .Sync(existingAnimation, 0);

tl.Seek(tl.Labels["flash"]);
```

Typed positions: `TimelinePosition.End`, `.PreviousEnd`, `.PreviousStart`, `flash + 100`.

## Stagger

```csharp
Presto.Animate(children, b => b.Y(-14).Delay(Presto.Stagger(60)));
Presto.Stagger(50, new() { From = StaggerFrom.Center, Total = 24 });
Presto.Stagger(40, new() { Grid = [7, 4], Axis = StaggerAxis.X, Reversed = true });
PrestoAvalonia.Stagger(40, new() { From = StaggerFrom.Center });  // grid from layout
```

## Animatable (continuously retargeted values)

```csharp
var follower = control.CreateAnimatable(new() { ["X"] = 500, ["Y"] = 500, Ease = Eases.Out(3) });
follower.Set("X", point.X).Set("Y", point.Y);
follower.Set("X", -140, 1200, "inOutQuad");
follower.Revert();

var springy = control.CreateAnimatable(new() { ["X"] = new(), Ease = new Spring(stiffness: 90, damping: 9), PreserveVelocity = true });
```

## Scopes and lifetime

```csharp
var scope = Presto.CreateScope(new ScopeParams { Defaults = d => d.Ease = Eases.OutQuad });
scope.Execute(_ => Presto.Animate(model, b => b.Property(nameof(model.Progress), 100)));
scope.Revert();

// Bridge: reverts everything created inside when the control leaves the visual tree.
var controlScope = PrestoAvalonia.CreateScope(this);
controlScope.Execute(_ => { /* build animations */ });
```

`PrestoAvalonia.WhenDetached` — `Continue` (default for core), `Pause` (bridge default), `Revert`. Override per builder with `.WhenDetached(WhenDetached.Revert)`.

## Declarative XAML

```xml
<!-- Lifecycle -->
<Border presto:Presence.Initial="{presto:Fade Direction=Up, Stagger=60}"
        presto:Presence.Enter="{x:Static local:Motions.SlideIn}"
        presto:Presence.Exit="{presto:Slide Edge=Right, Out=True}" />

<ContentControl Content="{Binding Step}" presto:Presence.Mode="Wait" />

<!-- State motion -->
<Button presto:Motion.Hover="{presto:Lift}"
        presto:Motion.Press="{presto:PressIn}"
        presto:Motion.Focus="{presto:Glow}" />

<Border presto:Motion.State="{Binding Status}">
    <presto:Motion.States>
        <presto:MotionStates>
            <presto:MotionState When="Syncing" Motion="{x:Static local:Motions.Pulse}" />
            <presto:MotionState When="Failed" Motion="{x:Static local:Motions.Alarm}" Leave="{x:Static local:Motions.Calm}" />
        </presto:MotionStates>
    </presto:Motion.States>
</Border>

<!-- One-shot triggers -->
<Button presto:Animate.OnTapped="{x:Static local:Motions.Pop}" />
<TextBlock presto:Animate.Watch="{Binding Count}" presto:Animate.OnChanged="{x:Static local:Motions.Bump}" />
<Border presto:Animate.OnAttached="{presto:Shimmer}" />

<!-- Layout -->
<Border presto:Animate.Layout="{presto:Reflow}" presto:Animate.LayoutMode="PositionAndSize" />
<ItemsControl ItemsSource="{Binding Rows}" presto:Animate.ItemLayout="{presto:Reflow 320}" />
<Border presto:Animate.ContentSize="{presto:Reflow}" />

<!-- Connected (shared element) -->
<Border presto:Connected.Key="{Binding Id}" presto:Connected.Motion="{x:Static local:Motions.Flight}" />
```

Recipe extensions: `{presto:Fade}`, `{presto:Scale}`, `{presto:Pop}`, `{presto:Blur}`, `{presto:Slide}`, `{presto:Shake}`, `{presto:Lift}`, `{presto:PressIn}`, `{presto:Glow}`, `{presto:Shimmer}`, `{presto:Reflow}`, `{presto:PlayFrames}`, `{presto:LineRise}`, `{presto:WordFade}`, `{presto:CharCascade}`, page transitions (`{presto:PageSlide}`, `{presto:PagePush}`, `{presto:PageFade}`).

All take `Duration`, `Delay`, `Ease`, `Stagger`, `Out`.

## Shapes

```csharp
PrestoAvalonia.CreateDrawable(shape).Animate(b => b.Draw(from: "0 0", to: "0 1").Duration(2200));

var motion = PrestoAvalonia.CreateMotionPath(trackShape);
rider.Animate(b => b.X(motion.X).Y(motion.Y).Rotate(motion.Rotate).Duration(3000));

var spline = PrestoAvalonia.CreateMotionPath(points, curviness: 1.2, autoRotate: 90);
track.Data = PrestoAvalonia.MotionPathGeometry(points, curviness: 1.2);

shape.Animate(b => b.Data(PrestoAvalonia.MorphTo(destination)).Duration(900));
```

`Draw` values are fractions of the contour: `"0 0"` hidden, `"0 1"` fully drawn, `"0.25 0.75"` a slice. Reverting restores the original dash state.

## Threads

```csharp
icon.AnimateOnCompositor(b => b.Rotate(360).Duration(1200).Loop());
var spin = icon.Animate(builder, AnimationThread.PreferCompositor);
bool fits = PrestoAvalonia.CanAnimateOnCompositor(builder, out var reason);
```

Compositor supports only `X`, `Y`, `Rotate`, `Scale`, `ScaleX`, `ScaleY`, `Opacity` and no keyframes/callbacks/composition. Everything else throws or falls back. Layout transitions (`AnimateLayout`, `AnimateSharedElement`) and `AnimateContentSize` are separate entry points.

## Reduced motion

```csharp
Presto.Defaults.ReducedMotion = settings.ReduceMotion;
Presto.Defaults.ReduceAll = true;                        // everything becomes an instant set
PrestoAvalonia.MotionPreference = MotionPreference.System; // Full | Reduced | System

builder.WhenReduced(r => r.Opacity(from: 0, to: 1).Duration(120));
```

The flag is read when a builder becomes params, so running animations keep their form. `Recipes` all ship a reduced alternative.

## Effects and filters (UI thread only)

```csharp
card.Animate(b => b.Shadow(0, 12, 24, Color.Parse("#66000000")));
title.Animate(b => b.Blur(from: 12, to: 0).Opacity(from: 0, to: 1));
tile.Animate(b => b.RotateY(180).Perspective(600));
photo.Animate(b => b.ClipCircle(from: 0, to: 1));
photo.Animate(b => b.ClipInset(from: "50%", to: "0%"));
photo.Animate(b => b.MaskSweep(from: 0, to: 1).MaskSweepAngle(90));
```

One effect per visual, one clip per visual. Effects at rest (blur 0, opacity 0 shadow, fully visible clip) are removed automatically.

## Recipes worth knowing

Entrances: `Fade`, `FadeUp/Down/Left/Right`, `ScaleIn`, `PopIn`, `BlurIn`, `SlideIn`, `Gather`, `FlipIn`, `Develop`.
Exits: `FadeOut*`, `ScaleOut`, `PopOut`, `BlurOut`, `SlideOut`, `FlipOut`, `Scatter`, `Fall`.
Attention: `Shake`, `Pulse`, `Wobble`, `Jelly`, `Heartbeat`, `Tada`, `Bounce`, `FlashTint`.
Loops: `Float`, `Spin`, `Breathe`, `Blink`, `Wave`.
Interactions: `Lift`, `Grow`, `PressIn`, `Glow`, `Dim`, `Colorize`.
Loading: `Shimmer`, `Spinner`, `Dots`, `Indeterminate`.
Media: `PlayFrames`, `Confetti`, `Sparkles`, `Burst`, `Assemble`, `Disintegrate`, `DrawIn`, `DrawOut`, `CircularReveal`.
Text: `Typewriter`, `CountUp`, `RollDigits`.
Tokens: `Recipes.Tokens` (`MotionTokens.Standard | Expressive | Reduced`, or `with { … }`).

## Gotchas

| Symptom | Cause |
|---|---|
| Animation flickers or restarts | Started on every tick; add edge detection |
| Facing/rotation snaps back | Presto shorthands and manual `RenderTransform` on the same control |
| Scale/rotate grows from the top-left | Shorthands act about `RenderTransformOrigin` (default `0,0`); set it to `0.5,0.5` |
| Values snap on completion | `Cancel()` leaves values; `Revert()` restores underlying values |
| `control.Cancel()` does not compile | `Cancel`/`Revert` live on `Animation`, not on the target; keep the returned handle |
| Reduced-motion variant loses a property (e.g. stroke stays hidden) | `WhenReduced` builds a new builder with **only** its own properties — restate anything the reduced state needs |
| `InvalidOperationException` adding a shadow | A blur/clip is already on that visual |
| `NotSupportedException` on compositor | Property or setting without a composition counterpart (`Width`, `OnUpdate`, keyframes…) |
| Animation keeps running after removal | No scope/Dispose; `WhenDetached.Pause` only pauses |
| Spring ignores `Duration` | Expected: spring duration is derived |
