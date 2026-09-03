import { test } from 'node:test'
import assert from 'node:assert/strict'
import { loadGecSurface } from './gec-support.mjs'

// WHAT[EPI-027]: OpenCode dispatch reuses the existing managed-session owner
// (delegation/fission, capacity, failure policy). Sphinx only describes which
// blind child to fork: common root snapshot, no sibling payload, a new child
// per retry without failure output, abort/drain intents, depth exactly one.
// The managed session below is a fake existing port: the proof is that every
// child points back at it and Sphinx keeps no private pool of its own.

// Fake existing managed-session port: stands in for the real IOpenCodePort
// owner. Plain data only; production resolves it to the live session.
const parentSession = { port: 'fake-managed-session-port', sessionId: 'sess_managed_001' }

const rootSnapshot = { hash: 'snap-root-001', messageId: 'msg_root_001' }

const siblings = [{ workId: 'work_sibling_002', payload: { answer: 'sibling answer must not leak' } }]

const dispatchInput = {
  workId: 'work_blind_001',
  attempt: 1,
  rootSnapshot,
  parentSession,
  siblings,
}

test('WHAT[EPI-027] blind_dispatch_forks_common_root_child_with_depth_one_and_new_child_per_retry', async () => {
  const gecSurface = await loadGecSurface()

  const first = await gecSurface.planOpenCodeDispatch(dispatchInput)
  assert.equal(first.error, undefined)
  // Child isolation: common root snapshot, parent is the managed session,
  // depth defaults to one, nothing from the sibling branch leaks in.
  assert.equal(first.child.parentSessionId, parentSession.sessionId)
  assert.equal(first.child.snapshotHash, rootSnapshot.hash)
  assert.equal(first.child.depth, 1)
  assert.equal(first.child.carriesSiblingPayload, false)
  assert.equal(first.child.carriesFailureOutput, false)
  assert.ok(typeof first.child.childSessionId === 'string' && first.child.childSessionId.length > 0)

  // Failed attempt retries under the same WorkId with a new attempt, a new
  // child and the ORIGINAL snapshot: failure output never propagates.
  const retry = await gecSurface.planOpenCodeRetry({
    workId: 'work_blind_001',
    failedAttempt: 1,
    nextAttempt: 2,
    failedOutput: { text: 'failed draft that must not propagate' },
    rootSnapshot,
    parentSession,
  })
  assert.equal(retry.error, undefined)
  assert.equal(retry.child.parentSessionId, parentSession.sessionId)
  assert.equal(retry.child.snapshotHash, rootSnapshot.hash)
  assert.equal(retry.child.depth, 1)
  assert.equal(retry.child.carriesSiblingPayload, false)
  assert.equal(retry.child.carriesFailureOutput, false)
  assert.notEqual(retry.child.childSessionId, first.child.childSessionId)
})

test('WHAT[EPI-027] abort_and_drain_terminate_dispatched_work_and_workers_cannot_recurse', async () => {
  const gecSurface = await loadGecSurface()

  const first = await gecSurface.planOpenCodeDispatch(dispatchInput)
  assert.equal(first.error, undefined)

  const aborted = await gecSurface.abortOpenCodeWork({
    workId: 'work_blind_001',
    childSessionId: first.child.childSessionId,
  })
  assert.equal(aborted.aborted, true)
  assert.equal(aborted.workId, 'work_blind_001')
  assert.equal(aborted.childSessionId, first.child.childSessionId)

  const drained = await gecSurface.drainOpenCodeHost({ parentSessionId: parentSession.sessionId })
  assert.equal(drained.drained, true)
  assert.equal(drained.pending, 0)

  // Default subagent depth is one: a worker child must not dispatch further.
  const recursive = await gecSurface.planOpenCodeDispatch({ ...dispatchInput, depth: 2 })
  assert.ok(recursive.error, 'depth beyond one must be refused, not forked')
  assert.match(recursive.error.code, /DEPTH_EXCEEDED/)
})
