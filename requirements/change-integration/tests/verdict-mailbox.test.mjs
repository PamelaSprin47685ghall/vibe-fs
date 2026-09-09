// Verdict mailbox race over the real compiled VerdictMailbox.JoinAvailable.
// Every outcome below is the production drain-first → race → re-drain decision
// over an in-memory verdict queue: no Join is modelled here and no source is
// inspected.
import assert from 'node:assert/strict'
import test from 'node:test'
import * as change from '../../../dist/Change/Surface.js'

const published = (jobId, head) => ({ kind: 'Published', jobId, head })

test('WHAT[DELEG-014] VERDICT_MAILBOX_ready_or_racing_verdict_beats_interrupt', async () => {
  for (const order of ['queued-before-join', 'publish-then-interrupt', 'interrupt-then-publish']) {
    const mailbox = change.createVerdictMailbox()
    change.verdictMailboxStartJob(mailbox)
    const interrupt = change.createVerdictInterrupt()
    let pending
    if (order === 'queued-before-join') {
      change.verdictMailboxPublish(mailbox, published('job-1', 'head-1'))
      change.fireVerdictInterrupt(interrupt, 'OperatorAbort')
      pending = change.verdictMailboxJoinAvailable(mailbox, 8, interrupt)
    } else {
      pending = change.verdictMailboxJoinAvailable(mailbox, 8, interrupt)
      if (order === 'publish-then-interrupt') {
        change.verdictMailboxPublish(mailbox, published('job-1', 'head-1'))
        change.fireVerdictInterrupt(interrupt, 'OperatorAbort')
      } else {
        change.fireVerdictInterrupt(interrupt, 'OperatorAbort')
        change.verdictMailboxPublish(mailbox, published('job-1', 'head-1'))
      }
    }
    const out = await pending
    assert.equal(out.kind, 'ResultsAvailable', `${order}: re-drain must beat the interrupt`)
    assert.equal(out.count, 1)
    assert.equal(out.verdicts[0].kind, 'Published')
    assert.equal(out.verdicts[0].detail, 'head-1')
    assert.equal(change.verdictMailboxPendingCount(mailbox), 0, `${order}: winning batch must drain exactly once`)
  }
})

test('WHAT[DELEG-015] VERDICT_MAILBOX_pending_interrupt_stays_interrupted_then_next_publish_delivers_exactly_once', async () => {
  const mailbox = change.createVerdictMailbox()
  change.verdictMailboxStartJob(mailbox)
  const interrupt = change.createVerdictInterrupt()
  const pending = change.verdictMailboxJoinAvailable(mailbox, 8, interrupt)
  change.fireVerdictInterrupt(interrupt, 'UserMessageArrived')
  const out = await pending
  assert.equal(out.kind, 'Interrupted')
  assert.equal(out.reason, 'UserMessageArrived')
  assert.notEqual(out.kind, 'ResultsAvailable', 'interrupt must never masquerade as a batch')

  // Interrupting the wait leaves the job active. Its next waiter must wake,
  // rather than the removed waiter absorbing this job's eventual publication.
  const next = change.verdictMailboxJoinAvailable(mailbox, 8, change.createVerdictInterrupt())
  change.verdictMailboxPublish(mailbox, published('job-2', 'head-2'))
  const after = await next
  assert.equal(after.kind, 'ResultsAvailable')
  assert.equal(after.count, 1)
  assert.equal(after.verdicts[0].kind, 'Published')
  assert.equal(after.verdicts[0].detail, 'head-2')
  assert.equal(change.verdictMailboxPendingCount(mailbox), 0)
})

test('WHAT[DELEG-014] VERDICT_MAILBOX_idle_empty_returns_empty_sentinel', async () => {
  const mailbox = change.createVerdictMailbox()
  const out = await change.verdictMailboxJoinAvailable(mailbox, 8, change.createVerdictInterrupt())
  assert.equal(out.kind, 'ResultsAvailable')
  assert.equal(out.count, 1)
  assert.equal(out.verdicts[0].kind, 'Empty')
  assert.equal(change.verdictMailboxPendingCount(mailbox), 0)
})

test('WHAT[DELEG-014] VERDICT_MAILBOX_batch_is_capped_at_maxjoinbatch_fifo_with_exact_remainder', async () => {
  const cap = change.verdictMaxBatch()
  assert.ok(Number.isInteger(cap) && cap > 0, 'cap must come from the compiled JoinBatch')
  const mailbox = change.createVerdictMailbox()
  const total = cap + 3
  for (let i = 0; i < total; i += 1) {
    change.verdictMailboxStartJob(mailbox)
    change.verdictMailboxPublish(mailbox, published(`job-${i}`, `head-${i}`))
  }
  assert.equal(change.verdictMailboxPendingCount(mailbox), total, 'every publish must reach the queue exactly once')

  const first = await change.verdictMailboxJoinAvailable(mailbox, 1000, change.createVerdictInterrupt())
  assert.equal(first.kind, 'ResultsAvailable')
  assert.equal(first.count, cap)
  assert.deepEqual(
    first.verdicts.map((v) => v.detail),
    Array.from({ length: cap }, (_, i) => `head-${i}`),
    'capped batch must preserve FIFO order',
  )
  assert.equal(change.verdictMailboxPendingCount(mailbox), 3)

  const second = await change.verdictMailboxJoinAvailable(mailbox, 1000, change.createVerdictInterrupt())
  assert.equal(second.kind, 'ResultsAvailable')
  assert.equal(second.count, 3)
  assert.deepEqual(
    second.verdicts.map((v) => v.detail),
    [`head-${cap}`, `head-${cap + 1}`, `head-${cap + 2}`],
    'both batches together must deliver every verdict exactly once in order',
  )
  assert.equal(change.verdictMailboxPendingCount(mailbox), 0)
})
