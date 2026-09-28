import assert from 'node:assert/strict'
import test from 'node:test'
import * as change from '../../../dist/Change/Surface.js'

test('WHAT[change-integration-014] real publish program invalidates a stale certificate before acquiring the gate', async () => {
  const observation = await change.observeRelayProgram('stale-certificate')
  assert.deepEqual(observation.invalidations, ['WorkspaceChangedAfterAssessment'])
  assert.deepEqual(observation.continuations, ['surface-loop-1'])
  assert.equal(observation.ffCalls, 0)
  assert.equal(observation.gateAcquireCount, 0)
  assert.deepEqual(observation.facts, [])
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
