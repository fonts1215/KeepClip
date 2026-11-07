# GitVersion integration plan

**Goal:** Version KeepClip assemblies and Windows packages from Git using the same pinned GitVersion tool locally and in CI.

**Architecture:** GitHubFlow on main, initial next version 0.2.0, ci prereleases and explicit v-prefixed release tags. A single version job supplies immutable metadata to all architecture jobs. Publish scripts stamp assembly properties and persist metadata beside the executable; packaging uses that metadata rather than recalculating from a potentially different checkout.

**Tech Stack:** GitVersion.Tool 6.8.2, PowerShell, MSBuild, GitHub Actions.

## Constraints

- Preserve locked dependency restore and existing Windows architecture coverage.
- Full Git history and tags are required for version calculation.
- Validate four-part numeric versions and component bounds before MSIX packaging. MSIX uses the weighted prerelease component as its fourth field for sideloaded builds; a future Microsoft Store submission needs a separate revision-zero policy.
- Never silently label an old publish directory with the current checkout's version.
- Only version calculation needs .NET 8 for the pinned GitVersion tool; app builds keep the SDK from global.json.
- No tags or GitHub Releases are created automatically.

## Tasks

- [x] Pin tool and GitHubFlow configuration; exercise actual GitVersion on current history and isolated tag/branch fixtures.
- [x] Test metadata validation (numeric bounds, malformed SemVer, missing metadata, assembly mismatch) before implementation.
- [x] Add common metadata validation, version calculation and versioned publish scripts; use metadata for ZIP/MSIX filenames and MSIX identity version.
- [x] Calculate version once in CI; pass metadata to all package jobs and retain checkout history and tags.
- [x] Document release tags, semver bump messages, local build commands and MSIX version policy.
- [x] Verify scripts, real publish/package contents, existing tests and actionlint; independent review found no actionable defects.
- [ ] Commit and publish to GitHub when authentication permits.

## Verification evidence

- Actual GitVersion 6.8.2 calculated 0.2.0-ci.28 / numeric 0.2.0.55028 from the pre-change HEAD; no real repository tags created.
- Five isolated history checks passed, including increasing development versions, v0.2.0 mapping to numeric 0.2.0.60000, the next patch and feature labels.
- Twelve metadata checks passed; the contradictory valid SemVer case was observed failing before adding cross-field validation.
- Versioned Release publish and executable resource verification succeeded for x64, ARM64 and x86, with the same SemVer and SHA.
- ZIP/MSIX packages created for all three architectures. Inspected each ZIP's metadata and each MSIX's identity version and architecture. ZIPs contain no MSIX manifest.
- Existing suites passed: 165 application and 8 Windows tests on each of x64/x86, 346 total. The sandbox run stalled and was interrupted; the outside-sandbox run completed successfully.
- All 24 portable integrity checks passed; actionlint and PowerShell parsing passed.
- Rulings from review: older publishes without version metadata require republishing; Microsoft Store revision-zero policy and automatic GitHub Releases remain outside this scope; direct IDE builds keep default SDK versions as documented.
