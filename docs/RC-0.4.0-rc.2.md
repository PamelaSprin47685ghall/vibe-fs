# 0.4.0-rc.2 — Remediation RC (release gate green)

| Field | Value |
|---|---|
| **Version** | `0.4.0-rc.2` |
| **Date** | 2026-07-28 |
| **Gate** | `npm run test:release` → **exit 0** |
| **P0** | 16 canaries × 3 staggered rounds — all pass |

## Semantic correction: Fallback is not a Host-contract blocker

Session **does not own** a permanent agent/model. Host only continues the **last user prompt**'s agent/model. Explicit user model always wins and may start a new Authority / Fallback epoch.

A/A/B/B is implemented as:

1. Durable `FallbackFailureRecorded` only on `session.status=retry`
2. Failures → Side map (A, A, permanent B, B, Dead)
3. Next Authority Root prompt that **omits model** → `chat.message` injects Side A/B into **that user message**
4. Host Effect.retry may re-run the **same** user message with the same model (attempt-local); that is not session lock-in

No `setAgentModel` required. No `HOST_CONTRACT_UNAVAILABLE` for this product shape.

## What landed (code, not docs-only)

- Prompt Authority full pipeline (claim/accept, UnknownOrigin fail-closed, Busy nudge, Companion eligibility, Fallback identity)
- Script forest mock (KISS-N11); error edges not seal-cached
- Review content confirmation; orchestrator / restart publish canaries
- Fallback user-prompt model injection in `HostSignalChatMessage`
- `docs/E2E_RELEASE_GUIDE.md` §4 rewritten to match

## Verify

```bash
npm run test:release
# = build + test:compile + test:next + manager-tools + gate-testkit + 3× staggered P0
```

## Next

- Optional: promote to `0.4.0` final after clean-checkout re-run and pack install smoke
- Keep product rule: explicit user model resets Fallback epoch; omit-model inherits durable Side

## Ship

```bash
git push origin master
# optional: git tag v0.4.0-rc.2 && git push origin v0.4.0-rc.2
```
