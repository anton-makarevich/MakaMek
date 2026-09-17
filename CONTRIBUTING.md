# Contributing to MakaMek

Thank you for helping improve MakaMek, a cross-platform, turn-based BattleTech game. Small, focused changes are welcome, especially when they include tests and a clear explanation of the player-facing effect.

## Before you start

- Install the .NET 10 SDK and Git. Install platform workloads only for the targets you need.
- Check the existing issues before starting substantial work. For a new feature or a change to game rules, open an issue first so the design can be discussed.
- Fork the repository, create a focused branch, and keep unrelated formatting or refactoring out of the change.

Suggested branch names are `fix/123-short-description`, `feature/123-short-description`, and `docs/short-description`.

## Build and test

From the repository root:

```bash
dotnet build MakaMek.slnx
dotnet test MakaMek.slnx
```

For a faster Core-only iteration:

```bash
dotnet test tests/MakaMek.Core.Tests/MakaMek.Core.Tests.csproj
```

Run the desktop application with:

```bash
dotnet run --project src/MakaMek.Avalonia/MakaMek.Avalonia.Desktop
```

When changing code, add or update tests in the corresponding test project. Core tests use xUnit, Shouldly, and NSubstitute. Keep presentation logic in `MakaMek.Presentation` so it can be tested without an Avalonia view.

## Project boundaries

The normal dependency direction is `Avalonia → Presentation → Core`. Core contains the authoritative game rules and should not depend on UI code. Commands are the normal mechanism for changing game state and for communicating changes between server and client.

When adding command, component, movement-cost, roll-modifier, or piloting-resolution types, follow the existing source-generator conventions; generated registries should not be hand-maintained. Consult the relevant architecture documents under `docs/architecture/` and use `docs/INDEX.md` to find other project guidance.

Do not modify or commit derived game art in `data/`. That content is distributed separately under its upstream license.

## Pull requests

Before opening a pull request:

- Link the issue or explain why the change is needed.
- Describe the behavior changed and any compatibility or rules implications.
- Include regression tests for bugs and tests for new behavior.
- Run the relevant tests, and include the commands used in the PR description.
- If any file under `src/` changed, increment only the patch segment of `VersionPrefix` in `Directory.Build.props`. Increment it once per PR.
- Keep the PR focused and update documentation when behavior or public guidance changes.

Pull requests should be ready for review, build cleanly in CI, and make it easy to verify the intended behavior. Maintainers may request design discussion or revisions before merging.

## Reporting bugs

Please include the version or commit, platform, steps to reproduce, expected behavior, actual behavior, and relevant logs or screenshots. A small reproduction or failing test is especially helpful for game-rule issues.
