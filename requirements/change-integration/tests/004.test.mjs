import assert from 'node:assert/strict'
import test from 'node:test'
import * as change from '../../../dist/Change/Surface.js'

test('WHAT[CHGINT-004] GATE_lock_path_is_stable_per_repo_and_branch', () => {
  const p1 = change.gateLockPath('/repo', 'main')
  const p2 = change.gateLockPath('/repo', 'main')
  assert.equal(p1, p2)
})

test('WHAT[CHGINT-004] GATE_acquire_and_release_round_trips', async () => {
  const gate = change.createIntegrationGate('/tmp/test-gate-1')
  const handle = await change.gateAcquire(gate)
  assert.equal(handle.ok, true)
  change.gateRelease(handle.value)
})

test('WHAT[CHGINT-004] GATE_dispose_releases_the_lock', async () => {
  const gate = change.createIntegrationGate('/tmp/test-gate-2')
  const handle = await change.gateAcquire(gate)
  change.gateDispose(gate)
  assert.equal(handle.ok, true)
})

test('WHAT[CHGINT-004] GATE_second_acquire_on_held_lock_eventually_fails', async () => {
  const gate = change.createIntegrationGate('/tmp/test-gate-3')
  const h1 = await change.gateAcquire(gate)
  assert.equal(h1.ok, true)
  const h2 = await change.gateTryAcquireTimeout(gate, 10)
  assert.equal(h2.ok, false)
  change.gateRelease(h1.value)
})

test('WHAT[CHGINT-004] ORCH_004_multiple_jobs_are_active_at_once_and_terminal_ones_drop_out', () => {
  const state = change.fold([
    { kind: 'ManagerJobCreated', jobId: 'j-1', session: 's-1', worktree: 'wt-1', agent: 'm', role: 'M' },
    { kind: 'ManagerJobCreated', jobId: 'j-2', session: 's-2', worktree: 'wt-2', agent: 'm', role: 'M' },
    { kind: 'Published', jobId: 'j-1', commit: 'c-1' },
  ])
  assert.equal(change.activeJobs(state).length, 1)
  assert.equal(change.activeJobs(state)[0].jobId, 'j-2')
})

test('WHAT[CHGINT-004] THEOREM_orchestrator_independent_jobs_confluent_across_interleavings', () => {
  const s1 = change.fold([
    { kind: 'ManagerJobCreated', jobId: 'j-1', session: 's-1', worktree: 'wt-1', agent: 'm', role: 'M' },
    { kind: 'ManagerJobCreated', jobId: 'j-2', session: 's-2', worktree: 'wt-2', agent: 'm', role: 'M' },
  ])
  const s2 = change.fold([
    { kind: 'ManagerJobCreated', jobId: 'j-2', session: 's-2', worktree: 'wt-2', agent: 'm', role: 'M' },
    { kind: 'ManagerJobCreated', jobId: 'j-1', session: 's-1', worktree: 'wt-1', agent: 'm', role: 'M' },
  ])
  assert.deepEqual(change.activeJobs(s1), change.activeJobs(s2))
})
