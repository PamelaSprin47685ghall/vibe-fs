// Join v2 mailbox / interrupt / batch drain contract (EXEC-017 / EXEC-018 / EXEC-019).
// CompletionMailbox + VerdictMailbox only — no HostForkRuntime (journal/durable).

import assert from 'node:assert/strict'
import test from 'node:test'
import {
  agentCompletion,
  caseOf,
  completionMailbox,
  joinInterrupt,
  joinWaitOutcome,
  mailboxWakeReason,
  maxJoinBatch,
  nonEmptyBatch,
  payloadOf,
  verdictMailbox,
} from '../support/domain.mjs'
import { JoinInterruptReason } from '../../../dist/Session/CompletionMailbox.js'

const run = (id) =>
  agentCompletion.completedRun({
    runId: `run-${id}`,
    agentId: id,
    agentName: `agent-${id}`,
    workRecord: `wr-${id}`,
  })

// ── MaxJoinBatch constant ────────────────────────────────────────────────────

test('EXEC_018_max_join_batch_is_32', () => {
  assert.equal(maxJoinBatch, 32)
  assert.equal(completionMailbox.maxJoinBatch, 32)
})

// ── 4: two completions drained in one batch ──────────────────────────────────

test('EXEC_018_drain_available_returns_two_completions_in_publish_order', () => {
  const box = completionMailbox.create(() => true)
  completionMailbox.publish(box, run('a'))
  completionMailbox.publish(box, run('b'))
  const batch = completionMailbox.drainAvailable(box, maxJoinBatch)
  assert.equal(batch.length, 2)
  assert.equal(batch[0].AgentId, 'a')
  assert.equal(batch[1].AgentId, 'b')
  assert.equal(completionMailbox.pendingCount(box), 0)
})

// ── 5: 33 completions → first drain 32, second drain 1 ───────────────────────

test('EXEC_018_thirty_three_completions_split_across_two_drains', () => {
  const box = completionMailbox.create(() => true)
  for (let i = 0; i < 33; i += 1) {
    completionMailbox.publish(box, run(`c${i}`))
  }
  assert.equal(completionMailbox.pendingCount(box), 33)

  const first = completionMailbox.drainAvailable(box, maxJoinBatch)
  assert.equal(first.length, 32)
  assert.equal(first[0].AgentId, 'c0')
  assert.equal(first[31].AgentId, 'c31')
  assert.equal(completionMailbox.pendingCount(box), 1)

  const second = completionMailbox.drainAvailable(box, maxJoinBatch)
  assert.equal(second.length, 1)
  assert.equal(second[0].AgentId, 'c32')
  assert.equal(completionMailbox.pendingCount(box), 0)
})

// ── 6: no duplicate handle in a drained batch ────────────────────────────────

test('EXEC_018_drained_batch_has_unique_agent_ids', () => {
  const box = completionMailbox.create(() => true)
  for (const id of ['x', 'y', 'z', 'x2', 'y2']) {
    completionMailbox.publish(box, run(id))
  }
  const batch = completionMailbox.drainAvailable(box, maxJoinBatch)
  const ids = batch.map((c) => c.AgentId)
  assert.deepEqual(ids, [...new Set(ids)])
  assert.equal(ids.length, 5)
})

// ── 7: second drain does not re-deliver consumed completions ─────────────────

test('EXEC_018_second_drain_does_not_re_consume_same_completion', () => {
  const box = completionMailbox.create(() => true)
  completionMailbox.publish(box, run('once'))
  const first = completionMailbox.drainAvailable(box, 1)
  assert.equal(first.length, 1)
  assert.equal(first[0].AgentId, 'once')

  const second = completionMailbox.drainAvailable(box, maxJoinBatch)
  assert.deepEqual(second, [])
  assert.equal(completionMailbox.pendingCount(box), 0)
})

// ── 1: WaitForSignal + user interrupt → UserInterrupted ──────────────────────

test('EXEC_017_wait_for_signal_user_interrupt_returns_user_interrupted', async () => {
  const box = completionMailbox.create(() => true)
  const interrupt = joinInterrupt.create()
  const pending = completionMailbox.waitForSignal(box, joinInterrupt.wait(interrupt))
  await new Promise((r) => setTimeout(r, 5))
  interrupt.Signal(JoinInterruptReason.OperatorAbort)
  const reason = await pending
  assert.equal(mailboxWakeReason.nameOf(reason), 'LocalInterrupt')
  assert.equal(caseOf(payloadOf(reason)), 'OperatorAbort')
})

// ── 2: interrupt does not cancel mailbox / does not discard later publish ────

test('EXEC_017_interrupt_does_not_cancel_mailbox_child_still_publishable', async () => {
  const box = completionMailbox.create(() => true)
  const interrupt = joinInterrupt.create()
  const pending = completionMailbox.waitForSignal(box, joinInterrupt.wait(interrupt))
  interrupt.Signal(JoinInterruptReason.OperatorAbort)
  await pending

  assert.equal(completionMailbox.isCancelled(box), false, 'interrupt ≠ Cancel')
  completionMailbox.publish(box, run('after-interrupt'))
  assert.equal(completionMailbox.pendingCount(box), 1)
  const drained = completionMailbox.drainAvailable(box, 1)
  assert.equal(drained[0].AgentId, 'after-interrupt')
})

// ── 3: after interrupt, next join/drain obtains the later completion ─────────

test('EXEC_017_completion_after_interrupt_is_available_to_next_drain', async () => {
  const box = completionMailbox.create(() => true)
  const interrupt = joinInterrupt.create()
  const waitP = completionMailbox.waitForSignal(box, joinInterrupt.wait(interrupt))
  interrupt.Signal(JoinInterruptReason.OperatorAbort)
  const reason = await waitP
  assert.equal(mailboxWakeReason.nameOf(reason), 'LocalInterrupt')
  assert.equal(caseOf(payloadOf(reason)), 'OperatorAbort')

  // Child finishes after the interrupted join returned.
  completionMailbox.publish(box, run('late-child'))
  const next = completionMailbox.drainAvailable(box, maxJoinBatch)
  assert.equal(next.length, 1)
  assert.equal(next[0].AgentId, 'late-child')
})

// ── 8: drain-before-interrupt — completion already queued wins ───────────────

test('EXEC_018_drain_before_interrupt_prefers_existing_completion', async () => {
  const box = completionMailbox.create(() => true)
  completionMailbox.publish(box, run('already-done'))

  // Immediate drain path (HostForkRuntime.tryDrainAvailable / WaitForSignal re-drain).
  const ready = completionMailbox.drainAvailable(box, maxJoinBatch)
  assert.equal(ready.length, 1)
  assert.equal(ready[0].AgentId, 'already-done')

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
  const after = completionMailbox.drainAvailable(box, maxJoinBatch)
  assert.equal(after.length, 1)
  assert.equal(after[0].AgentId, 'also-done')
})

// ── NonEmptyBatch + JoinWaitOutcome shape ────────────────────────────────────

test('EXEC_018_non_empty_batch_and_join_wait_outcome_constructors', () => {
  assert.equal(nonEmptyBatch.tryOfList([]), undefined)
  const batch = nonEmptyBatch.tryOfList([run('h'), run('t')])
  assert.ok(batch !== undefined)
  assert.equal(nonEmptyBatch.length(batch), 2)
  assert.deepEqual(
    nonEmptyBatch.toList(batch).map((c) => c.AgentId),
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
  const box = completionMailbox.create(() => true)
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
