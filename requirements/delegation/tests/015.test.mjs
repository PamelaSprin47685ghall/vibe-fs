import assert from 'node:assert/strict'
import test from 'node:test'
import * as join from '../../../dist/Execution/Delegation/Fork/OpenCode/JoinSurface.js'

const forkOne = async (probe) => {
  const placed = await join.joinProbeForkPty(probe, 'exit 0')
  assert.equal(placed.ok, true)
  return placed.ptyId
}
const quiescent = probe => {
  const counts = join.joinProbeCounts(probe)
  assert.equal(counts.pendingCompletions, 0)
  assert.equal(counts.pendingPtys, 0)
  assert.equal(counts.pendingRuns, 0)
}

test('WHAT[delegation-015] interrupted prose distinguishes user input, operator abort and deadline', () => {
  assert.match(join.renderInterrupted('english', 'UserMessageArrived'), /Something nearer has arrived/)
  assert.match(join.renderInterrupted('english', 'DeadlineExpired'), /waiting ended/)
  assert.match(join.renderInterrupted('english', 'OperatorAbort'), /waiting was interrupted/)
  for (const reason of ['UserMessageArrived', 'DeadlineExpired', 'OperatorAbort']) {
    assert.doesNotMatch(join.renderInterrupted('english', reason), /error|failed/i)
  }
})

test('WHAT[delegation-015] spurious wake burst still delivers the actual queued completion exactly once', async () => {
  const probe = join.createJoinProbe()
  const id = await forkOne(probe)
  const pending = join.joinAvailable(probe, 8, join.createJoinInterrupt())
  for (let sent = 0; sent < 5000; sent += 1) {
    join.joinProbePulseWake(probe)
    await Promise.resolve()
  }
  join.joinProbeCompletePty(probe, id)
  const result = await pending
  assert.equal(result.kind, 'ResultsAvailable')
  assert.equal(result.count, 1)
  assert.deepEqual(result.ptyIds, [id])
  quiescent(probe)
  assert.equal(join.joinProbeCounts(probe).ptyRuns, 0)
  assert.equal((await join.joinAvailable(probe, 8, join.createJoinInterrupt())).error, 'NothingToJoin')
})

test('WHAT[delegation-015] every interruption releases the wait while the child remains available for completion', async () => {
  for (const reason of ['UserMessageArrived', 'DeadlineExpired', 'OperatorAbort']) {
    const probe = join.createJoinProbe()
    const id = await forkOne(probe)
    const interrupt = join.createJoinInterrupt()
    const pending = join.joinAvailable(probe, 4, interrupt)
    join.fireJoinInterrupt(interrupt, reason)
    const result = await pending
    assert.equal(result.kind, 'Interrupted')
    assert.equal(result.reason, reason)
    assert.equal(join.joinProbeCounts(probe).ptyRuns, 1)
    join.joinProbeCompletePty(probe, id)
    const after = await join.joinAvailable(probe, 4, join.createJoinInterrupt())
    assert.equal(after.kind, 'ResultsAvailable')
    assert.deepEqual(after.ptyIds, [id])
    quiescent(probe)
  }
})

test('WHAT[delegation-015] actual queue errors and competing waits release the single-flight lock', async () => {
  for (const [limit, error] of [[0, 'Empty'], [4, 'NothingToJoin']]) {
    const probe = join.createJoinProbe()
    assert.equal((await join.joinAvailable(probe, limit, join.createJoinInterrupt())).error, error)
    const id = await forkOne(probe)
    join.joinProbeCompletePty(probe, id)
    assert.deepEqual((await join.joinAvailable(probe, 4, join.createJoinInterrupt())).ptyIds, [id])
    quiescent(probe)
  }
  const cancelledProbe = join.createJoinProbe()
  await forkOne(cancelledProbe)
  const pending = join.joinAvailable(cancelledProbe, 4, join.createJoinInterrupt())
  join.joinProbeCancel(cancelledProbe)
  assert.equal((await pending).error, 'Cancelled')
  assert.equal((await join.joinAvailable(cancelledProbe, 4, join.createJoinInterrupt())).error, 'Cancelled')
  quiescent(cancelledProbe)

  const probe = join.createJoinProbe()
  const id = await forkOne(probe)
  const first = join.joinAvailable(probe, 4, join.createJoinInterrupt())
  assert.equal((await join.joinAvailable(probe, 4, join.createJoinInterrupt())).error, 'JoinInProgress')
  join.joinProbeCompletePty(probe, id)
  assert.deepEqual((await first).ptyIds, [id])
  assert.equal((await join.joinAvailable(probe, 4, join.createJoinInterrupt())).error, 'NothingToJoin')
  quiescent(probe)
})

test('WHAT[delegation-015] permit gate rejects missing journal without poisoning later ordinary join', async () => {
  const probe = join.createJoinProbe()
  assert.equal((await join.joinAvailableWithPermit(probe, 0, 4, join.createJoinInterrupt())).error, 'NotFound')
  const id = await forkOne(probe)
  join.joinProbeCompletePty(probe, id)
  assert.deepEqual((await join.joinAvailable(probe, 4, join.createJoinInterrupt())).ptyIds, [id])
  quiescent(probe)
})

test.todo('WHAT[delegation-015] real Host input and cancellation reach join without cancelling the agent or revoking its authority (GAP-153)')

{
const change = await import('../../../dist/Change/Surface.js')
const assert = (await import('node:assert/strict')).default
const published = (jobId, head) => ({ kind: 'Published', jobId, head })

test('WHAT[delegation-015] interrupted commission join leaves the job active and the next waiter receives its verdict once', async () => {
  const mailbox = change.createVerdictMailbox()
  change.verdictMailboxStartJob(mailbox)
  const interrupt = change.createVerdictInterrupt()
  const pending = change.verdictMailboxJoinAvailable(mailbox, 8, interrupt)
  change.fireVerdictInterrupt(interrupt, 'UserMessageArrived')
  const out = await pending
  assert.equal(out.kind, 'Interrupted')
  assert.equal(out.reason, 'UserMessageArrived')
  const next = change.verdictMailboxJoinAvailable(mailbox, 8, change.createVerdictInterrupt())
  change.verdictMailboxPublish(mailbox, published('job-2', 'head-2'))
  const after = await next
  assert.equal(after.kind, 'ResultsAvailable')
  assert.equal(after.count, 1)
  assert.equal(after.verdicts[0].kind, 'Published')
  assert.equal(after.verdicts[0].detail, 'head-2')
  assert.equal(change.verdictMailboxPendingCount(mailbox), 0)
})

test('WHAT[delegation-015] a queued or racing commission verdict is consumed before reporting a join interruption', async () => {
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
    assert.equal(out.kind, 'ResultsAvailable', order)
    assert.equal(out.count, 1)
    assert.equal(out.verdicts[0].kind, 'Published')
    assert.equal(out.verdicts[0].detail, 'head-1')
    assert.equal(change.verdictMailboxPendingCount(mailbox), 0, order)
  }
})
}
