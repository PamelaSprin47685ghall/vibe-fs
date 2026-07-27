# Changelog

## 0.4.0-rc.1 — development only

This version is **not a release candidate for distribution**. It remains an internal development marker while release-blocking semantics are being verified.

Current blockers include provider-attempt-level A/A/B/B fallback control, real OpenCode E2E evidence, crash recovery coverage, and a clean-package installation check.

## Distribution policy

The package remains private (`private: true`, `license: UNLICENSED`). It must not be published to the public npm registry. A public release requires an explicit license decision, a matching `LICENSE` file, a non-private build manifest, and the release gate documented in `docs/E2E_RELEASE_GUIDE.md`.
