import assert from 'node:assert/strict'
import test from 'node:test'
import {
  interruptAttemptAdapterProbe,
  interruptRejectedAdapterProbe,
  interruptTerminatedAdapterProbe,
} from '../../../dist/OpenCode/Host/SessionsSurface.js'

test('WHAT[managed-session-lifecycle-016] Sessions adapter rejects root attempt interrupt and physically aborts a managed child exactly once', async () => {
  const observed = await interruptAttemptAdapterProbe()

  assert.equal(observed.created, true)
  assert.equal(observed.creationError, null)
  assert.equal(observed.rootRejected, true)
  assert.equal(
    observed.rootError,
    'MANAGED-SESSION-016: user-facing/root session may only be interrupted by the external user',
  )
  assert.equal(observed.transportCallsAfterRoot, 0)
  assert.equal(observed.childInterrupted, true)
  assert.equal(observed.transportCallsAfterChild, 1)
  assert.deepEqual(observed.abortedSessionIds, ['adapter-child'])
  assert.equal(observed.childStillManagedAfterInterrupt, true)
})

test('WHAT[managed-session-lifecycle-016] managed interrupt returns the held Host rejection after one AbortSession attempt', async () => {
  const observed = await interruptRejectedAdapterProbe()

  assert.equal(observed.outcome, 'Error')
  assert.equal(observed.error, 'controlled Host rejected AbortSession')
  assert.equal(observed.attemptsBeforeRejection, 1)
  assert.equal(observed.abortAttempts, 1)
  assert.deepEqual(observed.abortedSessionIds, ['adapter-rejected-child'])
})

test('WHAT[managed-session-lifecycle-016] already-terminal attempt interrupt is Ok and issues transport abort for root session', async () => {
  const observed = await interruptTerminatedAdapterProbe()

  assert.equal(observed.terminatedOutcome, 'Ok')
  assert.equal(observed.terminatedError, '')
  assert.equal(observed.abortsAfterTerminal, 1)
  assert.deepEqual(observed.abortedSessionIds, ['term-child'])
  assert.equal(observed.abortCount, 1)
  // Control: a non-terminal non-managed id is still rejected, also with zero transport calls.
  assert.equal(observed.otherOutcome, 'Error')
  assert.equal(
    observed.otherError,
    'MANAGED-SESSION-016: user-facing/root session may only be interrupted by the external user',
  )
})
