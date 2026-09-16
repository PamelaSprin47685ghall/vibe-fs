import assert from 'node:assert/strict'
import test from 'node:test'
import * as codec from '../../../dist/Persistence/Journal/FactCodecSurface.js'
import * as change from '../../../dist/Change/Surface.js'

const WT = 'wt-effect-010'
const fold = (facts) => change.fold(facts)

const managerCreated = {
  kind: 'ManagerJobCreated',
  jobId: 'job-1',
  session: 'ses-mgr-1',
  worktree: WT,
  agent: 'manager',
  role: 'Manager',
}

const requested = {
  kind: 'WorktreeCreateRequested',
  jobId: 'job-1',
  worktree: WT,
}

const created = {
  kind: 'WorktreeCreated',
  jobId: 'job-1',
  worktree: WT,
}

const rebased = {
  kind: 'RebasedCandidateReady',
  jobId: 'job-1',
  rebasedCommit: 'c-rebased',
  targetHead: 'c-target-head',
}

const claimed = {
  kind: 'PublishClaimed',
  jobId: 'job-1',
  rebasedCommit: 'c-rebased',
  expectedHead: 'c-target-head',
}

test('WHAT[EFFECT-ACCOUNTING-010] PERSIST_005_pre050_marker_refuses_with_migration_message', () => {
  const markers = [
    'FailuresOnCurrentSide',
    'IsDead',
    'BaseModelID',
    'AgentLinked',
    'OrchestratorPublished',
    'EnforcementCycleCommitted',
    'DurableEffectRequested',
    'DurableEffectAccepted',
  ]
  for (const marker of markers) {
    const payload = JSON.stringify({ legacyKey: marker, data: 42 })
    const result = codec.decode(payload)
    assert.equal(result.ok, false, `marker ${marker} must be rejected`)
    assert.equal(result.error, codec.pre050MigrationMessage, `marker ${marker} must produce pre050MigrationMessage`)
  }
})

test('WHAT[EFFECT-ACCOUNTING-010] typed_effect_facts_replace_the_generic_durable_effect_union', () => {
  assert.match(change.FACT_TYPES_SOURCE, /WorktreeCreateRequested/)
  assert.match(change.FACT_TYPES_SOURCE, /WorktreeCreated/)
  assert.match(change.FACT_TYPES_SOURCE, /PublishClaimed/)
  assert.match(change.FACT_TYPES_SOURCE, /RebasedCandidateReady/)
  assert.match(change.FACT_TYPES_SOURCE, /ManagerJobCreated/)
  assert.doesNotMatch(change.FACT_TYPES_SOURCE, /DurableEffectRequested|DurableEffectAccepted/)
  const projection = fold([managerCreated, requested, created, rebased, claimed])
  assert.equal(change.worktreeEffect(projection, WT), 'Created')
  assert.match(change.FACT_CODEC_SOURCE, /pre.?050|migration|unsupported/i)
})
