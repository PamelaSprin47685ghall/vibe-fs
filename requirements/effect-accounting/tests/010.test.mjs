import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { readFileSync } = await import("node:fs");
const change = await import("../../../dist/Change/Surface.js");

const FACT_CODEC_SOURCE = readFileSync(new URL('../../../src/Wanxiangshu/Persistence/Journal/FactCodec.fs', import.meta.url), 'utf8')
const FACT_TYPES_SOURCE = readFileSync(new URL('../../../src/Wanxiangshu/Change/Facts.fs', import.meta.url), 'utf8')
const JOB = 'job_ea'
const WT = 'wt_ea'
const WT_PATH = '/tmp/wt_ea'
const baseJob = {
  jobId: JOB,
  managerSessionId: 'ses_ea',
  managerAgent: 'manager',
  byname: 'Road',
  worktreeIdentity: WT,
  worktreePath: WT_PATH,
  targetRef: 'refs/heads/main',
  targetBranchFrozen: 'refs/heads/main',
}
const createJob = () => change.createJob(change.empty(), baseJob)
const progress = (kind, payload) => ({ kind, payload })
const managerCreated = { kind: 'ManagerJobCreated', payload: baseJob }
const requested = { kind: 'WorktreeCreateRequested', payload: { jobId: JOB, worktreeIdentity: WT, worktreePath: WT_PATH } }
const created = { kind: 'WorktreeCreated', payload: { jobId: JOB, worktreeIdentity: WT, worktreePath: WT_PATH } }
const rebased = {
  kind: 'RebasedCandidateReady',
  payload: { jobId: JOB, rebasedCommit: 'r1', targetHeadSnapshot: 'h1', workspaceSnapshotId: 'snap_2' },
}
const claimed = {
  kind: 'PublishClaimed',
  payload: {
    jobId: JOB,
    targetRef: 'refs/heads/main',
    rebasedCommit: 'r1',
    expectedHead: 'h1',
    workspaceSnapshotId: 'snap_2',
    qualityCertificateId: 'cert_ea',
    authorityRevision: 'rev_ea',
  },
}
const fold = (events) => {
  const result = change.fold(events)
  assert.equal(result.ok, true, result.error ?? '')
  return change.unwrapFold(result)
}

test('WHAT[effect-accounting-010] typed_effect_facts_replace_the_generic_durable_effect_union', () => {
  assert.match(FACT_TYPES_SOURCE, /WorktreeCreateRequested/)
  assert.match(FACT_TYPES_SOURCE, /WorktreeCreated/)
  assert.match(FACT_TYPES_SOURCE, /PublishClaimed/)
  assert.match(FACT_TYPES_SOURCE, /RebasedCandidateReady/)
  assert.match(FACT_TYPES_SOURCE, /ManagerJobCreated/)
  assert.doesNotMatch(FACT_TYPES_SOURCE, /DurableEffectRequested|DurableEffectAccepted/)
  const projection = fold([managerCreated, requested, created, rebased, claimed])
  assert.equal(change.worktreeEffect(projection, WT), 'Created')
  assert.match(FACT_CODEC_SOURCE, /pre.?050|migration|unsupported/i)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const codec = await import("../../../dist/Persistence/Journal/FactCodecSurface.js");


test('WHAT[effect-accounting-010] PERSIST_005_pre050_marker_refuses_with_migration_message', () => {
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
}
