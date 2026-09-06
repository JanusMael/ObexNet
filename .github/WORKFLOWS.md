# GitHub Actions — Workflow Reference

This document describes every automated workflow in this repository, how they
interrelate, and how to use them day-to-day.

The release and publish-check paths both delegate to **`publish/publish.ps1`** —
the same script developers run locally (`pwsh publish/publish.ps1`). Keeping a
single canonical entry point means a green CI publish and a green local publish
exercise the same code, with the same per-RID publish, trim-warning scan, and
archive creation.

---

## Workflow map

```
 any branch push  ──────────────────────────────────────────────────────────────
   │                                                                            │
   ├─▶ CI › build-and-test   windows   restore → Debug build → test (TRX)       │
   └─▶ CI › publish-check    windows   publish.ps1 -Rids win-x64 → zip artifact │
                                                                                │
 any PR  ──────────────────────────────────────────────────────────────────────┘
   (same two jobs as branch push above; concurrency group cancels stale runs)

 v*.*.* tag push  ──────────────────────────────────────────────────────────────
   │
   └─▶ Release › test                windows   fail-fast: true
         │  (must pass)
         └─▶ Release › publish-windows     publish.ps1 -Rids win-x64,win-arm64
               │
               └─▶ Release › release        create GitHub Release + upload archives

 Monday 09:00 UTC  ─────────────────────────────────────────────────────────────
   └─▶ Dependabot            NuGet + GitHub Actions version checks → open PRs
```

Every build job runs on Windows: ObexNet drives the Win32 debugging API and its
integration tests spawn `ObexNet.exe` against `regsvr32`, so no other host can
build *and* test it. The `release` job alone runs on Ubuntu because it only
downloads artifacts and calls `gh`.

---

## Workflows

### `workflows/ci.yml` — Continuous Integration

**Triggers:** push to any branch; any pull request
**Concurrency:** `ci-${{ github.ref }}` — cancels a stale run the moment a new
commit lands on the same branch or PR.

| Job | Runner | What it does |
|-----|--------|-------------|
| `build-and-test` | windows | Restore → Debug build → test with TRX results uploaded as artifacts (14-day retention). |
| `publish-check` | windows | Invokes `pwsh publish/publish.ps1 -Rids win-x64`. Same entry point the release path uses, so a broken self-contained / trimmed publish is caught on every push. The resulting `ObexNet-win-x64.zip` is kept as a 7-day artifact, so every commit yields a downloadable binary for ad-hoc testing. |

The CI badge in the README reflects the status of the most recent run on the
default branch.

---

### `workflows/release.yml` — Release

**Trigger:** push of a tag matching `v*.*.*` (e.g. `v1.2.3`, `v1.2.3-rc.1`)
**Permissions:** `contents: write` (create release + upload assets),
`pull-requests: read` (read PR body for release notes)

#### Job graph

```
test (windows, fail-fast) ─── PASS ───▶ publish-windows ───▶ release
```

| Job | Runner | RIDs |
|-----|--------|------|
| `publish-windows` | windows-latest | win-x64, win-arm64 |

Both RIDs are produced by one job; the win-arm64 runtime pack cross-publishes
from the x64 runner.

#### Versioning

`publish-windows` exports `PublicVersion=<tag without the leading v>` into the
job environment. MSBuild surfaces environment variables as properties, and the
`Bennewitz.Ninja.AutoVersioning` source generator (wired in
`Directory.Build.props`) reads `PublicVersion`, `CommitSha` and
`IsContinuousIntegration` to stamp the assembly's version attributes at compile
time. No `<Version>` element is maintained by hand anywhere in the repository.

Local runs of `pwsh publish/publish.ps1` without `-Version` get the generator's
date-based build version; pass `-Version 1.2.3` to reproduce a tagged build.

Archive filenames are unversioned (`ObexNet-<rid>.zip`) so local dev builds and
CI builds share one filename pattern — the release tag carries the version.

#### Pre-release detection

Any tag containing a hyphen (`v1.2.3-rc.1`, `v2.0.0-beta.2`) is published as a
pre-release on the Releases page. A plain `v1.2.3` tag creates a full release.

#### Release notes strategy

The `release` job determines the changelog content in priority order:

| Scenario | Source |
|----------|--------|
| Tag's commit is the merge commit of a PR | **PR body** — the author-written description becomes the changelog. Write your changelog in the PR description. |
| Tag is on a direct-push commit with a multi-line message | **Commit message body** — everything after the first blank line. Write changelog markdown directly in the commit message. |
| Neither the PR body nor the commit message has content | **`--generate-notes` fallback** — GitHub auto-generates a categorised list of merged PRs and commits since the previous tag. |

The download table and install note are always prepended regardless of which
source is used.

#### Triggering a release

```bash
# 1. Merge your PR (or push directly with a detailed commit message).

# 2. Tag the resulting commit:
git tag v1.2.3
git push --tags

# Pre-release:
git tag v1.2.3-rc.1
git push --tags
```

The workflow fires automatically, runs the tests, builds both archives, and
publishes a GitHub Release with the download table and changelog attached.

---

## Supporting configuration

### `dependabot.yml` — Dependency updates

Dependabot opens pull requests every **Monday 09:00 UTC** for outdated
dependencies. Packages are grouped so related updates land in a single PR:

| Group | Patterns | Update types |
|-------|----------|-------------|
| `mstest` | `MSTest*` | minor, patch |
| `github-actions` | `*` (Actions ecosystem) | minor, patch |

The `Bennewitz.Ninja.AutoVersioning` reference in `Directory.Build.props` is
tracked too; it is ungrouped, so its bumps arrive as individual PRs.

Dependabot PRs are labelled `dependencies` and excluded from the release
changelog by `.github/release.yml`.

### `.github/release.yml` — Release notes categorisation

When the `--generate-notes` fallback is used, GitHub reads `.github/release.yml`
to bucket merged PRs into sections based on their labels.

| Label on the PR | Section in release notes |
|-----------------|--------------------------|
| `breaking-change` | ⚠️ Breaking Changes |
| `enhancement`, `feature` | 🎉 New Features |
| `bug`, `fix` | 🐛 Bug Fixes |
| `performance` | ⚡ Performance |
| `security` | 🔒 Security |
| `documentation`, `docs` | 📝 Documentation |
| `chore`, `refactor`, `ci`, `build` | 🔧 Maintenance |
| `dependencies` | *(excluded — Dependabot bumps)* |
| `ignore-for-release` | *(excluded — internal maintenance)* |
| *(no matching label)* | Other Changes |

---

## Secrets and variables reference

All secrets and variables are set under **Settings → Secrets and variables →
Actions** on the repository.

### Repository secrets

| Secret | Used in | Purpose |
|--------|---------|---------|
| `WINDOWS_SIGNING_CERT` | `release.yml` | Base64-encoded `.pfx` Authenticode certificate |
| `WINDOWS_CERT_PASSWORD` | `release.yml` | Password for the `.pfx` certificate |

Both secrets are **placeholders** — the workflow ships without a signing step
wired up. Add the step body as documented in the comment block at the top of
`release.yml` when you're ready to sign; until then the secrets can stay unset.

### Repository variables

| Variable | Used in | Default | Purpose |
|----------|---------|---------|---------|
| `DOTNET_VERSION` | `ci.yml`, `release.yml` | `'10.x'` | .NET SDK version for all `setup-dotnet` steps |
| `APP_NAME` | `release.yml` | `ObexNet` | Release title prefix + download-table heading. Archive filenames are produced by `publish.ps1` from the project name — changing `APP_NAME` only affects the visible release metadata. |

Variables are optional — hardcoded defaults are used when a variable is not
defined. See the comment block near the top of each workflow for the exact
`${{ vars.NAME }}` substitution syntax.

---

## Local-vs-CI parity

A developer can reproduce exactly what CI does:

```bash
# Reproduces the publish-check job (CI windows runner):
pwsh publish/publish.ps1 -Rids win-x64

# Reproduces the publish-windows job for tag v1.2.3:
pwsh publish/publish.ps1 -Rids win-x64,win-arm64 -Version 1.2.3
```

When CI fails and a local run passes (or vice versa), the divergence is almost
always one of: a stale `bin/obj` (`-Clean` wipes them), or a `$env:PublicVersion`
left over in the shell environment from an earlier experiment.

---

## Badge reference

The badges at the top of the README use these URLs:

```markdown
[![CI](https://github.com/JanusMael/ObexNet/actions/workflows/ci.yml/badge.svg)](https://github.com/JanusMael/ObexNet/actions/workflows/ci.yml)
[![Release](https://github.com/JanusMael/ObexNet/actions/workflows/release.yml/badge.svg)](https://github.com/JanusMael/ObexNet/actions/workflows/release.yml)
[![Latest Release](https://img.shields.io/github/v/release/JanusMael/ObexNet?label=latest)](https://github.com/JanusMael/ObexNet/releases/latest)
```

The CI badge reflects the status of the most recent run on the **default
branch** (not on feature branches). The Release badge reflects the most recent
tag-triggered run.
