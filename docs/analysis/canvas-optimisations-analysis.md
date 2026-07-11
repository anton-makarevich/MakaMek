# Research Avalonia canvas virtualization

**Date*: 2026-07-11

---

## 1. Does Avalonia's Canvas do built-in viewport culling during rendering?

**Yes, at the composition renderer level -- but not at the Canvas layout level.**

Avalonia's composition renderer (introduced in v11 and significantly improved in v12 via PR [#20497](https://github.com/AvaloniaUI/Avalonia/pull/20497)) tracks **subtree bounds** for every visual in the composition tree. During the render pass (Pass 5), it checks dirty rect intersections and **skips rendering** for any visual whose subtree bounds do not intersect the dirty rect:

> "Render while checking for dirty rect intersections (but more efficiently now since we can skip not only based on clip but on subtree bounds too)"

This means if a child of a Canvas is positioned far off-screen and nothing has changed that requires repainting it, the renderer will skip it. The key PR #12568 ([Enhanced Clipping and Rendered Visuals Tracking](https://github.com/AvaloniaUI/Avalonia/pull/12568)) specifically describes this:

> "ServerCompositionVisual.Render method do nothing if boundaries of the visual do not intersect with DirtyRect. This approach effectively minimizes the areas of the scene redrawn."

However, this is **dirty-rect based** culling -- it only avoids re-rendering things that haven't changed. The critical caveat is that **the Canvas itself still creates, measures, arranges, and participates in hit-testing for ALL children**, even those far off-screen. The Canvas class (`Canvas.cs`) inherits from `Panel` and has no built-in viewport-awareness. Its `ArrangeOverride` simply positions every child based on attached properties (`Canvas.Left`, `Canvas.Top`). There is no mechanism in the Canvas itself to skip creating or laying out off-screen children.

In short:
- **Dirty-rect rendering culling**: Yes, the composition engine avoids re-drawing unchanged off-screen visuals. This helps with *rendering* performance.
- **Layout/creation culling**: No. The Canvas creates and lays out every child regardless of position. This is the expensive part that virtualization addresses.

---

## 2. Is there a `VirtualizingCanvas` or similar panel in Avalonia?

**No. There is no built-in `VirtualizingCanvas` in Avalonia (including v12.x).**

The only built-in virtualizing panel is `VirtualizingStackPanel`. There is no `VirtualizingCanvas`, `VirtualizingGrid`, `VirtualizingWrapPanel`, or similar 2D virtualizing panel shipped with Avalonia.

This is a known gap in the ecosystem. A commenter on PR [#20993](https://github.com/AvaloniaUI/Avalonia/pull/20993) explicitly asked:

> "I'm eager to see this project come to fruition so after for a VirtualizingCanvas for ItemsControls for 2D virtualization management, allowing to display 2D graphs with numerous controls on an unlimited canvas :-) Do you know if this already exists?"

And on Discussion [#17337](https://github.com/AvaloniaUI/Avalonia/discussions/17337), a developer working on a similar problem reported:

> "Finally, I started looking into the way to create something like VirtualizingCanvas and already even implemented a part of it but with no [success]..."

However, the framework provides the necessary **building blocks** to create one yourself:
- `VirtualizingPanel` -- abstract base class for virtualizing panels
- `VirtualizingLayout` -- base class for custom virtualizing layout algorithms
- `ItemsRepeater` with `VirtualizingLayout` -- supports custom layouts with virtualization
- `VirtualizingLayoutContext.RealizationRect` -- the visible area that the layout should realize items for

---

## 3. Recommended pattern for many items on a Canvas but only rendering visible ones?

Based on the official documentation and community patterns, there are three main approaches, in order of recommendation:

### Approach A: Custom `VirtualizingLayout` + `ItemsRepeater` (Recommended for data-driven scenarios)

`ItemsRepeater` with a custom `VirtualizingLayout` is the recommended way to get Canvas-like positioning with virtualization. The `VirtualizingLayoutContext` provides `RealizationRect` (the visible viewport) and `GetOrCreateElementAt()` / `RecycleElement()` APIs that let you create/recycle elements only within the viewport.

```xml
<ItemsRepeater ItemsSource="{Binding Items}">
    <ItemsRepeater.Layout>
        <local:VirtualizingGridLayout />  <!-- Your custom 2D layout -->
    </ItemsRepeater.Layout>
</ItemsRepeater>
```

From the official docs ([Custom ItemsPanel](https://github.com/AvaloniaUI/avalonia-docs/blob/main/docs/custom-controls/custom-itemspanel.md)):

> "When you replace the default panel with a non-virtualizing panel such as `Canvas` or `WrapPanel`, the control creates a UI element for every item in the collection. For large collections (hundreds or thousands of items), this can significantly increase memory usage and degrade rendering performance. If you need custom layout with large data sets, **consider building a custom panel that extends `VirtualizingPanel`** to retain virtualization support."

The challenge is implementing the `VirtualizingLayout` correctly for 2D positioning. The key method is checking `context.RealizationRect` to determine which items should be realized, and only calling `context.GetOrCreateElementAt()` for items within that rect. A StackOverflow answer ([#78410493](https://stackoverflow.com/questions/78410493/creating-a-custom-virtualizinglayout-for-avalonia-itemsrepeater)) documents the difficulty of getting this right, noting issues like "fast scrolling on large datasets would draw nothing at all."

### Approach B: `ItemsControl` with `Canvas` + manual visibility management (Simpler but less scalable)

You can use `ItemsControl` with `Canvas` as the `ItemsPanel`, but you must manually manage visibility:

```xml
<ItemsControl ItemsSource="{Binding TileList}">
    <ItemsControl.ItemsPanel>
        <ItemsPanelTemplate>
            <Canvas />
        </ItemsPanelTemplate>
    </ItemsControl.ItemsPanel>
    <!-- position items via Canvas.Left/Canvas.Top bindings -->
</ItemsControl>
```

This creates a control for every item. For large sets, you'd need to manually set `IsVisible="False"` on off-screen items (which removes them from both layout and rendering per the performance docs).

### Approach C: Custom rendering (Best for extremely large item counts)

From the [Hit Testing performance docs](https://docs.avaloniaui.net/docs/graphics-animation/hit-testing#performance-with-many-elements):

> "Switch to custom rendering. Instead of creating a separate control for each element, render all elements in a single control's `Render` override. This eliminates per-element hit testing entirely."

For a Canvas-like scenario with thousands of items, overriding `Render()` on a single `Control` and drawing directly via Skia (using `ICustomDrawOperation`) is the most performant option. You manage your own viewport culling and hit testing.

### VirtualizingStackPanel with Canvas scenario?

`VirtualizingStackPanel` is **not relevant** for a Canvas use case. It only works along a single axis (vertical or horizontal) and expects items to be stacked linearly. It would not handle 2D positioning. From the docs:

> "`VirtualizingStackPanel` works best when all items have the same height."

---

## 4. Does Avalonia's Canvas clip and skip off-screen children in its internal rendering pass?

**Partially -- only at the composition rendering level, not at layout.**

The full picture:

1. **Canvas layout**: The Canvas `ArrangeOverride` positions every child unconditionally. All children are measured and arranged regardless of viewport position. There is no culling here.

2. **Composition rendering**: The renderer does skip drawing visuals whose subtree bounds do not intersect the current dirty rect (described in the [Composition system SP1 PR #20497](https://github.com/AvaloniaUI/Avalonia/pull/20497)). But this only means the GPU/Skia draw calls are skipped -- the controls already exist, have been measured, arranged, and are tracked in the visual tree.

3. **ClipToBounds**: Setting `ClipToBounds="True"` on the Canvas creates a clip layer. Children outside the Canvas bounds are still created and laid out but are clipped during rendering. From the [Canvas docs](https://docs.avaloniaui.net/controls/layout/panels/canvas):

   > "The default behavior of a Canvas is to allow children to be drawn outside the bounds of the parent Canvas. If this behavior is undesirable, the ClipToBounds property can be set to true."

4. **Hit testing**: The hit-test path walks the visual tree linearly (though PR [#21310](https://github.com/AvaloniaUI/Avalonia/pull/21310) adds AABB-tree optimization for large child counts in v12+). From the [Hit Testing docs](https://docs.avaloniaui.net/docs/graphics-animation/hit-testing#performance-with-many-elements):

   > "Avalonia's hit-testing walks the visual tree and tests each element individually. There is no built-in spatial partitioning (such as a quadtree). For panels with a small number of children, this is fast. When you have hundreds or thousands of interactive elements on a `Canvas` or `Panel`, the linear walk becomes noticeable."

5. **Animation processing**: Since Avalonia v12, animations on invisible (off-viewport) visuals are paused automatically ([PR #20820](https://github.com/AvaloniaUI/Avalonia/pull/20820)). This helps if off-screen children have animations running.

**Summary**: The renderer can skip GPU draw calls for unchanged off-screen visuals, but the layout system, visual tree management, and (historically) hit testing still process ALL children. For a Canvas with many items, the bottleneck is typically hit testing and layout, not rendering.

---

## 5. Search results synthesis: "Avalonia Canvas virtualization", "Avalonia ItemsRepeater canvas", "Avalonia virtualizing panel"

Key findings from the searches:

**No VirtualizingCanvas exists in Avalonia.** This is confirmed across official docs, GitHub issues, discussions, and the source code. The only shipped virtualizing panel is `VirtualizingStackPanel`.

**The closest alternatives are:**

| Mechanism | Virtualization | 2D Layout | Built-in |
|-----------|:---:|:---:|:---:|
| `VirtualizingStackPanel` | Yes | No (1D only) | Yes |
| `ItemsRepeater` + `VirtualizingLayout` | Yes | Via custom layout | Yes (framework) |
| `ItemsRepeater` + `UniformGridLayout` | Yes | Grid (uniform) | Yes |
| `ItemsRepeater` + custom `VirtualizingLayout` | Yes | Any (custom) | No (write your own) |
| `ItemsControl` + `Canvas` | No | Yes | Yes |
| Custom `Control` + `Render()` override | Manual | Yes | No (write your own) |

**For the MakaMek use case specifically** (a BattleTech tabletop game with a hex map, unit tokens, and many visual elements on a canvas), the options are:

1. **Custom `VirtualizingLayout` with `ItemsRepeater`**: If you have a data-driven set of items (e.g., units, markers) that need 2D positioning, write a `VirtualizingLayout` subclass that checks `context.RealizationRect` and only realizes items within the visible viewport. This is the "correct" Avalonia-native approach but requires non-trivial implementation work.

2. **Custom rendering via `ICustomDrawOperation`**: For extreme performance (thousands of hexes, sprites, etc.), render everything yourself via Skia in a single `Render()` override. You manage your own viewport culling. The performance docs and community discussions consistently recommend this for scenarios with many thousands of drawn objects.

3. **Hybrid approach**: Use a custom-rendered Canvas for the map terrain and static elements (drawn once, cached via `BitmapCache`), but use individual Controls (or a `VirtualizingPanel`) for interactive elements like unit tokens that need pointer events and animations. This separates the rendering-heavy static content from the interaction-heavy dynamic content.

**Performance docs link**: [https://docs.avaloniaui.net/docs/app-development/performance](https://docs.avaloniaui.net/docs/app-development/performance)
</task_result>
</task>