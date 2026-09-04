// CHGINT-006/012 — nonterminal durable evidence keeps the Road worktree identity.

import assert from 'node:assert/strict'
import test from 'node:test'

const change = await import('../../../dist/Change/Surface.js')

test('WHAT[CHGINT-012] nonterminal durable evidence preserves the Road worktree across recovery', () => {
  let projection = change.createJob(change.empty(), {
    jobId: 'job-1',
    managerSessionId: 'ses-manager-1',
    managerAgent: 'manager',
    byname: 'Road',
    worktreeIdentity: 'manager/job-1',
    worktreePath: '/tmp/wt-job-1',
    targetRef: 'refs/heads/main',
    targetBranchFrozen: 'refs/heads/main',
  })

  projection = change.recordFact(
    projection,
    'job-1',
    change.fact('CandidateReady', {
      candidateCommit: 'candidate-head',
      workspaceSnapshotId: 'snapshot-1',
      qualityCertificateId: 'certificate-1',
    }),
  )
  projection = change.recordFact(
    projection,
    'job-1',
    change.fact('ConflictDetected', {
      candidateCommit: 'candidate-head',
      targetHeadSnapshot: 'target-head-1',
      workspaceSnapshotId: 'snapshot-conflict',
      conflictFiles: ['src/conflict.fs'],
      diagnosticsDigest: 'conflict-digest',
    }),
  )

  const job = change.find(projection, 'job-1')
  assert.equal(job.worktreePath, '/tmp/wt-job-1')
  assert.equal(job.worktreeIdentity, 'manager/job-1')
  assert.deepEqual(job.facts, ['CandidateReady', 'ConflictDetected'])
  assert.equal(change.activeJobs(projection).length, 1)
})
