---
name: coverage-check
description: Run tests for a project with code coverage and report uncovered source lines. Use when the user asks about test coverage, wants to know which lines are uncovered, asks to check coverage for a project, or says "run coverage" or "what's not covered".
---

# Coverage Check

Run tests with coverlet coverage and produce a diff-coverage report — mirroring the CI pipeline locally.

**Input:** test project path relative to the repo root (e.g. `tests/MakaMek.Core.Tests/MakaMek.Core.Tests.csproj`).

## Step 1: Locate and validate the test project

Resolve the test project path. Confirm the `.csproj` file exists. Read it to extract the `<AssemblyName>` value.

**Completion criterion:** test project found and its assembly name extracted.

## Step 2: Derive the source assembly name

Remove the `.Tests` suffix from the test project's assembly name to obtain the source assembly name. Example: `Sanet.MakaMek.Core.Tests` → `Sanet.MakaMek.Core`.

**Completion criterion:** source assembly name derived and ready for the coverage filter.

## Step 3: Run tests with coverage

Execute the test command matching the CI pipeline:

```powershell
dotnet test {testProjectPath} /p:CollectCoverage=true /p:CoverletOutputFormat=opencover /p:ExcludeByAttribute=GeneratedCodeAttribute /p:Include=[{SourceAssemblyName}]*
```

Stop and report if tests fail. Do not proceed to parsing.

**Completion criterion:** tests pass and `coverage.opencover.xml` exists in the test project directory.

## Step 4: Diff-coverage report with Cocodif

[Cocodif](https://github.com/sanet/Cocodif) is a .NET global tool that parses the OpenCover XML, filters changed files via `git diff`, and produces a Markdown diff-coverage report.

**Install** (if not already available):

```bash
dotnet tool install --global Sanet.Cocodif
```

**Run** against the module (after Step 3 produces `coverage.opencover.xml`):

```bash
Cocodif \
  -c tests/{TestProjectDir}/coverage.opencover.xml \
  -o diff-coverage.md \
  --include 'src/{ModuleName}/**' \
  --exclude '**/obj/**,**/bin/**' \
  --title '{ModuleName} Coverage'
```

If Cocodif cannot be installed, skip this step — the `coverage.opencover.xml` is still available for manual inspection.

**Completion criterion:** `diff-coverage.md` produced with per-file diff-coverage breakdown, or step skipped.
