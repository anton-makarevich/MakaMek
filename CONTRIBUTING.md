# Contributing to MakaMek

Thank you for helping to improve MakaMek, a cross-platform BattleTech implementation. Small, focused changes are welcome, especially when they include tests and a clear explanation of the player-facing effect.

## Before you start

- Install GIT, .NET 10 SDK and platform workloads for the targets you need.
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

## Testing requirements

Testing is **not optional**. All testable code must be covered by unit tests, and tests must actually exercise the logic — a test that passes without verifying meaningful behavior does not count as coverage.

- Every bug fix needs a regression test that fails without the fix.
- Every new feature or behavior change needs tests covering the new logic, including edge cases (boundary values, invalid input, failure paths).
- Tests should verify outcomes, not implementation details: assert on results and state changes rather than mocking so much that the test only re-states the code under test.
- Prefer a few focused tests per behavior over one large test; each test should have a clear arrange–act–assert structure and a descriptive name explaining the scenario and expectation.

Test-Driven Development (TDD) is recommended, especially for bugfixes: write a failing test first, implement the minimal code to pass it, then refactor. This is the fastest way to confirm the behavior is correct and stays correct.

When changing code, add or update tests in the corresponding test project. Core tests use xUnit, Shouldly, and NSubstitute. Keep presentation logic in `MakaMek.Presentation` so it can be tested without an Avalonia view — logic that lives in a view layer is by definition hard to test, so avoid putting it there.

If you are unsure whether something is testable, ask in the PR — but "it's hard to test" is not an acceptable reason to ship untested logic; it usually means the code should be restructured (e.g. extracted from a view into a ViewModel or service).

## Project boundaries

The normal dependency direction is `Avalonia → Presentation → Core`. Core contains the authoritative game rules and should not depend on UI code. Commands are the normal mechanism for changing game state and for communicating changes between server and client.

When adding command, component, movement-cost, roll-modifier, or piloting-resolution types, follow the existing source-generator conventions; generated registries should not be hand-maintained. Consult the relevant architecture documents under `docs/architecture/` and use `docs/INDEX.md` to find other project guidance.

Do not modify or commit derived game art in `data/`. That content is distributed separately under its upstream license.

## Pull requests

Before opening a pull request:

- Link the issue or explain why the change is needed.
- Describe the behavior changed and any compatibility or rules implications.
- Include regression tests for bugs and tests for new behavior (see [Testing requirements](#testing-requirements)).
- Run the relevant tests, and include the commands used in the PR description.
- If any file under `src/` changed, increment only the patch segment of `VersionPrefix` in `Directory.Build.props`. Increment it once per PR.
- Keep the PR focused and update documentation when behavior or public guidance changes.

Pull requests should be ready for review, build cleanly in CI, and make it easy to verify the intended behavior. Maintainers may request design discussion or revisions before merging.

## Reporting bugs

Please include the version or commit, platform, steps to reproduce, expected behavior, actual behavior, and relevant logs or screenshots. A small reproduction or failing test is especially helpful for game-rule issues.
