// requirements/knowledge-reuse/tests/015.test.mjs
//
// Laws: KNOWLEDGE-REUSE-015, KNOWLEDGE-REUSE-004
// Scenarios T24-T27: Abolishing strict replay & stability loops, and DevOps fix updating baselines.

import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

import * as eventStore from '../../../dist/Persistence/EventStore/Surface.js'
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

test('WHAT[KNOWLEDGE-REUSE-004] T27_devops_self_repair_updates_related_case_maintenance_baseline_without_fake_engineer_source', async () => {
  // DevOps repair updates related cases from B -> C, but does NOT become an Engineer case source.
  assert.equal(typeof casebook.applyExternalChangeToCase, 'function', 'casebook must apply external change B->C to existing case')
  const updated = casebook.applyExternalChangeToCase({
    identity: 'eng-1',
    completionFileState: 'state-B',
    maintenanceFileState: 'state-B',
    diff: 'B -> C',
    newState: 'state-C',
  })
  assert.equal(updated.completionFileState, 'state-B')
  assert.equal(updated.maintenanceFileState, 'state-C')
  assert.equal(updated.sourceRole, 'engineer', 'must retain original engineer source, not forge devops source')
})
