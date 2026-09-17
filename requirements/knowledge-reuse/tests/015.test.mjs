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

test('WHAT[KNOWLEDGE-REUSE-004] dual_baselines_freeze_pipeline_and_refresh_maintenance_evolution', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-kr-dual-baseline-'))
  const store = eventStore.create(dir, 'kr-dual-baseline-writer')
  try {
    const filePath = join(dir, 'code.fs')
    writeFileSync(filePath, 'let initial = 1', 'utf8')

    // 1. Freeze baseline via store
    const baselineJson = await casebook.freezeCompletionState(store, dir, ['code.fs', 'nonexistent.fs'])
    assert.equal(typeof baselineJson, 'string')
    const parsed = JSON.parse(baselineJson)

    // Code file is Present with payloadRef and sha256
    assert.equal(parsed['code.fs']?.kind, 'Present')
    assert.ok(parsed['code.fs']?.payloadRef)
    assert.equal(parsed['code.fs']?.sha256, casebook.contentHash('let initial = 1'))

    // Nonexistent file is Missing
    assert.equal(parsed['nonexistent.fs']?.kind, 'Missing')

    // Read payload from store to verify durable persistence
    const payloadBytes = await eventStore.readPayload(store, parsed['code.fs'].payloadRef)
    assert.ok(payloadBytes, 'payload must be readable from store')
    const readText = new TextDecoder().decode(payloadBytes)
    assert.equal(readText, 'let initial = 1')

    // 2. Finalize case with this baseline
    const identity = 'test-dual-case-1'
    const finalizeRes = await casebook.finalizeEngineerCase(
      store,
      identity,
      'trace-1',
      'How to implement?',
      'Answer initial',
      ['code.fs', 'nonexistent.fs'],
      baselineJson
    )
    assert.equal(finalizeRes.kind, 'finalized')

    // Fetch case and assert dual baselines are identical at completion
    const initialCase = await casebook.fetchCaseByIdentity(store, identity)
    assert.equal(initialCase.completionFileState, baselineJson)
    assert.equal(initialCase.maintenanceFileState, baselineJson)

    // 3. External modification triggers maintenance drift
    writeFileSync(filePath, 'let initial = 2 // modified', 'utf8')

    // Compute diff against baseline
    const diffObj = await casebook.computeMaintenanceDiff(dir, baselineJson)
    assert.equal(diffObj.hasDiff, true)

    // Freeze new target state
    const newTargetJson = await casebook.freezeCompletionState(store, dir, ['code.fs', 'nonexistent.fs'])
    const parsedNew = JSON.parse(newTargetJson)
    assert.notEqual(parsedNew['code.fs'].payloadRef, parsed['code.fs'].payloadRef)
    assert.equal(parsedNew['code.fs'].sha256, casebook.contentHash('let initial = 2 // modified'))

    // Refresh case with new maintenance baseline
    const refreshRes = await casebook.refreshWithDiff(
      store,
      identity,
      diffObj.diffSummary,
      newTargetJson,
      'How to implement?',
      'Answer maintained'
    )
    assert.equal(refreshRes.ok, true)

    // 4. Assert: completionFileState is unchanged, maintenanceFileState is updated to new blob baseline
    const maintainedCase = await casebook.fetchCaseByIdentity(store, identity)
    assert.equal(maintainedCase.completionFileState, baselineJson, 'completionFileState must NEVER be changed')
    assert.equal(maintainedCase.maintenanceFileState, newTargetJson, 'maintenanceFileState must advance to new baseline')
    assert.equal(maintainedCase.a, 'Answer maintained')
  } finally {
    eventStore.dispose(store)
    rmSync(dir, { recursive: true, force: true })
  }
})
