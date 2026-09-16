import assert from 'node:assert/strict'
import test from 'node:test'
import * as change from '../../../dist/Change/Surface.js'

const WT = 'wt-effect-001'
const fold = (facts) => change.fold(facts)

const managerCreated = {
  kind: 'ManagerJobCreated',
  jobId: 'job-1',
  session: 'ses-mgr-1',
  worktree: WT,
  agent: 'manager',
  role: 'Manager',
}

const requested = {
  kind: 'WorktreeCreateRequested',
  jobId: 'job-1',
  worktree: WT,
}

const created = {
  kind: 'WorktreeCreated',
  jobId: 'job-1',
  worktree: WT,
}

test('WHAT[EFFECT-ACCOUNTING-001] worktree_requested_created_are_distinct_typed_states_not_one_bool', () => {
  const initial = fold([managerCreated])
  assert.equal(change.worktreeEffect(initial, WT), 'NotRequested')

  const pending = fold([managerCreated, requested])
  assert.equal(change.worktreeEffect(pending, WT), 'Requested')

  const confirmed = fold([managerCreated, requested, created])
  assert.equal(change.worktreeEffect(confirmed, WT), 'Created')
})
