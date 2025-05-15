# MakaMek

A cross-platform implementation of turn-based tabletop tactics BattleTech, built with .NET 9 and AvaloniaUI.

## Overview

MakaMek is an open-source tactical combat game featuring giant walking war machines. The game is inspired by another computer implementation of BattleTech called [MegaMek](https://megamek.org/) but focusing on simplicity and accessibility for all players. We aim to keep gameplay simple and prioritize a mobile-first and web-first user experience.

![MakaMek](docs/screenshots/win/150525.png)

## Features
### Implemented
- [Client-Server app architecture](https://github.com/anton-makarevich/MakaMek/wiki/Game-(Protocol)-High-Level-Architecture) with RX communication for local play
- LAN multiplayer with SignalR (only Desktop app can host the game, mobile clients could connect)
- Single-player combat with up to 4 players on a single device
- Complete Turn flow implementation with all major phases including initiative, movement, attack declaration and resolution, heat and end phase
- Critical hits, destruction of units, locations, and components
- Hex map generator with the simplest terrain types (clear, light, and heavy wood) (MegaMek assets)
- Cross-platform support (Windows, Linux, macOS, Web, Android, iOS)
- Prototype UI built with AvaloniaUI
- Importing mechs defined in MTF format 

### Planned
- Single-player combat against AI opponents
- Multiplayer over the Internet (WebSockets/SignalR)
- Unit customization and management
- Compatible with common community data formats
- Monogame version with 3D graphics and possible VR/AR support

## Technology Stack

- .NET 9
- AvaloniaUI for cross-platform UI
- xUnit for testing

## Project Structure

```
MakaMek/
├── src/
│   ├── MakaMek.Core/        # Core game engine and logic
│   └── MakaMek.Avalonia/    # UI implementation
├── tests/                   # Unit tests
└── docs/                    # Documentation (also synced to GitHub Wiki)
```

### Project Status

| Component                  | Build Status                                                                                                                                                                                             | Package/Download |
|----------------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|----------------|
| **Test Coverage (non-ui)** | [![codecov](https://codecov.io/github/anton-makarevich/MakaMek/graph/badge.svg?token=SAQTXWFA21)](https://codecov.io/github/anton-makarevich/MakaMek)                                                    | |
| **MakaMek.Core**           | [![build](https://github.com/anton-makarevich/MakaMek/actions/workflows/core.yml/badge.svg)](https://github.com/anton-makarevich/MakaMek/actions/workflows/core.yml)                                     | [![NuGet Version](https://img.shields.io/nuget/vpre/Sanet.MakaMek.Core?logo=nuget)](https://www.nuget.org/packages/Sanet.MakaMek.Core) |
| **MakaMek.Avalonia**       | [![build](https://github.com/anton-makarevich/MakaMek/actions/workflows/avalonia.yml/badge.svg)](https://github.com/anton-makarevich/MakaMek/actions/workflows/avalonia.yml)                             | [![NuGet Version](https://img.shields.io/nuget/vpre/Sanet.MakaMek.Avalonia?logo=nuget)](https://www.nuget.org/packages/Sanet.MakaMek.Avalonia) |
| **Web Version (WASM)**     | [![Deploy WASM to GitHub Pages](https://github.com/anton-makarevich/MakaMek/actions/workflows/deploy-wasm.yml/badge.svg)](https://github.com/anton-makarevich/MakaMek/actions/workflows/deploy-wasm.yml) | [![Play in Browser](https://img.shields.io/badge/Play-in%20Browser-blue?logo=github)](https://anton-makarevich.github.io/MakaMek/) |
| **Android Version**        | [![Build Android APK](https://github.com/anton-makarevich/MakaMek/actions/workflows/build-android.yml/badge.svg)](https://github.com/anton-makarevich/MakaMek/actions/workflows/build-android.yml)       | [![Download Android APK](https://img.shields.io/badge/Download-Android%20APK-green?logo=android)](https://github.com/anton-makarevich/MakaMek/actions/workflows/build-android.yml) |
| **Windows Version**        | [![Build Windows App](https://github.com/anton-makarevich/MakaMek/actions/workflows/build-windows.yml/badge.svg)](https://github.com/anton-makarevich/MakaMek/actions/workflows/build-windows.yml)       | [![Download Windows Installer](https://img.shields.io/badge/Download-Windows%20Installer-blue?logo=data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAADIAAAAyCAYAAAAeP4ixAAAACXBIWXMAAAsTAAALEwEAmpwYAAABE0lEQVR4nO3aMUrEYBDF8R/Y2W1hoa29CF7BwgvoEWw9gI2lF9ADWNraiTarWwhewcJKOytL/UTIQhrjsolk17w/pBnCzDd88xjICyGEZWUV2zjAMS7wgDeMLSAb2MUhTnGFJ3ygNDy9MMIO9nGCSzzi/ZfDlj4a+R6FLazXYufVKMx72PJXjaxgE3s4qg56g2d8VgVea+9PY701Mmo5ClNKn41cd1ik10ZKGpEbKRmtBqIR0YhopIloRDQiGmnivqVOxh3mKjPUCEvDpOW133WYq8xQ40eyR2SPyB5pIhoRjYhGmohGRCOikUF8xP43tkJbo+dlkYyeNi7sWi12tqjW2yDM0EHY013/MHA7V8YQBswXmfZIX4+AWlMAAAAASUVORK5CYII=)](https://github.com/anton-makarevich/MakaMek/actions/workflows/build-windows.yml) |
| **Linux Version**          | [![Build Linux App](https://github.com/anton-makarevich/MakaMek/actions/workflows/build-linux.yml/badge.svg)](https://github.com/anton-makarevich/MakaMek/actions/workflows/build-linux.yml)             | [![Download Linux AppImage](https://img.shields.io/badge/Download-Linux%20AppImage-orange?logo=linux)](https://github.com/anton-makarevich/MakaMek/actions/workflows/build-linux.yml) |

> **Note:** macOS and iOS builds require code signing and have a more complex distribution process. While these platforms are supported by the codebase, automated builds are not available yet.
> Users can build and deploy to Apple platforms from the source code.

## Development Setup

### Prerequisites

- .NET 9 SDK
- Your favorite IDE (Visual Studio, Rider, or VS Code)

### Building

1. Clone the repository
2. Open `MakaMek.sln` in your IDE
3. Build the solution

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

This project is licensed under the GNU General Public License v3.0 - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- Inspired by MegaMek (https://megamek.org/)
- Thanks to the BattleTech community for their continued passion.

## Name Origin

The name MakaMek contains references to MegaMek, but also to my surname and the very first battlemech ever created - the Mackie.

## Disclaimer

This is a fan-made game and is not affiliated with or endorsed by any commercial mech combat game properties. All trademarks belong to their respective owners.
This project is primarily a learning experience and a labor of love - developed for the enjoyment of the development process itself.
