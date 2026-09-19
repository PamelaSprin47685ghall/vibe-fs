// requirements/knowledge-reuse/tests/015.test.mjs
//
// Laws: KNOWLEDGE-REUSE-015

import assert from 'node:assert/strict'
import test from 'node:test'

import * as casebook from '../../../dist/Repository/Knowledge/Casebook/Surface.js'

test('WHAT[KNOWLEDGE-REUSE-015] T24_T25_abolishes_strict_observation_replay_and_stability_verification_loops', async () => {
  // Proves that casebook does NOT require strict replay equality or replay-before / replay-after loops.
  // Instead, diff-driven migration is performed in a single step.
  assert.equal(typeof casebook.singlePassDiffRefresh, 'function', 'casebook must support singlePassDiffRefresh without stability loop')
  const result = await casebook.singlePassDiffRefresh({
    caseId: 'case-1',
    maintenanceBaseline: 'state-B',
    targetState: 'state-T',
    diff: '+change',
  })
  assert.equal(result.performedReplayLoop, false, 'must NOT perform replay stability loops')
})
