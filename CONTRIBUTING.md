# Contributing to MakaMek

Thank you for helping to improve MakaMek, a cross-platform BattleTech implementation. Small, focused changes are welcome, especially when they include tests and a clear explanation of the player-facing effect.

## Before you start

- Install Git, the .NET 10 SDK and platform workloads for the targets you need.
- Check the existing issues before starting substantial work. For a new feature or a change to game rules, open an issue first so the design can be discussed.
- Fork the repository, create a focused branch, and keep unrelated formatting or refactoring out of the change.

Suggested branch names are `fix/123-short-description`, `feature/123-short-description`, and `docs/short-description`.

### AI coding agents

Using AI coding agents is **allowed and explicitly supported**. The repository already ships the infrastructure for it:

- Root `AGENTS.md` describes the architecture, build/test commands, testing conventions, and versioning rules agents must follow; scoped `AGENTS.md` files exist where necessary (e.g. `src/MakaMek.Avalonia/AGENTS.md` for Avalonia UI work).
- Repo-local agent skills live in `skills/` and are installed to `.agents/skills` and `.claude/skills` via `mise run install-skills`.
- The [Serena](https://github.com/oraios/serena) MCP server provides symbol-level code search and editing; install it with `mise run install-serena` (or update with `mise run update-serena`).

Whether you contribute by hand or with an agent, the same rules apply.

### Development dependencies with mise

Tool and dev-workflow dependencies are managed with [mise](https://mise.jdx.dev/), configured in `mise.toml` at the repository root:

- The `[tools]` section pins exact versions of CLI tools the project relies on (`gh`, `pulumi`, `uv`) so every contributor gets the same environment. Run `mise install` after cloning to install them.
- The `[tasks.*]` sections define project bootstrap tasks, which are the preferred way to set up the agent/dev tooling:
  - `mise run install-skills` — installs all agent skills (local + third-party) into `.agents/skills` using the skills.sh CLI.
  - `mise run install-serena` — installs the Serena MCP server (via `uv tool install`).
  - `mise run update-serena` — updates the Serena MCP server.
  - `mise run install-devtools` — installs/updates the Avalonia Developer Tools global dotnet tool (`avdt`) required for F12 DevTools in the desktop app.

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

### Code coverage

Coverage uses [Coverlet](https://github.com/coverlet-coverage/coverlet) (MSBuild integration) plus [Cocodif](https://github.com/anton-makarevich/Cocodif), a diff-coverage tool that reports coverage only of the lines your change touches. This mirrors the CI pipeline, which collects an OpenCover report per module, computes diff coverage against the PR base branch, and posts a per-file report as a PR comment.

To run coverage locally (replace the project path and assembly name for other modules):

```bash
dotnet test tests/MakaMek.Core.Tests/MakaMek.Core.Tests.csproj \
  /p:CollectCoverage=true /p:CoverletOutputFormat=opencover \
  /p:ExcludeByAttribute=GeneratedCodeAttribute /p:Include=[Sanet.MakaMek.Core]*
```

The include filter uses the *source* assembly name (`Sanet.MakaMek.Core`), derived from the test assembly name (`Sanet.MakaMek.Core.Tests`) by dropping the `.Tests` suffix. Code marked with the `GeneratedCodeAttribute` (e.g. source-generator output) is excluded; UI (Avalonia) is intentionally outside coverage.

To get the diff-coverage report against your current branch, install the global tool and run:

```bash
dotnet tool install --global Sanet.Cocodif

Cocodif \
  -c tests/MakaMek.Core.Tests/coverage.opencover.xml \
  -o diff-coverage.md \
  --include 'src/MakaMek.Core/**' \
  --exclude '**/obj/**,**/bin/**' \
  --title 'MakaMek Core Coverage'
```

Cocodif parses the OpenCover XML, filters changed files via `git diff --merge-base`, and writes `diff-coverage.md` with a per-file breakdown. The CI action (`cocodif`) embeds the same output as a sticky PR comment per module. The process (and how an agent runs it) is documented in the `coverage-check` skill under `skills/coverage-check` — agents installed there can run the whole flow automatically when asked to "run coverage".

## Project boundaries

The normal dependency direction is `Avalonia → Presentation → Core`. Core contains the authoritative game rules and should not depend on UI code. Commands are the normal mechanism for changing game state and for communicating changes between server and client.

When adding command, component, movement-cost, roll-modifier, or piloting-resolution types, follow the existing source-generator conventions; generated registries should not be hand-maintained. Consult the relevant architecture documents under `docs/architecture/` and use `docs/INDEX.md` to find other project guidance.

Do not modify or commit derived game art in `data/`. That content is distributed separately under its upstream license.

## Commit messages

Use the [Conventional Commits](https://www.conventionalcommits.org/) format: `<type>(<optional scope>): <short description>`, for example:

- `feat(movement): allow skid damage on pavement`
- `fix(combat): apply critical hit chance correctly for rear arcs`
- `docs: update coverage instructions`
- `refactor(presentation): extract WeaponsAttackStep logic`

Keep the subject line concise (ideally under 72 characters), use the imperative mood ("add", not "added" or "adds"), and do not end it with a period. Common types are `feat`, `fix`, `docs`, `refactor`, `chore`, and `build`. If the change is not self-explanatory, add a body explaining the why, and reference the issue (`Refs #123` or `Fixes #123`) so it is linked automatically.

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

## Code of Conduct

By participating in this project you agree to keep discussions respectful, constructive, and welcoming.

## License and ownership of contributions

MakaMek is licensed under the [GPL-3.0](LICENSE). Unless you explicitly state otherwise, any contribution intentionally submitted for inclusion in the work (via pull request or patch) shall be licensed under GPL-3.0 as well, without any additional terms or conditions. Submitted code must be your own work or compatible with GPL-3.0 (please quote and attribute anything derived from reference implementations such as MegaMek, etc).
