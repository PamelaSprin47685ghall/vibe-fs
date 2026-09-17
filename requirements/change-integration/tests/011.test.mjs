import assert from 'node:assert/strict'
import test from 'node:test'

const change = await import('../../../dist/Change/Surface.js')

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

test('WHAT[CHGINT-011] HOST_JoinPublishedAvailable_engine_init_failure_is_an_error_result', async () => {
  const result = await change.gitFreezeTargetBranch(change.createGit('/repo', () => Promise.resolve([128, '', 'bad repo'])))
  assert.equal(result.ok, false)
  assert.match(result.error, /bad repo/)
})
