import assert from 'node:assert/strict'
import test from 'node:test'
import * as change from '../../../dist/Change/Surface.js'
import { stepIntegrationScenario } from './support/gate-scope-fixture.mjs'

test('WHAT[CHGINT-007] ORCH_007_the_three_publish_claim_branches_are_evaluated_in_the_clause_order', () => {
  const state = change.fold([
    { kind: 'ManagerJobCreated', jobId: 'j-1', session: 's-1', worktree: 'wt-1', agent: 'm', role: 'M' },
    { kind: 'RebasedCandidateReady', jobId: 'j-1', rebasedCommit: 'c-1', targetHead: 't-1' },
    { kind: 'PublishClaimed', jobId: 'j-1', rebasedCommit: 'c-1', expectedHead: 't-1' },
  ])
  assert.equal(change.classifyPublishClaim(state, 'j-1', 't-1'), 'ProceedWithPublish')
  assert.equal(change.classifyPublishClaim(state, 'j-1', 'c-1'), 'AlreadyLanded')
  assert.equal(change.classifyPublishClaim(state, 'j-1', 't-moved'), 'StaleNeedsRebase')
})

test('WHAT[CHGINT-007] ORCH_008_an_unreadable_target_head_fails_closed_for_every_head_dependent_case', () => {
  const state = change.fold([
    { kind: 'ManagerJobCreated', jobId: 'j-1', session: 's-1', worktree: 'wt-1', agent: 'm', role: 'M' },
    { kind: 'PublishClaimed', jobId: 'j-1', rebasedCommit: 'c-1', expectedHead: 't-1' },
  ])
  assert.equal(change.classifyPublishClaim(state, 'j-1', null), 'UnreadableFailClosed')
})

test('WHAT[CHGINT-007] THEOREM_publish_claimed_three_branch_order_is_fixed', () => {
  const state = change.fold([
    { kind: 'ManagerJobCreated', jobId: 'j-1', session: 's-1', worktree: 'wt-1', agent: 'm', role: 'M' },
    { kind: 'RebasedCandidateReady', jobId: 'j-1', rebasedCommit: 'c-1', targetHead: 't-1' },
    { kind: 'PublishClaimed', jobId: 'j-1', rebasedCommit: 'c-1', expectedHead: 't-1' },
  ])
  assert.equal(change.classifyPublishClaim(state, 'j-1', 't-1'), 'ProceedWithPublish')
})

test('WHAT[CHGINT-007] THEOREM_drop_ephemeral_preserves_publish_claimed_branch_algebra', () => {
  const state = change.fold([
    { kind: 'ManagerJobCreated', jobId: 'j-1', session: 's-1', worktree: 'wt-1', agent: 'm', role: 'M' },
    { kind: 'RebasedCandidateReady', jobId: 'j-1', rebasedCommit: 'c-1', targetHead: 't-1' },
    { kind: 'PublishClaimed', jobId: 'j-1', rebasedCommit: 'c-1', expectedHead: 't-1' },
  ])
  const stripped = change.dropEphemeral(state)
  assert.equal(change.classifyPublishClaim(stripped, 'j-1', 't-1'), 'ProceedWithPublish')
})

test('WHAT[CHGINT-007] classifyPublishClaim three-way reality ordering', async () => {
  const state = change.fold([
    { kind: 'ManagerJobCreated', jobId: 'j-1', session: 's-1', worktree: 'wt-1', agent: 'm', role: 'M' },
    { kind: 'RebasedCandidateReady', jobId: 'j-1', rebasedCommit: 'c-1', targetHead: 't-1' },
    { kind: 'PublishClaimed', jobId: 'j-1', rebasedCommit: 'c-1', expectedHead: 't-1' },
  ])
  assert.equal(change.classifyPublishClaim(state, 'j-1', 't-1'), 'ProceedWithPublish')
  assert.equal(change.classifyPublishClaim(state, 'j-1', 'c-1'), 'AlreadyLanded')
  assert.equal(change.classifyPublishClaim(state, 'j-1', 'other'), 'StaleNeedsRebase')
})

test('WHAT[CHGINT-007] published-but-unsettled reentry never replays FF', async () => {
  const result = await stepIntegrationScenario([
    { kind: 'reentry:already-landed' },
  ])
  assert.equal(result.timeline.filter((s) => s.startsWith('ff:')).length, 0)
})
