# 0.4.0-rc.5 Release Matrix

| Gate | Result |
|---|---|
| `git clean -xfd && npm ci` | pass |
| `npm run build` | pass |
| `tests-next` | 277 passed / 0 failed |
| `test:manager-tools` | pass |
| `gate-testkit` | 29 passed |
| `CANARY_REPEAT=3` (17 canaries) | pass |
| `npm pack ./build` | pass (`wanxiangshu-0.4.0-rc.5.tgz`) |
| empty-dir install + `import('wanxiangshu')` | pass |
| package version / private / LICENSE / prompts / Plugin entry | pass |

## Policy locked at freeze

- Scope freeze: `docs/SCOPE-0.4.0-FREEZE.md`
- Provider-visible same-run A/A/B/B is **blocking for final 0.4.0**
- Default distribution: **private** tarball under provisional commercial LICENSE
- This RC seal does not claim final 0.4.0

## Harness fixes included after freeze (no production product code)

- Process timeout headroom for dual-script canaries (90s)
- Intermediate after-tool / journal fact waits for dual PERFECT publish chains
- Reusable expectation claim-count waits (one match per wait)
