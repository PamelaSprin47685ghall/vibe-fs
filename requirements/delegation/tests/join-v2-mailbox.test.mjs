// Split from tests/unit/execution/join-v2-mailbox.test.mjs (cutover Wave 2a);
// owner: delegation. Join v2 mailbox / interrupt / batch drain contract
// （DELEG-013/014/015，EXEC-017/018/019）：CompletionMailbox + VerdictMailbox，
// 有界批次、稳定排序、join 中断 = Interrupted 非 ForkError、wake 机制。
// `EXEC_018_drain_available_returns_two_completions_in_publish_order`
// （PTY 队列 FIFO，PROC-008 完成事实双通道）→ process-execution。
// No HostForkRuntime (journal/durable) — mailbox only.

import assert from 'node:assert/strict'
import fs from 'node:fs'
import test from 'node:test'
import { fileURLToPath } from 'node:url'
import {
  agentCompletion,
  agentIdOf,
  caseOf,
  completionMailbox,
  joinInterrupt,
  joinWaitOutcome,
  mailboxWakeReason,
  maxJoinBatch,
  nonEmptyBatch,
  payloadOf,
  verdictMailbox,
} from '../../verification-system/tests/support/domain.mjs'
import { JoinInterruptReason } from '../../../dist/Execution/Session/Wait/CompletionMailbox.js'
import {
  JoinAttemptRegistry,
  JoinAttemptLease__get_Wait as leaseWait,
} from '../../../dist/Execution/Delegation/Handle/JoinInterruptRegistry.js'
import { SessionIdModule_create } from '../../../dist/Foundation/Identity.js'

const assertPending = async (wait, message) => {
  let settled = false
  wait.then(() => {
    settled = true
  })
  await Promise.resolve()
  assert.equal(settled, false, message)
}

const run = (id) =>
  agentCompletion.completedRun({
    runId: `run-${id}`,
    agentId: id,
    agentName: `agent-${id}`,
    workRecord: `wr-${id}`,
  })

const ptyIdOfDrained = (item) => completionMailbox.ptyIdOf(item)

// ── MaxJoinBatch constant ────────────────────────────────────────────────────

test('EXEC_018_max_join_batch_is_32', () => {
  assert.equal(maxJoinBatch, 32)
  assert.equal(completionMailbox.maxJoinBatch, 32)
})

// ── 5: 33 completions → first drain 32, second drain 1 ───────────────────────

test('EXEC_018_thirty_three_completions_split_across_two_drains', () => {
  const box = completionMailbox.create()
  for (let i = 0; i < 33; i += 1) {
    completionMailbox.publish(box, run(`c${i}`))
  }
  assert.equal(completionMailbox.pendingCount(box), 33)

  const first = completionMailbox.drainPtyCompletions(box, maxJoinBatch)
  assert.equal(first.length, 32)
  assert.equal(ptyIdOfDrained(first[0]), 'c0')
  assert.equal(ptyIdOfDrained(first[31]), 'c31')
  assert.equal(completionMailbox.pendingCount(box), 1)

  const second = completionMailbox.drainPtyCompletions(box, maxJoinBatch)
  assert.equal(second.length, 1)
  assert.equal(ptyIdOfDrained(second[0]), 'c32')
  assert.equal(completionMailbox.pendingCount(box), 0)
})

// ── 6: no duplicate handle in a drained batch ────────────────────────────────

test('EXEC_018_drained_batch_has_unique_agent_ids', () => {
  const box = completionMailbox.create()
  for (const id of ['x', 'y', 'z', 'x2', 'y2']) {
    completionMailbox.publish(box, run(id))
  }
  const batch = completionMailbox.drainPtyCompletions(box, maxJoinBatch)
  const ids = batch.map(ptyIdOfDrained)
  assert.deepEqual(ids, [...new Set(ids)])
  assert.equal(ids.length, 5)
})

// ── 7: second drain does not re-deliver consumed completions ─────────────────

test('EXEC_018_second_drain_does_not_re_consume_same_completion', () => {
  const box = completionMailbox.create()
  completionMailbox.publish(box, run('once'))
  const first = completionMailbox.drainPtyCompletions(box, 1)
  assert.equal(first.length, 1)
  assert.equal(ptyIdOfDrained(first[0]), 'once')

  const second = completionMailbox.drainPtyCompletions(box, maxJoinBatch)
  assert.deepEqual(second, [])
  assert.equal(completionMailbox.pendingCount(box), 0)
})

// ── 1: WaitForSignal + operator abort → LocalInterrupt OperatorAbort ─────────

test('EXEC_017_wait_for_signal_operator_abort_returns_local_interrupt', async () => {
  const box = completionMailbox.create()
  const interrupt = joinInterrupt.create()
  const pending = completionMailbox.waitForSignal(box, joinInterrupt.wait(interrupt))
  interrupt.Signal(JoinInterruptReason.OperatorAbort)
  const reason = await pending
  assert.equal(mailboxWakeReason.nameOf(reason), 'LocalInterrupt')
  assert.equal(caseOf(payloadOf(reason)), 'OperatorAbort')
})

// ── 1b: WaitForSignal + UserMessageArrived (registry signal, not OperatorAbort)

test('EXEC_017_wait_for_signal_user_message_returns_user_message_arrived', async () => {
  const box = completionMailbox.create()
  const interrupt = joinInterrupt.create()
  const pending = completionMailbox.waitForSignal(box, joinInterrupt.wait(interrupt))
  interrupt.Signal(JoinInterruptReason.UserMessageArrived)
  const reason = await pending
  assert.equal(mailboxWakeReason.nameOf(reason), 'LocalInterrupt')
  assert.equal(caseOf(payloadOf(reason)), 'UserMessageArrived')
  assert.notEqual(caseOf(payloadOf(reason)), 'OperatorAbort')
})

test('EXEC_017_user_message_interrupt_does_not_cancel_mailbox', async () => {
  const box = completionMailbox.create()
  const interrupt = joinInterrupt.create()
  const pending = completionMailbox.waitForSignal(box, joinInterrupt.wait(interrupt))
  interrupt.Signal(JoinInterruptReason.UserMessageArrived)
  await pending

  assert.equal(completionMailbox.isCancelled(box), false, 'user message ≠ Cancel')
  completionMailbox.publish(box, run('after-user-message'))
  assert.equal(completionMailbox.pendingCount(box), 1)
  const drained = completionMailbox.drainPtyCompletions(box, 1)
  assert.equal(ptyIdOfDrained(drained[0]), 'after-user-message')
})

// EXEC-017: two active attempts in one session both receive UserMessageArrived.
test('EXEC_017_join_interrupt_registry_signal_user_message_fans_out', async () => {
  const registry = new JoinAttemptRegistry()
  const session = SessionIdModule_create('ses-registry-fanout')
  const a1 = registry.Begin(session, undefined)
  const a2 = registry.Begin(session, undefined)

  const wait1 = leaseWait(a1)
  const wait2 = leaseWait(a2)
  registry.SignalUserMessage(session)

  const reason1 = await wait1
  const reason2 = await wait2
  assert.equal(caseOf(reason1), 'UserMessageArrived')
  assert.equal(caseOf(reason2), 'UserMessageArrived')
  assert.notEqual(caseOf(reason1), 'OperatorAbort')

  a1.Dispose()
  a2.Dispose()
})

// EXEC-017: a user message with no active attempt is dropped as a join wake;
// a future join remains blocked until completion, a new user message, or Esc.
test('EXEC_017_join_interrupt_registry_signal_with_no_active_attempt_does_not_wake_future_join', async () => {
  const registry = new JoinAttemptRegistry()
  const session = SessionIdModule_create('ses-registry-no-future-wake')

  registry.SignalUserMessage(session) // no active join attempt — dropped, not latched
  const attempt = registry.Begin(session, undefined) // a FUTURE join begins later

  await assertPending(
    leaseWait(attempt),
    'an old user message must not wake a future join (EXEC-017)',
  )

  attempt.Dispose()
})

// SessionDeleted cleanup: ClearSession removes active attempts without signaling, so a
// later Begin stays blocked (no residual latch).
test('EXEC_017_join_interrupt_registry_clear_session_removes_active_attempts', async () => {
  const registry = new JoinAttemptRegistry()
  const session = SessionIdModule_create('ses-registry-clear')

  const attempt = registry.Begin(session, undefined)
  registry.ClearSession(session)

  registry.SignalUserMessage(session)

  await assertPending(
    leaseWait(attempt),
    'ClearSession must remove active attempts; no UserMessageArrived is delivered',
  )

  attempt.Dispose()
})

// EXEC-017: Begin opens the attempt first, so a user signal arriving before
// mailbox wait setup is recorded on this attempt's
// own TCS and still wakes the current join. The signal-before-register race is
// solved by the attempt scope, not by a session-level latch.
test('EXEC_017_join_attempt_signal_before_mailbox_setup_wakes_current_join', async () => {
  const registry = new JoinAttemptRegistry()
  const session = SessionIdModule_create('ses-registry-p0-3')

  // Begin the attempt first, exactly like JoinTool does; then a user signal lands
  // before any mailbox registration is made — it must still resolve attempt.Wait.
  const attempt = registry.Begin(session, undefined)
  registry.SignalUserMessage(session)

  // The mailbox wait is set up 'later'; the attempt is already awake.
  const reason = await leaseWait(attempt)
  assert.equal(caseOf(reason), 'UserMessageArrived', 'signal before mailbox setup must wake the current join')
  assert.notEqual(caseOf(reason), 'OperatorAbort')

  attempt.Dispose()
})

// EXEC-017: after join A disposes, later join B must not inherit A's signal.
test('EXEC_017_join_attempt_old_signal_does_not_bleed_into_next_join', async () => {
  const registry = new JoinAttemptRegistry()
  const session = SessionIdModule_create('ses-registry-p0-4')

  // join A active, user message wakes it, A ends (Dispose unregisters).
  const a = registry.Begin(session, undefined)
  registry.SignalUserMessage(session)
  const reasonA = await leaseWait(a)
  assert.equal(caseOf(reasonA), 'UserMessageArrived')
  a.Dispose()

  // join B begins later: it must NOT inherit A's user-message signal.
  const b = registry.Begin(session, undefined)
  await assertPending(leaseWait(b), 'join B must not inherit join A\'s user-message signal (EXEC-017)')
  b.Dispose()
})

// ── 2: interrupt does not cancel mailbox / does not discard later publish ────

test('EXEC_017_interrupt_does_not_cancel_mailbox_child_still_publishable', async () => {
  const box = completionMailbox.create()
  const interrupt = joinInterrupt.create()
  const pending = completionMailbox.waitForSignal(box, joinInterrupt.wait(interrupt))
  interrupt.Signal(JoinInterruptReason.OperatorAbort)
  await pending

  assert.equal(completionMailbox.isCancelled(box), false, 'interrupt ≠ Cancel')
  completionMailbox.publish(box, run('after-interrupt'))
  assert.equal(completionMailbox.pendingCount(box), 1)
  const drained = completionMailbox.drainPtyCompletions(box, 1)
  assert.equal(ptyIdOfDrained(drained[0]), 'after-interrupt')
})

// ── 3: after interrupt, next join/drain obtains the later completion ─────────

test('EXEC_017_completion_after_interrupt_is_available_to_next_drain', async () => {
  const box = completionMailbox.create()
  const interrupt = joinInterrupt.create()
  const waitP = completionMailbox.waitForSignal(box, joinInterrupt.wait(interrupt))
  interrupt.Signal(JoinInterruptReason.OperatorAbort)
  const reason = await waitP
  assert.equal(mailboxWakeReason.nameOf(reason), 'LocalInterrupt')
  assert.equal(caseOf(payloadOf(reason)), 'OperatorAbort')

  // Child finishes after the interrupted join returned.
  completionMailbox.publish(box, run('late-child'))
  const next = completionMailbox.drainPtyCompletions(box, maxJoinBatch)
  assert.equal(next.length, 1)
  assert.equal(ptyIdOfDrained(next[0]), 'late-child')
})

// ── 8: drain-before-interrupt — completion already queued wins ───────────────

test('EXEC_018_drain_before_interrupt_prefers_existing_completion', async () => {
  const box = completionMailbox.create()
  completionMailbox.publish(box, run('already-done'))

  // Immediate drain path (HostForkRuntime.tryDrainAvailable / WaitForSignal re-drain).
  const ready = completionMailbox.drainPtyCompletions(box, maxJoinBatch)
  assert.equal(ready.length, 1)
  assert.equal(ptyIdOfDrained(ready[0]), 'already-done')

  // Even if interrupt is already signalled, re-drain would still see results first.
  const interrupt = joinInterrupt.create()
  interrupt.Signal(JoinInterruptReason.OperatorAbort)
  completionMailbox.publish(box, run('also-done'))
  const reason = await completionMailbox.waitForSignal(box, joinInterrupt.wait(interrupt))
  // Wait may report LocalInterrupt or CompletionMayBeAvailable; re-drain is authoritative.
  assert.ok(
    mailboxWakeReason.nameOf(reason) === 'LocalInterrupt' ||
      mailboxWakeReason.nameOf(reason) === 'CompletionMayBeAvailable',
  )
  const after = completionMailbox.drainPtyCompletions(box, maxJoinBatch)
  assert.equal(after.length, 1)
  assert.equal(ptyIdOfDrained(after[0]), 'also-done')
})

// ── NonEmptyBatch + JoinWaitOutcome shape ────────────────────────────────────

test('EXEC_018_non_empty_batch_and_join_wait_outcome_constructors', () => {
  assert.equal(nonEmptyBatch.tryOfList([]), undefined)
  const batch = nonEmptyBatch.tryOfList([run('h'), run('t')])
  assert.ok(batch !== undefined)
  assert.equal(nonEmptyBatch.length(batch), 2)
  assert.deepEqual(
    nonEmptyBatch.toList(batch).map(agentIdOf),
    ['h', 't'],
  )
})

// ── 11: Orchestrator VerdictMailbox FIFO TryJoinBatch ────────────────────────

test('EXEC_019_verdict_mailbox_try_join_batch_preserves_publish_fifo', async () => {
  const box = verdictMailbox.create()
  verdictMailbox.startJob(box)
  verdictMailbox.startJob(box)
  verdictMailbox.startJob(box)

  verdictMailbox.publish(box, verdictMailbox.rejectedDirty('first'))
  verdictMailbox.publish(box, verdictMailbox.rejectedDirty('second'))
  verdictMailbox.publish(box, verdictMailbox.rejectedDirty('third'))

  const batch = await verdictMailbox.tryJoinBatch(box, maxJoinBatch)
  assert.equal(batch.length, 3)
  assert.equal(verdictMailbox.nameOf(batch[0]), 'RejectedDirty')
  assert.equal(batch[0].fields[0], 'first')
  assert.equal(batch[1].fields[0], 'second')
  assert.equal(batch[2].fields[0], 'third')
  assert.equal(verdictMailbox.pendingCount(box), 0)
})

test('EXEC_019_verdict_mailbox_join_available_interrupt_without_verdict', async () => {
  const box = verdictMailbox.create()
  // Active job keeps JoinAvailable waiting; interrupt returns InterruptedByUserMessage.
  verdictMailbox.startJob(box)
  const interrupt = joinInterrupt.create()
  const pending = verdictMailbox.joinAvailable(box, maxJoinBatch, joinInterrupt.wait(interrupt))
  await new Promise((r) => setTimeout(r, 5))
  interrupt.Signal(JoinInterruptReason.OperatorAbort)
  const outcome = await pending
  assert.equal(joinWaitOutcome.nameOf(outcome), 'Interrupted')
  assert.equal(caseOf(payloadOf(outcome)), 'OperatorAbort')
})

test('EXEC_019_verdict_mailbox_join_available_prefers_drained_results_over_interrupt', async () => {
  const box = verdictMailbox.create()
  verdictMailbox.startJob(box)
  verdictMailbox.publish(box, verdictMailbox.rejectedDirty('preloaded'))

  const interrupt = joinInterrupt.create()
  interrupt.Signal(JoinInterruptReason.OperatorAbort)
  const outcome = await verdictMailbox.joinAvailable(box, maxJoinBatch, joinInterrupt.wait(interrupt))
  assert.equal(joinWaitOutcome.nameOf(outcome), 'ResultsAvailable')
  const batch = joinWaitOutcome.results(outcome)
  assert.equal(nonEmptyBatch.length(batch), 1)
  assert.equal(nonEmptyBatch.toList(batch)[0].fields[0], 'preloaded')
})

// ── Cancel is lifecycle only (still available; interrupt path does not call it)

test('EXEC_017_mailbox_cancel_is_separate_from_join_interrupt', async () => {
  const box = completionMailbox.create()
  const interrupt = joinInterrupt.create()
  const waitP = completionMailbox.waitForSignal(box, joinInterrupt.wait(interrupt))
  interrupt.Signal(JoinInterruptReason.OperatorAbort)
  await waitP
  assert.equal(completionMailbox.isCancelled(box), false)

  completionMailbox.cancel(box)
  assert.equal(completionMailbox.isCancelled(box), true)
  const reason = await completionMailbox.waitForWake(box)
  assert.equal(mailboxWakeReason.nameOf(reason), 'MailboxCancelled')
})

// ── Anti-cheat: user_message tests must not use OperatorAbort as stimulus ────
// Proposal §7.2: names containing user_message / UserMessageArrived /
// human_root_interrupt must not call JoinInterruptReason.OperatorAbort as the
// primary stimulus. Comments that forbid OperatorAbort are allowed.

test('EXEC_017_anti_cheat_user_message_tests_must_not_use_operator_abort_stimulus', () => {
  const source = fs.readFileSync(fileURLToPath(import.meta.url), 'utf8')
  const titleNeedle = /user_message|UserMessageArrived|human_root_interrupt/i
  const testOpenRe =
    /test\(\s*(['"`])((?:\\.|(?!\1).)*)\1\s*,\s*(?:async\s*)?\([^)]*\)\s*=>\s*\{/g

  let match
  let scanned = 0
  while ((match = testOpenRe.exec(source)) !== null) {
    const title = match[2]
    if (!titleNeedle.test(title)) continue
    // This meta-test's own title matches the needle; skip self.
    if (title.includes('anti_cheat')) continue

    let depth = 1
    let i = match.index + match[0].length
    while (i < source.length && depth > 0) {
      const ch = source[i]
      if (ch === '{') depth += 1
      else if (ch === '}') depth -= 1
      i += 1
    }
    const body = source.slice(match.index + match[0].length, i - 1)
    const codeOnly = body
      .split('\n')
      .filter((line) => {
        const trimmed = line.trim()
        return !(trimmed.startsWith('//') || trimmed.startsWith('*') || trimmed.startsWith('/*'))
      })
      .join('\n')

    scanned += 1
    assert.equal(
      codeOnly.includes('JoinInterruptReason.OperatorAbort'),
      false,
      `test '${title}' must not use JoinInterruptReason.OperatorAbort as the primary stimulus`,
    )
  }

  assert.ok(scanned >= 1, 'expected at least one user_message-named test body to scan')
})
