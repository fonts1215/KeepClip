# Windows CI and packaging

The user selected Windows x64, ARM64 and x86 where supported. Keep the existing WinUI application and its current default x64 build. Produce self-contained portable ZIP and unsigned MSIX packages per architecture through GitHub Actions on pushes, pull requests and manual runs. Run application and Windows tests before packaging; verify package integrity and publish SHA256 checksums with artifacts. Signing remains available locally using the existing certificate parameter.

Approaches: Windows architecture builds extend the existing implementation and were selected by the user. macOS/Linux would require a separate UI and clipboard port and are outside this work. Keep packages as Actions artifacts; publishing GitHub Releases is not required.

Parameterize architecture consistently in the project, packaging scripts and generated manifests; reject mislabeled executable payloads. Correct the native INPUT union layout for 32-bit processes and test both pointer widths. Do not launch KeepClip in CI or access the user's clipboard. Existing manual compatibility limits still apply.

The repository is now origin https://github.com/gdetra/KeepClip.git. Create main from the existing published history without rewriting any remote branch. Verify remote state before creating it. Work in the current checkout; preserve unrelated changes.
