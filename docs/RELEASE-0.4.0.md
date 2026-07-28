# 0.4.0 final release

| Field | Value |
|---|---|
| Version | `0.4.0` |
| Commit | `7f9a46cc83b2de9505cc2326e15be65e561480e9` |
| Date | 2026-07-28 |
| Status | Final release on this track |
| Package | `private: true`, license `SEE LICENSE IN LICENSE` |

Evidence: `docs/evidence/0.4.0/`

## Second clean-checkout gate (required by PR12)

| Gate | Result |
|---|---|
| `git clean -xfd && npm ci` | pass |
| `npm run build` | pass |
| `tests-next` | 276 passed / 0 failed |
| `test:manager-tools` | pass |
| `gate-testkit` | 29 passed |
| `CANARY_REPEAT=3` (17 canaries) | pass |
| `npm pack ./build` | `wanxiangshu-0.4.0.tgz` |
| empty-dir install + `import('wanxiangshu')` | pass |
| tarball sha256 | `d8af81a3b1c4caaefcea113bc52485f3e19dfc22de0ee45931ea2254e2a40f00` |

## Roadmap coverage

PR0–PR12 critical path completed:

1. Prompt Authority SSOT
2. Typed completion / join JSON
3. Manager full loop
4. Companion ActiveLogicalRun eligibility
5. Review physical confirmation
6. Fallback Logical-Run epoch
7. Process/PTY
8. Host restart recovery
9. Orchestrator crash-safe publish
10. P0 staggered canary forest
11. RC.3 clean package gate
12. Final 0.4.0 clean package gate

## Non-blocking follow-ups

- Optional real-provider request traces for same-run A/A/B/B
- External commercial distribution agreement if publishing outside private license
