# Architecture Rules

## Project Structure

```
AICleverness_private/
├── AiClevernessLib/          (main library — NuGet package)
├── AiClevernessLib.Demo/     (demo console app)
├── AiClevernessLib.Tests/    (unit tests)
└── AiCleverness.slnx
```

## Layer Dependency Direction (strict one-way)

```
AiClevernessLib.Demo   →  AiClevernessLib  ←  AiClevernessLib.Tests
                             (library)
```

- **AiClevernessLib** — abstractions, models, service implementations, search providers, browser wrappers. This is the published NuGet package.
- **AiClevernessLib.Demo** — references AiClevernessLib. Demonstrates all library features. Not published.
- **AiClevernessLib.Tests** — references AiClevernessLib. Unit tests only. Not published.

## Forbidden in AiClevernessLib.Demo

- ❌ Production logic — demo is for demonstration only
- ❌ Test assertions — tests belong in AiClevernessLib.Tests

## Forbidden in AiClevernessLib.Tests

- ❌ References to AiClevernessLib.Demo (no dependency)
- ❌ Live network calls in unit tests — use `FakeHttpMessageHandler` or NSubstitute mocks
- ✅ Integration tests that hit the network must be in a separate category/trait

## Forbidden in AiClevernessLib (library)

- ❌ References to Demo or Tests projects (circular dependency)
- ❌ Console.WriteLine or any UI output
- ❌ Hard-coded secrets or API keys

## One type per file (MANDATORY)

- ❌ NEVER put more than one type (class, record, struct, interface, enum) in a single `.cs` file
- ✅ Each type gets its own file, named after the type

## Security — test data rules (MANDATORY)

- ❌ NEVER use real hostnames, usernames, passwords, or API keys in tests or documentation
- ❌ NEVER connect to a live service in unit tests — tests must be hermetic
- ✅ Use obviously fake values: `https://test.example.com`, `fake-api-key`
- ✅ Mock HTTP responses with `FakeHttpMessageHandler` or NSubstitute

## Test conventions

- AAA pattern (Arrange / Act / Assert)
- xUnit as test framework
- FluentAssertions for assertions
- NSubstitute for mocking interfaces
- One test class per production class, named `{ClassName}Tests`

## Code review validation

- A full solution build is **not required** for a code review.
- Prefer source inspection and targeted tests for the changed behavior.
- Run only checks that provide evidence about the requested behavior or inspected files; do not perform unrelated validation as a ritual.
- Do not run documentation builds, `git diff --check`, benchmarks, or broad test/build commands unless the review scope or changed files make them relevant, or the user explicitly requests them.
- Build only the affected project or dependency chain when compilation evidence is relevant; run the full solution build only when the review scope requires it or the user explicitly requests it.

## CHANGELOG maintenance (release workflow)

`CHANGELOG.md` follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) format.

### Rule: packages without a first release

- While the package has never been released (CHANGELOG contains no versioned sections, only `[Unreleased]`), do NOT update `CHANGELOG.md` for code changes — there are no released users to inform. Changelog content is written only when a release is prepared.

### Format rules

- Top-level heading: `# Changelog`
- Each release is a level-2 heading: `## [X.Y.Z] - YYYY-MM-DD`
- Work-in-progress lives under: `## [Unreleased]`
- Change categories (level-3 headings): `Added`, `Changed`, `Deprecated`, `Removed`, `Fixed`, `Security`
- Bottom of the file has reference links: `[X.Y.Z]: https://github.com/AlexNek/AICleverness/releases/tag/vX.Y.Z`

### Algorithm: when making code changes

The `[Unreleased]` section must always describe the **final user-facing state relative to the last released version** — never the development history.

1. **Every user-visible change** (feature, fix, breaking change) must be covered under `## [Unreleased]` in the appropriate category
2. **New feature in development** — maintain ONE bullet per feature that describes its complete current state; do NOT add a new bullet per incremental change
3. **Change to an already-released behavior** — one bullet per issue; if revisited, update the existing bullet
4. **Fix/change that only concerns an unreleased feature** — fold it into the feature's bullet; never add a separate `Fixed`/`Changed` entry
5. Keep bullets concise but descriptive enough for end users
6. Do NOT modify existing versioned sections unless explicitly asked

### Algorithm: preparing a release (when asked to tag/release version X.Y.Z)

1. Rename `## [Unreleased]` → `## [X.Y.Z] - YYYY-MM-DD` (use actual date)
2. Add a new empty `## [Unreleased]` section above it
3. Update the reference links at the bottom:
   - Change `[Unreleased]` link to compare `vX.Y.Z...HEAD`
   - Add `[X.Y.Z]: https://github.com/AlexNek/AICleverness/releases/tag/vX.Y.Z`
4. The release workflow (`.github/workflows/release.yml`) will automatically:
   - Build, test, and pack the NuGet package
   - Publish to NuGet.org
   - Create a GitHub Release with auto-generated release notes

### Important

- CHANGELOG.md must exist in the repo root (the release workflow reads it at checkout path)

## Edit discipline (MANDATORY)

- Make surgical edits only: change exactly what the task requires.
- Never refactor, "simplify", or restructure adjacent working code unless explicitly asked.
- If a change seems to require touching unrelated structure, stop and ask first.

## Planning and issue-document immutability (MANDATORY)

- ❌ Do not modify `github-issue.md`, feature specifications, implementation plans, or other task-definition documents after implementation begins or completes unless the user explicitly requests that specific document change in the current task.
- ✅ Treat those documents as historical requirements and planning records. Do not rewrite them to reflect implementation status, test results, or later design decisions without a clear task.

## Git discipline (MANDATORY)

- ❌ NEVER run `git commit`, `git push`, `git tag`, or any history-rewriting command — commits are made by the user only, with the user's own commit text
- ✅ Leave finished changes in the working tree for the user to review, stage, and commit
- ✅ You may suggest commit message text when asked, but never execute the commit yourself
