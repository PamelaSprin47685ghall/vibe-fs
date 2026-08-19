// CHGINT-006 — active manager jobs remain outstanding until a terminal fact.

import assert from 'node:assert/strict'
import test from 'node:test'

const change = await import('../../../dist/Change/Surface.js')

test('WHAT[CHGINT-006] EXEC_016_active_manager_jobs_are_outstanding_for_orchestrator', () => {
  let jobs = change.createJob(change.empty(), {
    jobId: 'job_1',
    managerSessionId: 'ses_mgr',
    managerAgent: 'fast-manager',
    byname: 'Road',
    worktreeIdentity: 'manager/job_1',
    worktreePath: '/tmp/wt',
    targetRef: 'refs/heads/main',
    targetBranchFrozen: 'main',
  })
  assert.equal(change.activeJobs(jobs).length, 1)

  jobs = change.recordFact(
    jobs,
    'job_1',
    change.fact('Published', { candidateCommit: 'c1', resultingTargetHead: 'r1' }),
  )
  assert.equal(change.activeJobs(jobs).length, 0)
})
