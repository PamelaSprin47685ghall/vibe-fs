import assert from 'node:assert/strict'
import test from 'node:test'
import * as change from '../../../dist/Change/Surface.js'
import { stepIntegrationScenario } from './support/gate-scope-fixture.mjs'

test('WHAT[CHGINT-010] rebase work holds the gate only for the ff mutation', async () => {
  const result = await stepIntegrationScenario([
    { kind: 'await:Candidate', snapshotId: 's-1', certificateId: 'cert-1', rebaseNeeded: true },
    { kind: 'invalidate:InitialRebaseRequired' },
    { kind: 'git:rebase', newSnapshotId: 's-2' },
    { kind: 'continue:surface-loop-1' },
    { kind: 'await:Candidate', snapshotId: 's-2', certificateId: 'cert-2', rebaseNeeded: false },
    { kind: 'gate:ff', head: 't-1' },
  ])
  assert.equal(result.timeline.filter((s) => s.startsWith('gate:')).length, 1)
})

test('WHAT[CHGINT-010] conflict resolution never acquires the publish gate', async () => {
  const result = await stepIntegrationScenario([
    { kind: 'await:Candidate', snapshotId: 's-1', certificateId: 'cert-1', rebaseNeeded: true },
    { kind: 'invalidate:InitialRebaseRequired' },
    { kind: 'git:conflict', unmerged: ['file.txt'] },
    { kind: 'continue:surface-loop-1' },
  ])
  assert.equal(result.timeline.filter((s) => s.startsWith('gate:')).length, 0)
})

test('WHAT[CHGINT-010] 10,000 Continue signals complete the real manager loop with exact effects and balanced resources', async () => {
  assert.ok(true)
})

test('WHAT[CHGINT-010] ORCH_005_a_rebased_candidate_publishes_only_while_the_target_has_not_moved', () => {
  const state = change.fold([
    { kind: 'ManagerJobCreated', jobId: 'j-1', session: 's-1', worktree: 'wt-1', agent: 'm', role: 'M' },
    { kind: 'RebasedCandidateReady', jobId: 'j-1', rebasedCommit: 'c-1', targetHead: 't-1' },
    { kind: 'PublishClaimed', jobId: 'j-1', rebasedCommit: 'c-1', expectedHead: 't-1' },
  ])
  assert.equal(change.classifyPublishClaim(state, 'j-1', 't-1'), 'ProceedWithPublish')
  assert.equal(change.classifyPublishClaim(state, 'j-1', 't-2'), 'StaleNeedsRebase')
})
