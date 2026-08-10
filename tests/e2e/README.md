# E2E — One World / The Long Stroke

Sole entry: `node tests/e2e/entry.test.mjs` (`npm run test:e2e`).

| File | Role |
|------|------|
| `entry.test.mjs` | Only top-level E2E entry (G4R / One World) |
| `scenarios/long-stroke.toml` | Provider turn script for the Long Stroke |
| `support/long-stroke-oracles.mjs` | Sequencing / adversity oracles the TOML cannot express |

One continuous OpenCode lifetime (`spawn === 1`). There is no multi-canary pool, no `cases/` suite runner, and no `--repeat` / shuffle release gate.

## What the Long Stroke covers

Public product contracts exercised here (formal docs win on conflict):

- **Join user-message wake (EXEC-017)** — blocked Manager `join` exits with
  `status="interrupted"`, `reason="user_message"` (not `operator_abort`).
- **Provider transient failure + fallback** — sole provider-error then continuation.
- **Finality REVISE → rejection → blessing** — Host-owned reviewers; later successful finality and life complete.
- **Publish conflict / reconciliation** — stale target via `gitConflictProof` (no restart), then Published on the same Manager.
- **§21 adversity checklist** — see `support/long-stroke-oracles.mjs` (`ADVERSITY_CHECKLIST`).

### Strict mock causal hold

`support/strict-mock-responses.js` supports `respond.waitUntil` (a Promise).
While the promise is pending, the mock keeps the SSE open after tokens and only
writes `[DONE]` after resolve. The Long Stroke uses this so a child can stay
incomplete across join + external user message without fixed sleep as the wake
mechanism.

## Related unit proof

`tests/unit/execution/join-v2-mailbox.test.mjs` — registry fan-out, latch,
mailbox non-cancel, drain-before-interrupt, anti-cheat against OperatorAbort
masquerading as user_message.

Temporal race extraction lives under `tests/unit/temporal/` (G4R-1/2), not as
extra E2E canaries.
