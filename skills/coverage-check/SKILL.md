---
name: coverage-check
description: Run tests for a project with code coverage and report uncovered source lines. Use when the user asks about test coverage, wants to know which lines are uncovered, asks to check coverage for a project, or says "run coverage" or "what's not covered".
---

# Coverage Check

Run tests with coverlet coverage, parse the opencover XML, and report uncovered lines — mirroring the CI pipeline locally.

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

## Step 4: Parse the coverage XML

Locate `{testProjectDir}/coverage.opencover.xml`. Parse the XML:

1. Build a file map from `<Files>` — each `<File uid="N" fullPath="..." />` maps a numeric ID to a source file path.
2. For every `<SequencePoint>` with `vc="0"` (zero visits), record the file ID (`fileid` attribute) and start line (`sl` attribute).
3. Group uncovered lines by source file.
4. Filter to source files under `src/` only — skip generated files under `obj/`.

**Completion criterion:** a map of source file → sorted list of uncovered line numbers extracted.

## Step 5: Report uncovered lines

For each file with uncovered lines:

1. Show the file path (relative to repo root) and the count of uncovered lines.
2. List the uncovered line numbers.
3. Read those lines from the source file to show the actual code.

End with a summary: total source files checked, files with gaps, total uncovered lines.

**Completion criterion:** every uncovered line presented with its source code, grouped by file.
