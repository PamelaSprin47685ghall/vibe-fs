# Join user-message wake — public contract (test surface)

Product clauses: `docs/what/execution.md` EXEC-004 / EXEC-017;
algorithm and wire: `docs/how/execution.md` (Join wire, EXEC-018/025).
This file documents only the **verified public proof surface** owned by the
unit/e2e sources listed below. It does not redefine product semantics.

## Sources

| Path | Role |
|------|------|
| `src/Wanxiangshu/Session/JoinInterruptRegistry.fs` | Process-local join wake registry |
| `tests/unit/execution/join-v2-mailbox.test.mjs` | Mailbox + registry + anti-cheat |
| `tests/e2e/cases/manager-unhappy-path.test.mjs` | End-to-end `reason=user_message` oracle |
| `tests/e2e/support/strict-mock-responses.js` | Causal SSE hold (`waitUntil`) for incomplete child |

## Public behavior

### Wait outcomes

A blocked `join` ends for one of:

```text
completion available  → ResultsAvailable (drain wins)
operator abort (Esc / tool abort) → Interrupted(OperatorAbort)
external user ingress → Interrupted(UserMessageArrived)
DevOps 10s deadline   → ForkError.TimedOut (status=failed, code=TIMED_OUT)
```

Wire for human/operator interrupts (not errors):

```toml
status = "interrupted"
reason = "user_message"   # external user ingress

status = "interrupted"
reason = "operator_abort" # Esc / AttachAbort
```

### Wake ≠ authority

External-user ingress ends **only the current wait**:

- does not cancel mailbox, runtime, session, or child
- does not abandon handles or drop later completions
- does not call `AcceptHumanRoot`, reset LogicalRun, or open a new Manager Life
- does not itself grant Prompt authority (PROMPT-004 fail-closed stays)

Queued user text remains for the next provider turn; the join tool result carries
only the typed interruption fact, not a copy of the user text.

### Drain-before-interrupt

After any race wake, re-drain the authoritative completion source first. If a
completion is already visible, return it; only an empty drain may emit the
interrupt result.

### `IJoinInterruptRegistry` (process-local)

```text
Register(sessionId, JoinInterrupt) → IDisposable  # unregister on dispose
SignalUserMessage(sessionId) → unit                 # UserMessageArrived fan-out
```

Properties proven by unit tests:

1. `SignalUserMessage` fans out `UserMessageArrived` to every active waiter for
   that session (not `OperatorAbort`).
2. Signal-before-Register latches once: a pulse with zero waiters is consumed by
   the next `Register` for that session.
3. Pulse is wake-only and not journaled.
4. User-message interrupt leaves the mailbox usable; a later publish remains
   drainable by the next join.

Classification of external-user candidates (product layer): physical user message
id present, no `PromptKey`, not host compaction. PromptKey continuations and host
compaction must not wake join.

### Anti-cheat gate

Any test title containing `user_message`, `UserMessageArrived`, or
`human_root_interrupt` must not use `JoinInterruptReason.OperatorAbort` as the
primary stimulus. Enforced by
`EXEC_017_anti_cheat_user_message_tests_must_not_use_operator_abort_stimulus`
in `join-v2-mailbox.test.mjs`.

## E2E: manager-unhappy-path

Scenario: `tests/e2e/scenarios/manager-unhappy-path.toml`.
Oracle: `tests/e2e/cases/manager-unhappy-path.test.mjs` `finalOracle`.

Hard wire assertions for stroke 3 (join wake):

```text
≥1 join tool result with status = "interrupted"
at least one reason = "user_message"
zero reason = "operator_abort" on that path
next Manager provider turn consumes the queued labor prompt
child remains harvestable via a later join (HandleRetired ≥ 1)
```

Causal hold: child C1 SSE stays open across join + user-message wake via
`respond.waitUntil` (see `strict-mock-responses.js`). Fixed sleeps are not the
wake mechanism.

Related oracle notes (not join wire, but same canary):

- ordinary suicide refusal while work is outstanding is instruction-only
  (`Call join before seeking your end`) with **no** top-level `error=` field
- Finality rejection continues as guidance comments carrying the work record
- reviewer first-assignment matching uses Host opening assignment text; mid-run
  MARK fragments are not production lastUser (PROMPT-004)

## Gotchas

1. Do not treat Esc / `OperatorAbort` as a substitute for user-message wake in
   tests named for user messages.
2. Do not assert that queued user messages never interrupt join — that clause is
   reversed by EXEC-017.
3. Completion that is already durable/queued beats a simultaneous user pulse.
4. A second independent Manager idle occasion is unit-proven elsewhere; the
   unhappy-path canary does not require two idle encouragements.

## Run

```bash
node tests/unit/run.mjs          # includes join-v2-mailbox
node tests/e2e/cases/manager-unhappy-path.test.mjs
```
