import assert from 'node:assert/strict'
import test from 'node:test'
import * as change from '../../../dist/Change/Surface.js'

test('WHAT[change-integration-014] real publish program refuses a candidate whose target head no longer matches the admitted rebased evidence', async () => {
  // The certificate is gone. The surviving machine fact is the target pin:
  // a rebased candidate is publishable only while the live head still equals
  // the admitted snapshot, and a moved head is refused before the gate.
  assert.equal(change.classifyRebasedCandidate('target-1', 'rebased-1', 'target-1').kind, 'PublishReady')
  assert.equal(change.classifyRebasedCandidate('target-2', 'rebased-1', 'target-1').kind, 'NeedsRebase')

  const observation = await change.observeRelayProgram('target-moved')
  assert.deepEqual(observation.continuations, ['surface-loop-1'])
  assert.equal(observation.ffCalls, 0)
  assert.equal(observation.gateAcquireCount, 0)
  assert.equal(observation.facts.includes('Published'), false)
  const rebase = observation.timeline.indexOf('git:rebase')
  const continuation = observation.timeline.indexOf('continue:surface-loop-1')
  assert.ok(rebase >= 0 && continuation > rebase)
})

test('WHAT[change-integration-014] real publish program refuses a perfect candidate with unmerged files', async () => {
  const observation = await change.observeRelayProgram('artifact-conflict')
  assert.deepEqual(observation.facts, ['ConflictDetected'])
  assert.equal(observation.ffCalls, 0)
  assert.equal(observation.gateAcquireCount, 0)
})

test('WHAT[change-integration-014] claim recovery cannot publish a different workspace snapshot', async () => {
  const observation = await change.observeRelayProgram('reentry-wrong-snapshot')
  assert.equal(observation.ffCalls, 0)
  assert.deepEqual(observation.ffPinnedCandidates, [])
  assert.equal(observation.facts.includes('Published'), false)
})
