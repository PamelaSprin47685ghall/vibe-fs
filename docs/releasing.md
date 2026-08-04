# Releasing

## Preflight

From a clean Git tree (`git status` empty):

```bash
npm ci
dotnet tool restore
npm run check:release
```

`check:release` runs:

```text
check  →  test:e2e -- --repeat 3  →  test:package  →  npm pack --dry-run
```

where `check` = lint → build → unit → integration.

Do not tag or pack while the working tree is dirty.

## Package

Pack from the repository root. No staging package directory.

```bash
npm pack --pack-destination artifacts/package
```

The tarball contains:

- `dist/`
- `resources/`
- npm metadata automatically included (`package.json`, `README.md`, `LICENSE`)

It must not contain `src/`, `tests/`, `scripts/`, `spec/`, `docs/`, or `artifacts/`.

Release verification logs and pack outputs belong in CI artifacts or release attachments, not in the git tree.

## Version checklist

1. Bump `package.json` `version`
2. Add a user-facing entry in [CHANGELOG.md](../CHANGELOG.md)
3. Confirm `git status` is empty
4. Run `npm ci` → `dotnet tool restore` → `npm run check:release`
5. `npm pack --pack-destination artifacts/package`
6. Tag `vX.Y.Z` only after the above is green

## Non-goals for patch normalization releases

No runtime protocol change, no journal fact rename, no Host patches (ARCH-003).
