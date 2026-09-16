import assert from 'node:assert/strict'
import test from 'node:test'
import * as change from '../../../dist/Change/Surface.js'

const WT = 'wt-effect-009'
const fold = (facts) => change.fold(facts)

const managerCreated = {
  kind: 'ManagerJobCreated',
  jobId: 'job-1',
  session: 'ses-mgr-1',
  worktree: WT,
  agent: 'manager',
  role: 'Manager',
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

test('WHAT[EFFECT-ACCOUNTING-009] publish_claimed_recovery_three_branch_order_is_fixed', () => {
  const published = fold([managerCreated, rebased, claimed, { kind: 'Published', jobId: 'job-1', commit: 'c-rebased' }])
  assert.equal(change.classifyPublishClaim(published, 'job-1', 'c-rebased'), 'AlreadyLanded')
  assert.equal(change.classifyPublishClaim(published, 'job-1', 'c-target-head'), 'AlreadyLanded')

  const pending = fold([managerCreated, rebased, claimed])
  assert.equal(change.classifyPublishClaim(pending, 'job-1', 'c-target-head'), 'ProceedWithPublish')
  assert.equal(change.classifyPublishClaim(pending, 'job-1', 'c-target-moved'), 'StaleNeedsRebase')
})
