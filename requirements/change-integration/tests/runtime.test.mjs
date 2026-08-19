// CHGINT-002/006/012 — review failure keeps the active worktree identity.

import assert from 'node:assert/strict'
import test from 'node:test'

const change = await import('../../../dist/Change/Surface.js')

test('WHAT[CHGINT-012] ORCH_007_NeedsReview_preserves_the_active_worktree', () => {
  let removeCalls = 0
  let projection = change.createJob(change.empty(), {
    jobId: 'job-1',
    managerSessionId: 'ses-manager-1',
    managerAgent: 'fast-manager',
    byname: 'Road',
    worktreeIdentity: 'manager/job-1',
    worktreePath: '/tmp/wt-job-1',
    targetRef: 'refs/heads/main',
    targetBranchFrozen: 'refs/heads/main',
  })

  projection = change.recordFact(
    projection,
    'job-1',
    change.fact('CandidateReady', { candidateCommit: 'candidate-head', preRebaseReviewBarrierId: 'bar-1' }),
  )
  const job = change.find(projection, 'job-1')
  assert.equal(job.worktreePath, '/tmp/wt-job-1')
  assert.equal(job.worktreeIdentity, 'manager/job-1')
  assert.deepEqual(job.facts, ['CandidateReady'])
  assert.equal(removeCalls, 0, 'a NeedsReview verdict must keep the active worktree')
})
