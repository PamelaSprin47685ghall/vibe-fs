# E2E canaries

Bounded parallel suite: `node tests/e2e/run.mjs` (optional `--repeat 1|2|3`).
Each case under `cases/` pairs with a scenario TOML under `scenarios/`.

## Manager unhappy path

| File | Ownership |
|------|-----------|
| `scenarios/manager-unhappy-path.toml` | Provider turn script |
| `cases/manager-unhappy-path.test.mjs` | Sequencing oracles the TOML cannot express |

Public product contracts exercised here (formal docs win on conflict):

- **Join user-message wake (EXEC-017)** — blocked Manager `join` exits with
  `status="interrupted"`, `reason="user_message"` (not `operator_abort`). Queued
  user text is consumed on a later Manager turn; child stays listable/joinable.
  See `tests/docs/join-user-message-wake.md`.
- **Finality instruction-only refusal** — premature `suicide` while work is
  outstanding returns guidance comments (e.g. call join) without top-level
  `error=`.
- **Finality rejection / blessing** — work records projected as Host-adopted
  guidance comments; one blessing and one life complete.
- **Coder reuse** — a single durable fast-coder session across reuses;
  hidden reviewers never appear in Manager join results.

### Strict mock causal hold

`support/strict-mock-responses.js` supports `respond.waitUntil` (a Promise).
While the promise is pending, the mock keeps the SSE open after tokens and only
writes `[DONE]` after resolve. The unhappy-path case uses this so child C1 stays
incomplete across join + external user message without fixed sleep as the wake
mechanism.

### Reviewer turn matching note

Production Finality reviewer lastUser is Host `OpeningAssignment` (+ payload
data), not mid-run MARK labor text. The case rewrites first-round reviewer turn
matchers to the opening assignment prefix and retires first-round digests after
their last step so later rounds cannot steal enlistment. Do not “fix” matching
by elevating mid-run labor prompts to HumanRoot (PROMPT-004).

## Related unit proof

`tests/unit/execution/join-v2-mailbox.test.mjs` — registry fan-out, latch,
mailbox non-cancel, drain-before-interrupt, anti-cheat against OperatorAbort
masquerading as user_message.
