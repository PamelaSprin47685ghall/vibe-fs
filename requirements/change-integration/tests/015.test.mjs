import assert from 'node:assert/strict'
import test from 'node:test'
import * as change from '../../../dist/Change/Surface.js'

test('WHAT[change-integration-015] actual publish program requests a new review when the observed target no longer matches the admitted evidence', async () => {
  // Workspace snapshots and certificates are retired. A moved target is the
  // surviving mismatch: the program rebases outside the gate and continues
  // into the next tenure instead of publishing on the old evidence.
  const changed = await change.observeRelayProgram('target-moved')
  assert.deepEqual(changed.continuations, ['surface-loop-1'])
  assert.equal(changed.ffCalls, 0)
  assert.equal(changed.facts.includes('RebasedCandidateReady'), true)
  const rebase = changed.timeline.indexOf('git:rebase')
  const recorded = changed.timeline.indexOf('fact:RebasedCandidateReady')
  const continuation = changed.timeline.indexOf('continue:surface-loop-1')
  assert.ok(rebase >= 0 && recorded > rebase && continuation > recorded)
  const unchanged = await change.observeRelayProgram('fresh')
  assert.equal(unchanged.verdict.kind, 'Published')
  assert.equal(unchanged.ffCalls, 1)
})

test.todo('WHAT[change-integration-015] GAP-212: actual Engineer or DevOps mutation invalidates the prior test evidence and a new independent assessment authorizes only the new snapshot')
