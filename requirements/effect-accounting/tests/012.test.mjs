import assert from 'node:assert/strict'
import test from 'node:test'
import * as change from '../../../dist/Change/Surface.js'

const WT = 'wt-effect-012'

const managerCreated = {
  kind: 'ManagerJobCreated',
  jobId: 'job-1',
  session: 'ses-mgr-1',
  worktree: WT,
  agent: 'manager',
  role: 'Manager',
}

const claimed = {
  kind: 'PublishClaimed',
  jobId: 'job-1',
  rebasedCommit: 'c-rebased',
  expectedHead: 'c-target-head',
}

test('WHAT[EFFECT-ACCOUNTING-012] publish_claim_without_durable_rebase_witness_is_rejected', () => {
  const result = change.fold([managerCreated, claimed])
  assert.equal(result.ok, false)
  assert.match(result.error, /publish claimed for a job with no rebased candidate/i)
})
