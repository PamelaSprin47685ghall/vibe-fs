import assert from 'node:assert/strict'
import test from 'node:test'
import * as change from '../../../dist/Change/Surface.js'

test('WHAT[CHGINT-012] nonterminal durable evidence preserves the Road worktree across recovery', () => {
  const state = change.fold([
    { kind: 'ManagerJobCreated', jobId: 'j-1', session: 's-1', worktree: 'wt-1', agent: 'm', role: 'M' },
    { kind: 'RebasedCandidateReady', jobId: 'j-1', rebasedCommit: 'c-1', targetHead: 't-1' },
  ])
  assert.equal(change.job(state, 'j-1')?.worktree, 'wt-1')
  assert.equal(change.isTerminal(state, 'j-1'), false)
})
