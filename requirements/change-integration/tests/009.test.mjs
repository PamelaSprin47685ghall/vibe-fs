import assert from 'node:assert/strict'
import test from 'node:test'
import * as change from '../../../dist/Change/Surface.js'

const job = (id, path = `/tmp/${id}`) => ({
  jobId: id,
  managerSessionId: `ses-${id}`,
  managerAgent: 'manager',
  byname: id,
  worktreeIdentity: `manager/${id}`,
  worktreePath: path,
  targetRef: 'refs/heads/main',
  targetBranchFrozen: 'refs/heads/main',
})

test('WHAT[CHGINT-009] manager loop keeps the durable job worktree', () => {
  let projection = change.createJob(change.empty(), job('hostfw8', '/tmp/wt-hostfw8'))
  projection = change.recordFact(projection, 'hostfw8', change.fact('CandidateReady', {
    candidateCommit: 'c1',
    workspaceSnapshotId: 'snapshot-1',
    qualityCertificateId: 'certificate-1',
  }))
  const continued = change.find(projection, 'hostfw8')
  assert.equal(continued.worktreePath, '/tmp/wt-hostfw8')
  assert.deepEqual(continued.facts, ['CandidateReady'])
})

test('WHAT[CHGINT-009] ORCH_006_the_worktree_is_located_by_identity_and_the_path_is_only_diagnostic', () => {
  const state = change.fold([
    { kind: 'ManagerJobCreated', jobId: 'j-1', session: 's-1', worktree: 'wt-1', agent: 'm', role: 'M' },
  ])
  assert.equal(change.job(state, 'j-1')?.worktree, 'wt-1')
})

test('WHAT[CHGINT-009] ORCH_003_durable_facts_do_not_change_job_identity', () => {
  const state = change.fold([
    { kind: 'ManagerJobCreated', jobId: 'j-1', session: 's-1', worktree: 'wt-1', agent: 'm', role: 'M' },
    { kind: 'RebasedCandidateReady', jobId: 'j-1', rebasedCommit: 'c-1', targetHead: 't-1' },
  ])
  assert.equal(change.job(state, 'j-1')?.jobId, 'j-1')
})

test('WHAT[CHGINT-009] ORCH_003_a_manager_session_resolves_to_its_one_job', () => {
  const state = change.fold([
    { kind: 'ManagerJobCreated', jobId: 'j-1', session: 's-1', worktree: 'wt-1', agent: 'm', role: 'M' },
  ])
  assert.equal(change.jobForSession(state, 's-1')?.jobId, 'j-1')
})

test('WHAT[CHGINT-009] ORCH_003_a_second_create_for_one_job_id_cannot_change_its_manager_or_worktree', () => {
  const state = change.fold([
    { kind: 'ManagerJobCreated', jobId: 'j-1', session: 's-1', worktree: 'wt-1', agent: 'm', role: 'M' },
    { kind: 'ManagerJobCreated', jobId: 'j-1', session: 's-2', worktree: 'wt-2', agent: 'm2', role: 'M' },
  ])
  assert.equal(change.job(state, 'j-1')?.session, 's-1')
  assert.equal(change.job(state, 'j-1')?.worktree, 'wt-1')
})

test('WHAT[CHGINT-009] WORKTREE_CMD_identity_of_is_manager_slash_job', () => {
  assert.equal(change.worktreeIdentityOf('job-xyz'), 'manager/job-xyz')
})
