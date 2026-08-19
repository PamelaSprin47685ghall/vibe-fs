// FINALITY-028: published/released ManagerJob never revives; an active owned
// job may receive an Orchestrator append on the same session/worktree.
//
// ManagerJob history is folded by the production FinalitySurface owner. Tests
// pass plain event objects and assert the JS-native projection only.

import assert from 'node:assert/strict'
import test from 'node:test'

const finality = await import('../../../dist/Mission/Manager/FinalitySurface.js')

const JOB = 'job_finality_028'
const MANAGER = 'ses_finality_028'

const created = (overrides = {}) => ({
  kind: 'job-created',
  jobId: JOB,
  managerSessionId: MANAGER,
  managerAgent: 'fast-manager',
  byname: 'fast-manager',
  worktreeIdentity: 'wt_finality_028',
  worktreePath: '/tmp/wt-finality-028',
  targetRef: 'refs/heads/main',
  targetBranchFrozen: 'refs/heads/main',
  ...overrides,
})

const fact = (kind, attributes = {}) => ({
  kind: 'job-fact',
  jobId: JOB,
  fact: kind,
  ...attributes,
})

const terminal = {
  Published: () => fact('Published', { candidateCommit: 'c1', resultingTargetHead: 'r1' }),
  JobFailed: () => fact('JobFailed', { reason: 'boom' }),
  JobAbandoned: () => fact('JobAbandoned'),
}

const terminalNames = ['Published', 'JobFailed', 'JobAbandoned']

const laterFacts = [
  fact('CandidateReady', { candidateCommit: 'c9', preRebaseReviewBarrierId: 'bar_late' }),
  fact('JobFailed', { reason: 'late' }),
  fact('JobAbandoned'),
]

const projectionOf = (events) => {
  const result = finality.jobProjection(events)
  assert.equal(result.ok, true, JSON.stringify(result.error))
  return result
}

const onlyJob = (projection) => {
  assert.equal(projection.jobs.length, 1)
  return projection.jobs[0]
}

test('WHAT[FINALITY-028] a terminal ManagerJob is not active and does not resume', () => {
  for (const name of terminalNames) {
    const projection = projectionOf([created(), terminal[name]()])
    const job = onlyJob(projection)
    assert.deepEqual(job.facts, [name])
    assert.equal(projection.activeJobs.length, 0, `${name}: not active`)
  }
})

test('WHAT[FINALITY-028] later facts cannot reopen a terminal ManagerJob', () => {
  for (const name of terminalNames) {
    const sealed = projectionOf([created(), terminal[name]()])
    const sealedFacts = onlyJob(sealed).facts
    for (const later of laterFacts) {
      const after = projectionOf([created(), terminal[name](), later])
      assert.deepEqual(onlyJob(after).facts, sealedFacts)
      assert.equal(after.activeJobs.length, 0)
    }
  }
})

test('WHAT[FINALITY-028] replaying ManagerJobCreated cannot re-enlist a terminal job', () => {
  const replayed = projectionOf([
    created(),
    terminal.Published(),
    created({
      managerSessionId: 'ses_other',
      managerAgent: 'deep-manager',
      byname: 'deep-manager',
      worktreeIdentity: 'wt_other',
      worktreePath: '/tmp/wt-other',
      targetRef: 'refs/heads/other',
      targetBranchFrozen: 'refs/heads/other',
    }),
  ])
  const job = onlyJob(replayed)
  assert.deepEqual(job.facts, ['Published'])
  assert.equal(job.managerSessionId, MANAGER)
  assert.equal(job.worktreeIdentity, 'wt_finality_028')
  assert.equal(replayed.activeJobs.length, 0)
})

test('WHAT[FINALITY-028] an active owned job continues on the same session and worktree', () => {
  const projection = projectionOf([created()])
  const job = onlyJob(projection)
  assert.equal(projection.activeJobs.length, 1)
  assert.deepEqual(job.facts, [])
  assert.equal(job.managerSessionId, MANAGER)
  assert.equal(job.worktreeIdentity, 'wt_finality_028')
  assert.equal(job.worktreePath, '/tmp/wt-finality-028')
})
