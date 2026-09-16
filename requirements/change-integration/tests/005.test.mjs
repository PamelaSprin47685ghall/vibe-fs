import assert from 'node:assert/strict'
import test from 'node:test'
import * as change from '../../../dist/Change/Surface.js'
import { stepIntegrationScenario } from './support/gate-scope-fixture.mjs'

test('WHAT[CHGINT-005] rebase conflict records machine fact and continues the loop outside the gate', async () => {
  const result = await stepIntegrationScenario([
    { kind: 'await:Candidate', snapshotId: 's-1', certificateId: 'cert-1', rebaseNeeded: true },
    { kind: 'invalidate:InitialRebaseRequired' },
    { kind: 'git:conflict', unmerged: ['conflict.txt'] },
  ])
  assert.equal(result.timeline.filter((s) => s.startsWith('continue:surface-loop')).length, 1)
  assert.equal(result.timeline.filter((s) => s.startsWith('gate:')).length, 0)
})

test('WHAT[CHGINT-005] artifact conflict continues the loop outside the gate', async () => {
  const result = await stepIntegrationScenario([
    { kind: 'await:Candidate', snapshotId: 's-1', certificateId: 'cert-1', unmerged: true },
    { kind: 'invalidate:ArtifactAdmissionUnmerged' },
  ])
  assert.equal(result.timeline.filter((s) => s.startsWith('continue:surface-loop')).length, 1)
  assert.equal(result.timeline.filter((s) => s.startsWith('gate:')).length, 0)
})

test('WHAT[CHGINT-005] GIT_conflicted_files_parses_lines', async () => {
  const git = change.createGit('/repo', () => Promise.resolve([0, 'conflict1.txt\nconflict2.txt\n', '']))
  const files = await change.gitConflictedFiles(git)
  assert.deepEqual(files, ['conflict1.txt', 'conflict2.txt'])
})

test('WHAT[CHGINT-005] GIT_conflicted_files_error_propagates', async () => {
  const git = change.createGit('/repo', () => Promise.resolve([1, '', 'diff error']))
  const result = await change.gitConflictedFilesResult(git)
  assert.equal(result.ok, false)
})

test('WHAT[CHGINT-005] ConflictDetected_preserves_machine_conflict_evidence_on_the_same_Road_worktree', () => {
  const state = change.fold([
    { kind: 'ManagerJobCreated', jobId: 'j-1', session: 's-1', worktree: 'wt-1', agent: 'm', role: 'M' },
    { kind: 'ConflictDetected', jobId: 'j-1', snapshotId: 's-snap', unmerged: ['a.txt'] },
  ])
  assert.equal(change.job(state, 'j-1')?.conflict?.snapshotId, 's-snap')
})

test('WHAT[CHGINT-005] THEOREM_conflict_detected_remains_independent_durable_evidence', () => {
  const state = change.fold([
    { kind: 'ManagerJobCreated', jobId: 'j-1', session: 's-1', worktree: 'wt-1', agent: 'm', role: 'M' },
    { kind: 'ConflictDetected', jobId: 'j-1', snapshotId: 's-snap', unmerged: ['a.txt'] },
  ])
  assert.deepEqual(change.job(state, 'j-1')?.conflict?.unmerged, ['a.txt'])
})

test('WHAT[CHGINT-005] THEOREM_drop_ephemeral_preserves_conflict_evidence', () => {
  const state = change.fold([
    { kind: 'ManagerJobCreated', jobId: 'j-1', session: 's-1', worktree: 'wt-1', agent: 'm', role: 'M' },
    { kind: 'ConflictDetected', jobId: 'j-1', snapshotId: 's-snap', unmerged: ['a.txt'] },
  ])
  const stripped = change.dropEphemeral(state)
  assert.equal(change.job(stripped, 'j-1')?.conflict?.snapshotId, 's-snap')
})

test('WHAT[CHGINT-005] cleanup failure after landed FF preserves published fact as PublishedPendingCleanup', async () => {
  const result = await stepIntegrationScenario([
    { kind: 'gate:ff-then-cleanup-fail' },
  ])
  assert.equal(result.verdict?.kind, 'PublishedPendingCleanup')
})

test('WHAT[CHGINT-005] reentry cleanup failure preserves PublishedPendingCleanup', async () => {
  const result = await stepIntegrationScenario([
    { kind: 'reentry:cleanup-fail' },
  ])
  assert.equal(result.verdict?.kind, 'PublishedPendingCleanup')
})

test('WHAT[CHGINT-005] WORKTREE_create_returns_owned_resource_and_marks_path_identity', async () => {
  const git = change.createGit('/repo', () => Promise.resolve([0, '', '']))
  const res = await change.worktreeCreate(git, 'job-1', '/wt-path')
  assert.equal(res.ok, true)
  assert.equal(res.value.identity, 'manager/job-1')
})

test('WHAT[CHGINT-005] WORKTREE_release_removes_worktree_and_branch_once', async () => {
  const git = change.createGit('/repo', () => Promise.resolve([0, '', '']))
  const res = await change.worktreeRelease(git, '/wt-path', 'manager/job-1')
  assert.equal(res.ok, true)
})

test('WHAT[CHGINT-005] WORKTREE_release_aggregates_both_failures', async () => {
  const git = change.createGit('/repo', () => Promise.resolve([1, '', 'error']))
  const res = await change.worktreeRelease(git, '/wt-path', 'manager/job-1')
  assert.equal(res.ok, false)
})

test('WHAT[CHGINT-005] WORKTREE_release_reports_single_failure_side', async () => {
  let call = 0
  const git = change.createGit('/repo', () => {
    call += 1
    return Promise.resolve([call === 1 ? 0 : 1, '', 'branch delete fail'])
  })
  const res = await change.worktreeRelease(git, '/wt-path', 'manager/job-1')
  assert.equal(res.ok, false)
  assert.match(res.error, /branch delete fail/i)
})

test('WHAT[CHGINT-005] WORKTREE_unreleased_resource_disposes_by_releasing', async () => {
  let released = false
  const git = change.createGit('/repo', () => {
    released = true
    return Promise.resolve([0, '', ''])
  })
  const res = await change.worktreeCreate(git, 'job-1', '/wt-path')
  change.worktreeDispose(res.value)
  assert.equal(released, true)
})

test('WHAT[CHGINT-005] WORKTREE_CMD_remove_force_flag_and_no_cwd', async () => {
  const calls = []
  const git = change.createGit('/repo', (cmd) => {
    calls.push(cmd.args)
    return Promise.resolve([0, '', ''])
  })
  await change.worktreeRemove(git, '/wt-path')
  assert.ok(calls.some((args) => args.includes('--force')))
})
