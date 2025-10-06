# MakaMek Color Scheme

## Overview

The MakaMek application uses a military/tactical color scheme inspired by the mech icon.

## Color Palette

### Primary Colors - Military Green (from mech body)
Inspired by the olive/military green of the mech body in the icon.

- **Primary**: `#6B8E23` - Main body green
- **Primary Light**: `#8FA557` - Lighter panels
- **Primary Dark**: `#556B2F` - Darker sections

**Usage**: Main buttons, primary actions, success states, borders for important panels

### Secondary Colors - Tactical Blue (from cockpit canopy)
Inspired by the bright cyan/blue of the cockpit canopy.

- **Secondary**: `#2196F3` - Main canopy blue
- **Secondary Light**: `#4FC3F7` - Top of canopy
- **Secondary Dark**: `#1976D2` - Darker blue

**Usage**: Information displays, mech armor indicators, movement indicators, secondary actions

### Accent Colors - Purple (from sensor)
Inspired by the purple/lavender sensor/camera on the mech.

- **Accent**: `#7E57C2` - Medium purple
- **Accent Light**: `#9575CD` - Light purple
- **Accent Dark**: `#5E35B1` - Dark purple

**Usage**: Special highlights, sensor-related UI elements, tertiary actions

### Highlight Colors - Gold/Yellow (from lights)
Inspired by the warm yellow lights and details on the mech.

- **Highlight**: `#FFC107` - Medium yellow/gold
- **Highlight Light**: `#FFD54F` - Light yellow
- **Highlight Dark**: `#FFA000` - Dark gold

**Usage**: Warnings, mech structure indicators, important highlights, heat indicators (gradient)

### Neutral Colors - Dark Tactical Theme
Dark backgrounds for a tactical, military feel.

- **Background**: `#1A1A1A` - Very dark gray (main background)
- **Surface**: `#2A2A2A` - Dark gray (panels, cards)
- **Surface Light**: `#3A3A3A` - Medium dark gray (elevated surfaces)
- **Text**: `#E0E0E0` - Light gray (primary text)
- **Text Light**: `#B0B0B0` - Medium gray (secondary text)
- **Border**: `#4A4A4A` - Medium gray (borders, dividers)

### Semantic Colors

- **Success**: `#6B8E23` - Uses Primary color (military green)
- **Warning**: `#FFC107` - Uses Highlight color (gold/yellow)
- **Error**: `#D32F2F` - Red for errors and critical states
- **Info**: `#2196F3` - Uses Secondary color (tactical blue)

### Game-Specific Colors

- **Mech Armor**: `#4FC3F7` - Light blue (Secondary Light)
- **Mech Structure**: `#FFD54F` - Light yellow (Highlight Light)
- **Heat**: `#D32F2F` - Red (Error color)
- **Destroyed**: `#80D32F2F` - Semi-transparent red
- **Damaged**: `#80FFC107` - Semi-transparent yellow

### Overlay Colors

- **Overlay Background**: `#1A1A1A` - Same as Background
- **Overlay Transparent**: `#CC1A1A1A` - Semi-transparent dark overlay (80% opacity)
- **Targeting**: `#D32F2F` - Red for targeting indicators
- **Movement**: `#4FC3F7` - Light blue for movement indicators

### Shadow Colors

- **Shadow Light**: `#20000000` - 12.5% opacity black (subtle shadows)
- **Shadow Medium**: `#40000000` - 25% opacity black (medium shadows)
- **Shadow Dark**: `#80000000` - 50% opacity black (dialog overlays)

## Usage Guidelines

### Buttons

- **Primary Buttons**: Use Primary colors (military green) for main actions
- **Secondary Buttons**: Use Secondary colors (tactical blue) for alternative actions
- **Outline Buttons**: Use Primary border with transparent background
- **Icon Buttons**: Transparent background with hover states

### Panels and Cards

- **Background**: Use Background color for main app background
- **Surface**: Use Surface color for panels and cards
- **Borders**: Use Border color or Primary color for important panels
- **Shadows**: Use Shadow colors for depth

### Text

- **Primary Text**: Use Text color for main content
- **Secondary Text**: Use Text Light color for less important content
- **Headings**: Use Text color with appropriate font weight

### Game Elements

- **Mech Health Bars**: 
  - Armor: Mech Armor color (light blue)
  - Structure: Mech Structure color (light yellow)
  - Destroyed: Destroyed color (semi-transparent red)
  
- **Heat Indicator**: Gradient from Secondary Light (blue) → Highlight (yellow) → Error (red)

- **Status Indicators**:
  - Active/Ready: Success color (green)
  - Warning: Warning color (yellow)
  - Critical/Error: Error color (red)
  - Info: Info color (blue)

### Accessibility

- **Contrast Ratios**: All text colors meet WCAG AA standards against their backgrounds
- **Color Blindness**: Important states use both color and icons/text
- **Dark Theme**: The entire scheme is designed as a dark theme for reduced eye strain

## Implementation

All colors are defined in `src/MakaMek.Avalonia/MakaMek.Avalonia/Styles/Colors.axaml` as:

1. **Color Resources**: Named colors (e.g., `PrimaryColor`)
2. **Brush Resources**: SolidColorBrush instances (e.g., `PrimaryBrush`)

### Using Colors in XAML

```xml
<!-- Using a brush directly -->
<Button Background="{DynamicResource PrimaryBrush}" />

<!-- Using a color in a gradient -->
<LinearGradientBrush>
    <GradientStop Offset="0" Color="{StaticResource SecondaryLightColor}"/>
    <GradientStop Offset="1" Color="{StaticResource ErrorColor}"/>
</LinearGradientBrush>
```

### Dynamic vs Static Resources

- **DynamicResource**: Use for brushes that might change (e.g., theme switching)
- **StaticResource**: Use for colors in gradients and other static contexts

## Files Modified

1. `src/MakaMek.Avalonia/MakaMek.Avalonia/Styles/Colors.axaml` - Main color definitions
2. `src/MakaMek.Avalonia/MakaMek.Avalonia/Styles/Panels.axaml` - Updated shadow colors
3. `src/MakaMek.Avalonia/MakaMek.Avalonia/Controls/UnitHeatLevelPanel.axaml` - Updated heat gradient
4. `src/MakaMek.Avalonia/MakaMek.Avalonia/Controls/UnitMovementInfoPanel.axaml` - Updated overlay color
5. `src/MakaMek.Avalonia/MakaMek.Avalonia/Views/StartNewGame/Fragments/PlayersFragment.axaml` - Updated overlay color

## Future Enhancements

1. **Theme Switching**: Add support for light theme variant
2. **Color Customization**: Allow users to customize accent colors
3. **Faction Colors**: Add faction-specific color schemes
4. **Accessibility Options**: Add high-contrast mode
5. **Animation**: Add color transitions for state changes

