# Windows CI Implementation Plan

**Goal:** Build and package KeepClip for Windows x64, ARM64 and x86, and create main on the personal GitHub repository.

**Architecture:** Architecture parameters flow from the Actions matrix through the app project and PowerShell packaging scripts. Tests gate packaging; each architecture gets separate self-contained ZIP/MSIX artifacts and checksums.

**Tech Stack:** .NET 10, WinUI 3, PowerShell, GitHub Actions.

**Spec:** docs/superpowers/specs/2026-11-07-windows-ci-design.md

## Constraints and review focus

- Preserve the default x64 behavior and existing history.
- Test INPUT layout in x64 and x86 processes; ARM64 execution requires an ARM64 host.
- Reject a publish directory whose PE machine does not match the requested architecture.
- Keep ZIP payload independent of the MSIX-generated AppxManifest.xml.
- Fail CI when a build, packaging operation or expected artifact is missing.
- CI produces unsigned MSIX; installation requires trusted signing.

## Tasks

- [x] Create main locally from the current published commit; remote creation pending authentication.
- [x] Add architecture-aware packaging regression tests and native ABI assertions; observe the relevant failures.
- [x] Parameterize app/project and packaging for x64, ARM64, x86; fix native INPUT layout.
- [x] Add Actions tests and packaging matrix, upload test reports and per-architecture artifacts.
- [x] Document build commands and artifact/signing behavior; verify tests, packages, XML and workflow validation.
- [x] Independent read-only review: no actionable defects.
- [ ] Commit the changes and push; inspect Actions when remote access permits.

## Verification evidence

- Baseline: 165 application tests and 8 Windows tests passed.
- RED: x86 INPUT layout test expected 28 bytes and observed 36; architecture-aware package test rejected the previously unavailable Architecture parameter.
- GREEN: 165 application tests and 8 Windows tests passed in each of x64 and x86 processes. All eight package checks passed for each of x64, arm64 and x86.
- Locked solution restore succeeded. Release publish succeeded for x64, ARM64 and x86. Native build SDK discovery required execution outside the restricted registry sandbox.
- Portable ZIP and unsigned MSIX generated for all three architectures. Independently opened each ZIP and verified every manifest entry's length and SHA256; confirmed no AppxManifest.xml was included. Opened each MSIX and checked its manifest architecture. Generated package SHA256SUMS.txt files.
- PowerShell/XML parsing and actionlint workflow validation succeeded; git diff --check passed.
- Initial remote HEAD was refs/heads/feat/clipboard-mvp at 47dfe3fb856145b4b7a7e7d4ed3407f1c92ceb94; main did not exist. Initial push failed because GitHub authentication was cancelled.
- Review rulings: ARM64 execution remains outside local hardware coverage; executable-only architecture validation follows the chosen contract. MSBuild platform conditions are case insensitive, so arm64/ARM64 names select the same runtime.
