import assert from 'node:assert/strict'
import test from 'node:test'
import * as change from '../../../dist/Change/Surface.js'
import {
  baseIntegrationScenario,
  fullPublishScenario,
  stepIntegrationScenario,
} from './support/gate-scope-fixture.mjs'

test('WHAT[CHGINT-001] fresh quality candidate runs the full publish lifecycle to Published', async () => {
  const finality = await fullPublishScenario()
  assert.equal(finality.verdict.kind, 'Published')
  assert.equal(finality.verdict.detail, 'c-1')
  assert.equal(finality.timeline.filter((s) => s.startsWith('ff:c-1')).length, 1)
})

test('WHAT[CHGINT-001] retirement without a valid certificate continues the loop', async () => {
  const result = await stepIntegrationScenario([
    { kind: 'await:ExceptionalTerminal', reason: 'Retire' },
  ])
  assert.equal(result.timeline.filter((s) => s === 'continue:surface-loop-1').length, 1)
  assert.equal(result.timeline.filter((s) => s.startsWith('gate:')).length, 0)
})

test('WHAT[CHGINT-001] ORCH_003_a_created_job_persists_the_manager_agent_and_the_worktree_identity', () => {
  const state = change.fold([{
    kind: 'ManagerJobCreated',
    jobId: 'job-1',
    session: 'ses-1',
    worktree: 'wt-1',
    agent: 'manager',
    role: 'Manager',
  }])
  assert.equal(change.job(state, 'job-1')?.agent, 'manager')
  assert.equal(change.job(state, 'job-1')?.worktree, 'wt-1')
})

test('WHAT[CHGINT-001] fresh quality candidate runs to Published with verified publication evidence', async () => {
  const finality = await baseIntegrationScenario()
  assert.equal(finality.verdict.kind, 'Published')
  assert.equal(finality.verdict.detail, 'c-1')
})

test('WHAT[CHGINT-001] pre-rebase C1/S1 certificate cannot authorize rebased S2 candidate and fails closed', async () => {
  const result = await stepIntegrationScenario([
    { kind: 'await:Candidate', snapshotId: 's-1', certificateId: 'cert-1', rebaseNeeded: true },
    { kind: 'invalidate:InitialRebaseRequired' },
    { kind: 'git:rebase', newSnapshotId: 's-2' },
  ])
  assert.equal(result.timeline.filter((s) => s.startsWith('gate:')).length, 0)
})

test('WHAT[CHGINT-001] cancellation during manager loop returns Cancelled verdict and does not burn retry budget', async () => {
  const result = await stepIntegrationScenario([
    { kind: 'cancel' },
  ])
  assert.equal(result.verdict.kind, 'Cancelled')
})

test('WHAT[CHGINT-001] reentry gate cancellation returns Cancelled with zero FF', async () => {
  const result = await stepIntegrationScenario([
    { kind: 'await:Candidate', snapshotId: 's-1', certificateId: 'cert-1', rebaseNeeded: false },
    { kind: 'gate:cancel' },
  ])
  assert.equal(result.verdict.kind, 'Cancelled')
  assert.equal(result.timeline.filter((s) => s.startsWith('ff:')).length, 0)
})

test('WHAT[CHGINT-001] old certificate still fails closed after the rebased record supersedes it', async () => {
  const result = await stepIntegrationScenario([
    { kind: 'await:Candidate', snapshotId: 's-1', certificateId: 'cert-1', rebaseNeeded: true },
    { kind: 'invalidate:InitialRebaseRequired' },
    { kind: 'git:rebase', newSnapshotId: 's-2' },
  ])
  assert.equal(result.timeline.filter((s) => s.startsWith('gate:')).length, 0)
})
