import assert from 'node:assert/strict'
import test from 'node:test'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import {
  interruptAttemptAdapterProbe,
  interruptRejectedAdapterProbe,
  interruptTerminatedAdapterProbe,
} from '../../../dist/OpenCode/Host/SessionsSurface.js'

const ROOT = fileURLToPath(new URL('../../..', import.meta.url))

const read = (path) => readFileSync(join(ROOT, path), 'utf8')

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

test('WHAT[managed-session-lifecycle-016] managed interrupt Host rejection is terminal after exactly one AbortSession attempt', async () => {
  const observed = await interruptRejectedAdapterProbe()

  assert.equal(observed.outcome, 'Error')
  assert.equal(observed.error, 'controlled Host rejected AbortSession')
  assert.equal(observed.attemptsBeforeRejection, 1)
  assert.equal(observed.abortAttempts, 1)
  assert.deepEqual(observed.abortedSessionIds, ['adapter-rejected-child'])
  assert.deepEqual(Array.from(observed.virtualTimes), [0, 10, 1000])
  assert.deepEqual(observed.trace, [
    't=0 AbortSession(adapter-rejected-child)',
    't=10 Error(controlled Host rejected AbortSession)',
    't=1000 quiescent attempts=1',
  ])
})

test('WHAT[managed-session-lifecycle-016] Turn orchestration consumes typed outcome without cross-callback aborted registry PC', () => {
  const ordinary = read('src/Wanxiangshu/Composition/Turn/OrdinaryTurnWorkflow.fs')
  const workflow = read('src/Wanxiangshu/Composition/Turn/Workflow.fs')
  const observer = read('src/Wanxiangshu/OpenCode/Host/HostTurnObserver.fs')

  // OrdinaryTurnWorkflow and TurnWorkflow must not receive or use abortedSessions mutable HashSet
  assert.doesNotMatch(ordinary, /abortedSessions/)
  assert.doesNotMatch(workflow, /abortedSessions/)
  assert.doesNotMatch(observer, /scope\.Sessions\.AbortedSessions/)
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
