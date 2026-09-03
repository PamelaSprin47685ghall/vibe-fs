// Effect state transitions through the Change owner and journal codec surfaces.
import assert from 'node:assert/strict'
import test from 'node:test'
import { readFileSync } from 'node:fs'
import * as change from '../../../dist/Change/Surface.js'

const FACT_CODEC_SOURCE = readFileSync(new URL('../../../src/Wanxiangshu/Persistence/Journal/FactCodec.fs', import.meta.url), 'utf8')
const FACT_TYPES_SOURCE = readFileSync(new URL('../../../src/Wanxiangshu/Change/Facts.fs', import.meta.url), 'utf8')

const JOB = 'job_ea'
const WT = 'wt_ea'
const WT_PATH = '/tmp/wt_ea'
const baseJob = {
  jobId: JOB,
  managerSessionId: 'ses_ea',
  managerAgent: 'manager',
  byname: 'Road',
  worktreeIdentity: WT,
  worktreePath: WT_PATH,
  targetRef: 'refs/heads/main',
  targetBranchFrozen: 'refs/heads/main',
}
const createJob = () => change.createJob(change.empty(), baseJob)
const progress = (kind, payload) => ({ kind, payload })
const managerCreated = { kind: 'ManagerJobCreated', payload: baseJob }
const requested = { kind: 'WorktreeCreateRequested', payload: { jobId: JOB, worktreeIdentity: WT, worktreePath: WT_PATH } }
const created = { kind: 'WorktreeCreated', payload: { jobId: JOB, worktreeIdentity: WT, worktreePath: WT_PATH } }
const rebased = {
  kind: 'RebasedCandidateReady',
  payload: { jobId: JOB, rebasedCommit: 'r1', targetHeadSnapshot: 'h1', postRebaseReviewBarrierId: 'bar_2' },
}
const claimed = { kind: 'PublishClaimed', payload: { jobId: JOB, expectedHead: 'h1' } }

const fold = (events) => {
  const result = change.fold(events)
  assert.equal(result.ok, true, result.error ?? '')
  return change.unwrapFold(result)
}

test('WHAT[EFFECT-ACCOUNTING-001] worktree_requested_created_are_distinct_typed_states_not_one_bool', () => {
  let projection = createJob()
  assert.equal(change.worktreeEffect(projection, WT), null)
  projection = change.requestWorktree(projection, WT, WT_PATH, JOB)
  assert.equal(change.worktreeEffect(projection, WT), 'Requested')
  projection = change.acceptWorktree(projection, WT, WT_PATH, JOB)
  assert.equal(change.worktreeEffect(projection, WT), 'Created')
  projection = change.requestWorktree(projection, WT, WT_PATH, JOB)
  assert.equal(change.worktreeEffect(projection, WT), 'Created')
  assert.equal(change.worktreeEffect(fold([managerCreated, requested, created]), WT), 'Created')
})

test('WHAT[EFFECT-ACCOUNTING-009] publish_claimed_recovery_three_branch_order_is_fixed', () => {
  const projection = fold([
    managerCreated,
    rebased,
    claimed,
  ])
  assert.deepEqual(change.find(projection, JOB).facts, ['RebasedCandidateReady', 'PublishClaimed'])
  assert.equal(change.classifyPublishClaim('r1', 'r1', 'h1').kind, 'AlreadyFastForwarded')
  assert.equal(change.classifyPublishClaim('h1', 'r1', 'h1').kind, 'PublishReady')
  assert.equal(change.classifyPublishClaim('zzz', 'r1', 'h1').kind, 'ClaimExpired')
  assert.equal(change.classifyPublishClaim(null, 'r1', 'h1').kind, 'HeadUnreadable')
})

test('WHAT[EFFECT-ACCOUNTING-012] publish_claim_without_durable_rebase_witness_is_rejected', () => {
  const result = change.fold([managerCreated, claimed])
  assert.equal(result.ok, false)
  assert.match(result.error, /publish claimed for a job with no rebased candidate/i)
})

test('WHAT[EFFECT-ACCOUNTING-010] typed_effect_facts_replace_the_generic_durable_effect_union', () => {
  assert.match(FACT_TYPES_SOURCE, /WorktreeCreateRequested/)
  assert.match(FACT_TYPES_SOURCE, /WorktreeCreated/)
  assert.match(FACT_TYPES_SOURCE, /PublishClaimed/)
  assert.match(FACT_TYPES_SOURCE, /RebasedCandidateReady/)
  assert.match(FACT_TYPES_SOURCE, /ManagerJobCreated/)
  assert.doesNotMatch(FACT_TYPES_SOURCE, /DurableEffectRequested|DurableEffectAccepted/)
  const projection = fold([managerCreated, requested, created, rebased, claimed])
  assert.equal(change.worktreeEffect(projection, WT), 'Created')
  assert.match(FACT_CODEC_SOURCE, /pre.?050|migration|unsupported/i)
})
