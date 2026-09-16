// Join wake ownership and resource bounds through the compiled JoinSurface probe.
// Every outcome below is the real Fable HostForkJoin/Join decision over an
// in-memory session port, in-memory PtyPort handler, and the production
// CompletionMailbox: no Join is modelled here and no source is inspected.
import assert from 'node:assert/strict'
import test from 'node:test'
import * as join from '../../../dist/Execution/Delegation/Fork/OpenCode/JoinSurface.js'

const WAKE_BURST = 5000

const forkOne = async (probe, command = 'exit 0') => {
  const placed = await join.joinProbeForkPty(probe, command)
  assert.equal(placed.ok, true, `fork must place a PTY, got ${placed.error}`)
  assert.match(placed.ptyId, /^pty-/)
  return placed.ptyId
}

const quiescentMailbox = (probe) => {
  const counts = join.joinProbeCounts(probe)
  assert.equal(counts.pendingCompletions, 0, 'mailbox completion queue must drain')
  assert.equal(counts.pendingPtys, 0, 'mailbox PTY queue must drain')
  assert.equal(counts.pendingRuns, 0, 'no agent runs may linger')
  return counts
}

test('WHAT[DELEG-015] JOIN_WAKE_spurious_wake_burst_still_delivers_exact_completion', async () => {
  const probe = join.createJoinProbe()
  const ptyId = await forkOne(probe)
  const interrupt = join.createJoinInterrupt()
  const pending = join.joinAvailable(probe, 8, interrupt)

  for (let sent = 0; sent < WAKE_BURST; sent += 1) {
    join.joinProbePulseWake(probe)
    await Promise.resolve()
  }

  join.joinProbeCompletePty(probe, ptyId)
  const out = await pending
  assert.equal(out.kind, 'ResultsAvailable')
  assert.equal(out.count, 1)
  assert.deepEqual(out.ptyIds, [ptyId])

  const counts = quiescentMailbox(probe)
  assert.equal(counts.ptyRuns, 0, 'delivered PTY tracks must release')

  const idle = join.createJoinInterrupt()
  const after = await join.joinAvailable(probe, 8, idle)
  assert.equal(after.kind, 'Error')
  assert.equal(after.error, 'NothingToJoin')
})

test('WHAT[DELEG-015] JOIN_WAKE_batch_is_capped_at_maxjoinbatch_with_exact_remainder', async () => {
  const cap = join.joinMaxBatch()
  assert.ok(Number.isInteger(cap) && cap > 0, 'cap must come from the compiled JoinBatch')
  const probe = join.createJoinProbe()
  const ids = []
  for (let i = 0; i < cap + 8; i += 1) {
    ids.push(await forkOne(probe, `exit ${i}`))
  }
  for (const id of ids) {
    join.joinProbeCompletePty(probe, id)
  }
  assert.equal(
    join.joinProbeCounts(probe).pendingCompletions,
    cap + 8,
    'every physical completion must reach the mailbox exactly once',
  )

  const first = await join.joinAvailable(probe, 1000, join.createJoinInterrupt())
  assert.equal(first.kind, 'ResultsAvailable')
  assert.equal(first.count, cap)
  assert.equal(new Set(first.ptyIds).size, cap, 'capped batch must carry distinct completions')
  assert.equal(join.joinProbeCounts(probe).pendingCompletions, 8)

  const second = await join.joinAvailable(probe, 1000, join.createJoinInterrupt())
  assert.equal(second.kind, 'ResultsAvailable')
  assert.equal(second.count, 8)
  assert.deepEqual(
    [...first.ptyIds, ...second.ptyIds].sort(),
    [...ids].sort(),
    'both batches together must deliver every completion exactly once',
  )
  quiescentMailbox(probe)
})

test('WHAT[DELEG-015] JOIN_WAKE_interrupt_stays_distinct_from_failure_and_releases_lock', async () => {
  const probe = join.createJoinProbe()
  const ptyId = await forkOne(probe)
  const interrupt = join.createJoinInterrupt()
  const pending = join.joinAvailable(probe, 4, interrupt)
  join.fireJoinInterrupt(interrupt, 'UserMessageArrived')
  const out = await pending
  assert.equal(out.kind, 'Interrupted')
  assert.equal(out.reason, 'UserMessageArrived')
  assert.notEqual(out.kind, 'Error', 'interrupt must never masquerade as failure')

  join.joinProbeCompletePty(probe, ptyId)
  const after = await join.joinAvailable(probe, 4, join.createJoinInterrupt())
  assert.equal(after.kind, 'ResultsAvailable')
  assert.equal(after.count, 1)
  assert.deepEqual(after.ptyIds, [ptyId])

  const expiring = await forkOne(probe)
  const deadline = join.createJoinInterrupt()
  const waiting = join.joinAvailable(probe, 4, deadline)
  join.fireJoinInterrupt(deadline, 'DeadlineExpired')
  const expired = await waiting
  assert.equal(expired.kind, 'Interrupted')
  assert.equal(expired.reason, 'DeadlineExpired')
  join.joinProbeCompletePty(probe, expiring)
  const drained = await join.joinAvailable(probe, 4, join.createJoinInterrupt())
  assert.equal(drained.count, 1)
  quiescentMailbox(probe)
})

test('WHAT[DELEG-015] JOIN_WAKE_cancel_and_error_paths_release_lock_for_next_join', async () => {
  const emptyProbe = join.createJoinProbe()
  const empty = await join.joinAvailable(emptyProbe, 0, join.createJoinInterrupt())
  assert.equal(empty.kind, 'Error')
  assert.equal(empty.error, 'Empty')
  const recovered = await forkOne(emptyProbe)
  join.joinProbeCompletePty(emptyProbe, recovered)
  const afterEmpty = await join.joinAvailable(emptyProbe, 4, join.createJoinInterrupt())
  assert.equal(afterEmpty.kind, 'ResultsAvailable')
  assert.equal(afterEmpty.count, 1)

  const losingProbe = join.createJoinProbe()
  const losing = await join.joinAvailable(losingProbe, 4, join.createJoinInterrupt())
  assert.equal(losing.kind, 'Error')
  assert.equal(losing.error, 'NothingToJoin')
  const revived = await forkOne(losingProbe)
  join.joinProbeCompletePty(losingProbe, revived)
  const afterLosing = await join.joinAvailable(losingProbe, 4, join.createJoinInterrupt())
  assert.equal(afterLosing.kind, 'ResultsAvailable')
  assert.deepEqual(afterLosing.ptyIds, [revived])

  const cancelProbe = join.createJoinProbe()
  await forkOne(cancelProbe)
  const cancelled = join.joinAvailable(cancelProbe, 4, join.createJoinInterrupt())
  join.joinProbeCancel(cancelProbe)
  const cancelOut = await cancelled
  assert.equal(cancelOut.kind, 'Error')
  assert.equal(cancelOut.error, 'Cancelled')
  const afterCancel = await join.joinAvailable(cancelProbe, 4, join.createJoinInterrupt())
  assert.equal(afterCancel.kind, 'Error')
  assert.equal(afterCancel.error, 'Cancelled')
  assert.notEqual(afterCancel.error, 'JoinInProgress', 'cancel must release the single-flight lock')
  quiescentMailbox(cancelProbe)

  const flightProbe = join.createJoinProbe()
  const flightId = await forkOne(flightProbe)
  const first = join.joinAvailable(flightProbe, 4, join.createJoinInterrupt())
  const second = await join.joinAvailable(flightProbe, 4, join.createJoinInterrupt())
  assert.equal(second.kind, 'Error')
  assert.equal(second.error, 'JoinInProgress')
  join.joinProbeCompletePty(flightProbe, flightId)
  const won = await first
  assert.equal(won.kind, 'ResultsAvailable')
  assert.deepEqual(won.ptyIds, [flightId])
  const released = await join.joinAvailable(flightProbe, 4, join.createJoinInterrupt())
  assert.equal(released.kind, 'Error')
  assert.equal(released.error, 'NothingToJoin')
})

test('WHAT[DELEG-015] JOIN_WAKE_permit_gate_fails_closed_and_keeps_runtime_usable', async () => {
  const probe = join.createJoinProbe()
  const refused = await join.joinAvailableWithPermit(probe, 0, 4, join.createJoinInterrupt())
  assert.equal(refused.kind, 'Error')
  assert.equal(refused.error, 'NotFound', 'permit join without a journal must fail closed')

  const ptyId = await forkOne(probe)
  join.joinProbeCompletePty(probe, ptyId)
  const out = await join.joinAvailable(probe, 4, join.createJoinInterrupt())
  assert.equal(out.kind, 'ResultsAvailable')
  assert.deepEqual(out.ptyIds, [ptyId])
  quiescentMailbox(probe)
})
