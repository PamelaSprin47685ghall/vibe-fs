import assert from 'node:assert/strict'
import test from 'node:test'
import * as change from '../../../dist/Change/Surface.js'
import { stepIntegrationScenario } from './support/gate-scope-fixture.mjs'

test('WHAT[CHGINT-013] target movement before publish invalidates the certificate and continues the loop without entering the gate', async () => {
  const result = await stepIntegrationScenario([
    { kind: 'await:Candidate', snapshotId: 's-1', certificateId: 'cert-1', rebaseNeeded: false },
    { kind: 'target:moved', newTargetHead: 't-2' },
    { kind: 'invalidate:TargetAdvanced' },
  ])
  assert.equal(result.timeline.filter((s) => s.startsWith('gate:')).length, 0)
})

test('WHAT[CHGINT-013] CAS miss invalidates certificate rebases and continues the loop after releasing the gate', async () => {
  const result = await stepIntegrationScenario([
    { kind: 'await:Candidate', snapshotId: 's-1', certificateId: 'cert-1', rebaseNeeded: false },
    { kind: 'gate:cas-miss' },
    { kind: 'invalidate:PublishCasMissed' },
  ])
  assert.equal(result.timeline.filter((s) => s === 'gate:released').length, 1)
})

test('WHAT[CHGINT-013] CAS miss appends the complete claim then a superseding rebased record, continues once, publishes at most once', async () => {
  const result = await stepIntegrationScenario([
    { kind: 'gate:cas-miss-rebase-reenter' },
  ])
  assert.equal(result.timeline.filter((s) => s.startsWith('ff:')).length, 1)
})

test('WHAT[CHGINT-013] target movement appends a superseding rebased record and continues once without publishing', async () => {
  const result = await stepIntegrationScenario([
    { kind: 'target:moved-record-rebase' },
  ])
  assert.equal(result.timeline.filter((s) => s.startsWith('ff:')).length, 0)
})

test('WHAT[CHGINT-013] target_movement_makes_the_rebased_binding_stale', () => {
  const state = change.fold([
    { kind: 'ManagerJobCreated', jobId: 'j-1', session: 's-1', worktree: 'wt-1', agent: 'm', role: 'M' },
    { kind: 'RebasedCandidateReady', jobId: 'j-1', rebasedCommit: 'c-1', targetHead: 't-1' },
    { kind: 'PublishClaimed', jobId: 'j-1', rebasedCommit: 'c-1', expectedHead: 't-1' },
  ])
  assert.equal(change.classifyPublishClaim(state, 'j-1', 't-2'), 'StaleNeedsRebase')
})

test('WHAT[CHGINT-013] THEOREM_stale_target_invalidates_the_rebased_binding', () => {
  const state = change.fold([
    { kind: 'ManagerJobCreated', jobId: 'j-1', session: 's-1', worktree: 'wt-1', agent: 'm', role: 'M' },
    { kind: 'RebasedCandidateReady', jobId: 'j-1', rebasedCommit: 'c-1', targetHead: 't-1' },
    { kind: 'PublishClaimed', jobId: 'j-1', rebasedCommit: 'c-1', expectedHead: 't-1' },
  ])
  assert.equal(change.classifyPublishClaim(state, 'j-1', 't-moved'), 'StaleNeedsRebase')
})

test('WHAT[CHGINT-013] CAS miss releases gate and invalidates certificate without publishing', async () => {
  const result = await stepIntegrationScenario([
    { kind: 'gate:cas-miss-release' },
  ])
  assert.equal(result.timeline.filter((s) => s.startsWith('ff:')).length, 0)
})

test('WHAT[CHGINT-013] superseding rebased evidence appends while the old claim cannot reenter after supersession', async () => {
  const result = await stepIntegrationScenario([
    { kind: 'claim:superseded' },
  ])
  assert.equal(result.timeline.filter((s) => s.startsWith('ff:')).length, 0)
})

test('WHAT[CHGINT-013] recordFact keeps the latest rebased record instead of the first write', () => {
  const state = change.fold([
    { kind: 'ManagerJobCreated', jobId: 'j-1', session: 's-1', worktree: 'wt-1', agent: 'm', role: 'M' },
    { kind: 'RebasedCandidateReady', jobId: 'j-1', rebasedCommit: 'c-1', targetHead: 't-1' },
    { kind: 'RebasedCandidateReady', jobId: 'j-1', rebasedCommit: 'c-2', targetHead: 't-2' },
  ])
  assert.equal(change.job(state, 'j-1')?.rebasedCommit, 'c-2')
})

test('WHAT[CHGINT-013] stale claim superseded by newer rebased evidence re-enters the loop without FF', async () => {
  const result = await stepIntegrationScenario([
    { kind: 'claim:superseded-reenter' },
  ])
  assert.equal(result.timeline.filter((s) => s.startsWith('ff:')).length, 0)
})
