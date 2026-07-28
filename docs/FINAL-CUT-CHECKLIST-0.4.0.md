# Final 0.4.0 Cut Checklist

Use only after RC observation exit criteria pass.

## Pre-cut

- [x] Scope freeze unchanged (`docs/SCOPE-0.4.0-FREEZE.md`)
- [x] Sealed RC evidence present (`docs/evidence/0.4.0-rc.7/`)
- [x] Observation exit satisfied (`docs/evidence/0.4.0-rc.7/OBSERVATION-EXIT.md`)
- [x] Provider-visible A/A/B/B direct evidence attached (`provider-aabb-trace.txt`)
- [x] Private distribution policy still desired (`private: true`)

## Version cut (docs/version only preferred)

- [ ] `package.json` → `0.4.0`
- [ ] `build-package.json` → `0.4.0`
- [ ] `next/package.json` → `0.4.0`
- [ ] No residual `0.4.0-rc.x` in ship path
- [ ] `CHANGELOG.md` final section
- [ ] `MIGRATION.md` / `README.md` / `docs/RELEASE-0.4.0.md`

## Second clean gate on `0.4.0`

```bash
git clean -xfd
npm ci
npm run test:release
npm pack --dry-run
npm pack ./build
# empty-dir install + import
```

## Evidence

- [ ] `docs/evidence/0.4.0/` complete (ENV, COMMIT, canary 3-round, provider-aabb-trace, pack, install, import, sha256, matrix)
- [ ] tag `v0.4.0` points at gate-green commit
- [ ] no post-tag mutation of the same 0.4.0 artifact
