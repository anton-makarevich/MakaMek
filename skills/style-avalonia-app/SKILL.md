---
name: style-avalonia-app
description: "Use this skill whenever the task involves styling an AvaloniaUI app — creating or editing XAML style files, defining colors/brushes/themes, building TemplatedControls with ControlTemplates, or setting up the style/resource merge chain from App.axaml. Triggers include: 'add a theme', 'style this button', 'create a custom control', 'change colors', 'make a reusable panel', 'set up typography', or any work touching .axaml style files, ResourceDictionary, Styles, or TemplatedControl."
metadata:
  author: MakaMek project conventions
  version: "1.0"
  framework: AvaloniaUI
  reference-project: MakaMek.Avalonia.Controls
---

# Style an Avalonia App — Coat

Coat is the process of layering styles and control templates onto an AvaloniaUI application. It follows a two-hierarchy merge: **resources** (colors, animations, icons) via `ResourceDictionary.MergedDictionaries`, and **styles** (class selectors, control templates) via `<StyleInclude>`.

---

## The Two-Hierarchy Merge

```
App.axaml
 ├── Application.Resources ─── ResourceInclude ─── Resources.axaml
 │                                                    ├── Colors.axaml
 │                                                    ├── Animations.axaml
 │                                                    └── Icons.axaml
 └── Application.Styles
      ├── FluentTheme (base)
      └── StyleInclude ─── Theme.axaml
                              ├── Typography.axaml
                              ├── Buttons.axaml
                              ├── Panels.axaml
                              ├── Controls.axaml
                              ├── Layouts.axaml
                              └── TemplatedControls/*.axaml
```

Every `.axaml` file goes in a separate class-library project (e.g. `MakaMek.Avalonia.Controls`), referenced via `avares://` pack URIs. `App.axaml` includes exactly two files — `Resources.axaml` and `Theme.axaml` — and everything else is transitive.

---

## Step 1 — Define the Color Palette

Create `Colors.axaml` as a `<ResourceDictionary>`. Define:

1. **Raw `<Color>` resources** grouped by role (Primary, Secondary, Accent, Highlight, Neutral, Semantic, Game-specific, Overlay, Shadow).
2. **Corresponding `<SolidColorBrush>` resources** that reference the colors via `{StaticResource}`.

MakaMek reference: `MakaMek.Avalonia.Controls/Styles/Colors.axaml`

Completion criterion: every brush used elsewhere references a `{StaticResource}` color — no hardcoded hex values outside this file.

---

## Step 2 — Define Supporting Resources

Create separate `ResourceDictionary` files for non-color assets:

- **`Animations.axaml`** — reusable `<Animation>` resources (e.g. fade-out, pulse).
- **`Icons.axaml`** — `<StreamGeometry>` resources for icon paths (FontAwesome-style vector data).

Merge all of them into a `Resources.axaml` hub:

```xml
<ResourceDictionary>
    <ResourceDictionary.MergedDictionaries>
        <ResourceInclude Source="avares://Your.Assembly/Styles/Colors.axaml"/>
        <ResourceInclude Source="avares://Your.Assembly/Styles/Animations.axaml"/>
        <ResourceInclude Source="avares://Your.Assembly/Styles/Icons.axaml"/>
    </ResourceDictionary.MergedDictionaries>
    <!-- Project-wide resources (fonts, etc.) -->
</ResourceDictionary>
```

MakaMek reference: `MakaMek.Avalonia.Controls/Styles/Resources.axaml`

---

## Step 3 — Write Style Files by Concern

Split styles into focused files, each a `<Styles>` root with class-selector rules:

| File | Controls styled | Class prefix examples |
|---|---|---|
| `Typography.axaml` | `TextBlock`, `TextBox` | `.h1`, `.body`, `.label`, `.caption`, `.turnInfo` |
| `Buttons.axaml` | `Button` | `.primary`, `.secondary`, `.outline`, `.icon`, `.actionButton`, `.cardAction`, `.cornerBadge`, `.inlineIcon`, `.actionButtonText` |
| `Panels.axaml` | `Border`, `Grid`, `StackPanel` | `.card`, `.gamePanel`, `.dialogContainer`, `.formGroup`, `.statusTag` |
| `Controls.axaml` | `ProgressBar`, `ListBox`, `Slider`, `CheckBox`, `TabControl`, `ScrollViewer` | `.mechArmor`, `.gameList`, `.gameSlider`, `.gameCheckBox`, `.gameTabControl` |
| `Layouts.axaml` | `Grid`, `StackPanel`, `DockPanel` | `.pageContainer`, `.section`, `.centeredContent`, `.rightAligned` |

Rules for each style:
- Use CSS-like class selectors: `Button.primary`, `TextBlock.h1`, `Border.card`.
- Define all pseudo-class states inline: `:pointerover`, `:pressed`, `:disabled`.
- Reference brushes with `{StaticResource BrushKey}` — never hardcode colors.
- Use `/template/` syntax for templated parts (e.g. `Slider /template/ Thumb`).

MakaMek reference: `MakaMek.Avalonia.Controls/Styles/*.axaml`

---

## Step 4 — Create TemplatedControls

Each custom **TemplatedControl** gets two files:

### 4a — Code-behind (`.axaml.cs`)

Choose the right base class:

| Base class | When | MakaMek example |
|---|---|---|
| `Button` | Control that acts as a clickable button | `ActionButton` — adds `IconData` property |
| `ContentControl` | Container hosting arbitrary child content | `GamePanel` — adds `Title`, `CloseCommand` |
| `TemplatedControl` | Composite shell with no predefined content slot | `ToolBar` — builds layout from multiple sub-controls |

Register each custom property as `StyledProperty<T>`:

```csharp
public static readonly StyledProperty<Geometry?> IconDataProperty =
    AvaloniaProperty.Register<ActionButton, Geometry?>(nameof(IconData));

public Geometry? IconData
{
    get => GetValue(IconDataProperty);
    set => SetValue(IconDataProperty, value);
}
```

For computed read-only properties (e.g. `HasLeftButton`), use `DirectProperty` and raise change notifications in the static constructor via `PropertyChanged.AddClassHandler`.

### 4b — Template (`.axaml`)

Write a `<Styles>` root with a single style for the control's selector that sets the `Template` property:

```xml
<Styles xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:controls="using:Your.Namespace.TemplatedControls">

    <Style Selector="controls|YourControl">
        <Setter Property="Template">
            <ControlTemplate>
                <!-- Build the visual tree here -->
            </ControlTemplate>
        </Setter>
    </Style>
</Styles>
```

Template rules:
- Bind to control properties with `{TemplateBinding PropertyName}`.
- Reference resources with `{StaticResource ResourceKey}`.
- Apply style classes via `Classes="className"` to pick up styles from Step 3.
- Embed other TemplatedControls inside templates for composability.

MakaMek references:
- `TemplatedControls/ActionButton.axaml` + `.axaml.cs` — extends `Button` with an icon
- `TemplatedControls/GamePanel.axaml` + `.axaml.cs` — extends `ContentControl` with header + close
- `TemplatedControls/ToolBar.axaml` + `.axaml.cs` — extends `TemplatedControl` with left/right buttons + title

---

## Step 5 — Merge Everything in Theme.axaml

Create `Theme.axaml` as a `<Styles>` root that includes every style file and TemplatedControl template:

```xml
<Styles xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <StyleInclude Source="avares://Your.Assembly/Styles/Typography.axaml"/>
    <StyleInclude Source="avares://Your.Assembly/Styles/Buttons.axaml"/>
    <StyleInclude Source="avares://Your.Assembly/Styles/Panels.axaml"/>
    <StyleInclude Source="avares://Your.Assembly/Styles/Controls.axaml"/>
    <StyleInclude Source="avares://Your.Assembly/Styles/Layouts.axaml"/>
    <StyleInclude Source="avares://Your.Assembly/TemplatedControls/YourControl.axaml"/>
</Styles>
```

Completion criterion: every `.axaml` file in Steps 1–4 is accounted for by exactly one `<StyleInclude>` or `<ResourceInclude>`.

---

## Step 6 — Wire Into App.axaml

```xml
<Application.Styles>
    <FluentTheme />
    <StyleInclude Source="avares://Your.Assembly/Styles/Theme.axaml"/>
</Application.Styles>

<Application.Resources>
    <ResourceDictionary>
        <!-- app-level converters, service collections, etc. -->
        <ResourceDictionary.MergedDictionaries>
            <ResourceInclude Source="avares://Your.Assembly/Styles/Resources.axaml"/>
        </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
</Application.Resources>
```

MakaMek reference: `MakaMek.Avalonia/App.axaml`

Completion criterion: all colors, brushes, icons, styles, and control templates from Steps 1–5 are usable in any view without additional includes.

---

## Step 7 — Verify the Coat

Open any view and use the styled classes and controls:

```xml
<!-- Class-based styling -->
<Button Classes="primary" />
<TextBlock Classes="h1" />
<Border Classes="card" />

<!-- TemplatedControl -->
<templatedControls:ToolBar
    LeftCommand="{Binding BackCommand}"
    LeftIcon="{StaticResource BackArrowIcon}"
    Title="My Page" />
```

Check that:
- All `{StaticResource}` keys resolve (no missing resource warnings).
- Pseudo-class visual states work (`:pointerover`, `:pressed`, `:disabled`).
- TemplatedControl properties bind correctly via `{TemplateBinding}`.

---

## Reference: MakaMek File Map

| File | Purpose |
|---|---|
| `Styles/Colors.axaml` | 28 `<Color>` + matching `<SolidColorBrush>` resources in 6 groups |
| `Styles/Animations.axaml` | Fade-out animation for damage labels |
| `Styles/Icons.axaml` | 8 `StreamGeometry` icon paths (Close, BackArrow, Chevrons, etc.) |
| `Styles/Resources.axaml` | Merges Colors + Animations + Icons; declares `AwesomeFontSolid` font |
| `Styles/Typography.axaml` | 17 text styles: headings h1–h4, body, label, caption, game-specific |
| `Styles/Buttons.axaml` | 10 button variants × 3–4 pseudo-class states each |
| `Styles/Panels.axaml` | 11 container/panel styles: card, dialog, form groups, status tags |
| `Styles/Controls.axaml` | 5 ProgressBar, ListBox, Slider, CheckBox, TabControl, ScrollViewer styles |
| `Styles/Layouts.axaml` | 6 layout utility styles: pageContainer, centered, aligned |
| `Styles/Theme.axaml` | Single aggregation point — includes all of the above + TemplatedControls |
| `TemplatedControls/ActionButton.axaml` + `.cs` | Circular 40×40 icon button (extends `Button`) |
| `TemplatedControls/GamePanel.axaml` + `.cs` | Overlay panel with header + close (extends `ContentControl`) |
| `TemplatedControls/ToolBar.axaml` + `.cs` | Three-section bar with left/right icon buttons (extends `TemplatedControl`) |
| `App.axaml` | Entry point — includes `Resources.axaml` and `Theme.axaml` only |
