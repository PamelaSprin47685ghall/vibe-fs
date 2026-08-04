# Releasing

## Preflight

```bash
npm run lint
npm run check:release
```

`check:release` = `gate:static` → `build` → unit → harness → e2e × 3 → `npm pack --dry-run`.

If e2e × 3 is too long for a dry run: `npm run test:e2e` once, then full three-round before tag.

## Package

```bash
npm run build
npm pack
```

Tarball should include `dist/`, `resources/`, and package metadata only — not `tests/`, `spec/`, or `scripts/`.

## Version checklist

1. Bump `package.json` `version`
2. [CHANGELOG.md](../CHANGELOG.md) user-facing entry
3. Tag `vX.Y.Z` after green `check:release`
4. Attach tarball / CI artifacts outside the git tree when needed

## Non-goals for patch normalization releases

No runtime protocol change, no journal fact rename, no Host patches (ARCH-003).
