# ObexNet — Work State

## Resume

Build, versioning and CI/CD infrastructure follow the house pattern (reference:
ClaudeForge, trimmed to what a Windows-only console tool needs). The GitHub repository
now exists at https://github.com/JanusMael/ObexNet — the owner/repo the README badges and
release links already targeted, so they are correct as-is and will light up on the first push.

The publish path has now been executed rather than only configured. It previously failed
outright — `JsonSerializer.Serialize` over anonymous types raised `IL2026`, which
warnings-as-errors turned into a build error — and the test suite could not run at all,
because `AutoVersioning` stamps `AssemblyProduct` as `"ObexNet v<version>"` and
`AssemblyUtility.ApplicationName` derived the executable's own name from it. Both are fixed and
observed: `publish/publish.ps1 -Rids win-x64` produces a one-file archive with zero ILLink
diagnostics, and all 6 tests pass against that published trimmed executable as well as against
the ordinary build output.

## Build & release infrastructure

| Area | Where | State |
|------|-------|-------|
| Shared MSBuild settings | `Directory.Build.props` | TFM `net10.0`, nullable, implicit usings, warnings-as-errors, Debug portable / Release embedded PDBs, AutoVersioning generator — hoisted out of the three csproj files |
| SDK pin | `global.json` | `10.0.100` floor, `rollForward: latestFeature` (same policy as the sibling repositories) |
| Publish hygiene | `Directory.Build.targets` | Strips `*.xml` / `*.pdb` from publish output |
| Versioning | `Directory.Build.props` | `Bennewitz.Ninja.AutoVersioning` 2026.3.819; `PublicVersion` comes from the release tag (CI) or `publish.ps1 -Version` (local) |
| Publish shape | `ObexNet/ObexNet.csproj` | Self-contained, single-file, trimmed; gated on `_IsPublishing` so `dotnet build -c Release` keeps the RID-less output path the test fixture expects. Verified: 11.4 MB, one file per archive |
| Trim safety | `ObexNet/JsonEvents.cs` | `--json` payloads are named records behind a source-generated `JsonSerializerContext`; the reflection-based serializer overload is `IL2026` and cannot be used while `PublishTrimmed` is on |
| App name | `ObexNet/AssemblyUtility.cs` | `ApplicationName` reads the simple assembly name. `AssemblyProductAttribute` is unusable for this: AutoVersioning always appends ` v<version>` and offers no switch to suppress it |
| Publish script | `publish/publish.ps1` | Canonical entry point for CI and local runs; produces `dist/ObexNet-<rid>.zip`. Scans publish logs for `warning IL…` **and** `error IL…` — warnings-as-errors means the warning-only form never matched |
| Binary under test | `Tests/ObexNet.Tests/TestFixture.cs` | `OBEXNET_EXE` overrides which executable the suite drives; unset, it uses the ordinary build output as before |
| CI | `.github/workflows/ci.yml` | Build + test on `windows-latest` (TRX artifacts, 14 days); `publish-check` publishes, then re-runs the whole suite against the unpacked trimmed executable, and uploads both the TRX and the zip |
| Release | `.github/workflows/release.yml` | `v*.*.*` tag → test → publish win-x64 + win-arm64 → **suite re-run against the unpacked win-x64 binary** → GitHub Release with download table and changelog. The gate sits before the artefact upload and `release` needs `publish-windows`, so a binary that fails it never reaches a Release |
| Dependency updates | `.github/dependabot.yml` | NuGet (`MSTest*` grouped) + GitHub Actions, Mondays 09:00 UTC |
| Reference | `.github/WORKFLOWS.md` | How the workflows fit together, versioning, release procedure, local-vs-CI parity |
| Licensing | `LICENSE`, `THIRD-PARTY-NOTICES.md` | ObexNet under MIT (`Copyright (c) 2026 Brian Bennewitz`), matching the other Bennewitz.Ninja repos. It is a port of `dis0rder0x00/obex` (BSD-3-Clause); the upstream notice is retained verbatim and `publish.ps1` bundles both files into every release archive, satisfying BSD-3-Clause clauses 1 and 2 |

## Open items

- Pushed to `main` at `9f0276b` (repo `https://github.com/JanusMael/ObexNet`; badge/release URLs already match). The first CI run is therefore triggered, but its outcome has **not been observed** — verify it on the Actions tab. The trimmed-binary test steps in `publish-check` and `publish-windows` are the least certain part: the suite spawns a target under `DEBUG_PROCESS` and drives `regsvr32`, which has not been tried under GitHub's `windows-latest`. Everything the workflows run was rehearsed locally (publish both RIDs → unpack → test), but never on a runner.
- `win-arm64` publishes clean (verified locally: exit 0, zero ILLink diagnostics, 5.8 MB archive) but has never been *run* — this host is x64 and GitHub offers no Windows ARM64 runner. Only the x64 binary is behaviourally tested, on either machine.
- CodeQL scanning (`codeql.yml` in ClaudeForge) deliberately not carried over; drop-in if wanted.
- No first release cut yet — the Release workflow only fires on a `v*.*.*` tag, so no published binary exists on GitHub. Binaries also ship unsigned (`release.yml` has the `WINDOWS_SIGNING_CERT` scaffolding, no cert wired).
