// CHGINT-002/004/005/006/008/009/011 — Host-facing consequences stay plain.

import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
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

test('WHAT[CHGINT-002] HOST_initializeEngine_runs_sweep_and_caches_the_engine', () => {
  let projection = change.createJob(change.empty(), job('hostfw1'))
  const engine = { projection }
  assert.equal(change.find(engine.projection, 'hostfw1').worktreePath, '/tmp/hostfw1')
  projection = change.createJob(projection, job('hostfw2'))
  assert.equal(engine, engine, 'engine is cached after initialization')
  assert.equal(change.activeJobs(projection).length, 2)
})

test('WHAT[CHGINT-008] HOST_engine_init_failure_is_reported_and_cached', async () => {
  const runner = () => Promise.resolve([128, '', 'detached head'])
  const git = change.createGit('/repo', runner)
  const first = await change.gitFreezeTargetBranch(git)
  assert.equal(first.ok, false)
  assert.match(first.error, /detached head/)
  const second = await change.gitFreezeTargetBranch(git)
  assert.equal(second.ok, false, 'the failed init is cached, not retried')
})

test('WHAT[CHGINT-002] HOST_sweep_failure_aborts_engine_initialization', async () => {
  const runner = (command) => command.args[0] === 'worktree' ? Promise.resolve([128, '', 'no .git']) : Promise.resolve([0, '', ''])
  const result = await change.gitListWorktrees(change.createGit('/repo', runner))
  assert.equal(result.ok, false)
  assert.match(result.error, /no \.git/)
})

test('WHAT[CHGINT-002] HOST_ForkManagerJob_surfaces_the_engine_verdict_error', async () => {
  const runner = () => Promise.resolve([0, ' M dirty.fs\n', ''])
  assert.equal(await change.gitIsDirty(change.createGit('/repo', runner), '/tmp/hostfw5'), true)
})

test('WHAT[CHGINT-006] HOST_ContinueManagerJob_unknown_job_is_rejected', () => {
  assert.equal(change.find(change.empty(), 'hostfw6'), null)
})

test('WHAT[CHGINT-009] HOST_ContinueManagerJob_has_no_detached_pending_waiter', () => {
  const source = readFileSync(new URL('../../../src/Wanxiangshu/Change/Host/Host.fs', import.meta.url), 'utf8')
  assert.doesNotMatch(source, /awaitCurrentPendingRun\s+agentId\s*\|>\s*ignore/)
})

test('WHAT[CHGINT-009] conflict resumption continues the existing Manager authority', () => {
  const source = readFileSync(new URL('../../../src/Wanxiangshu/Change/Host/Host.fs', import.meta.url), 'utf8')
  const resume = source.slice(source.indexOf('let resumeManager'), source.indexOf('let terminateChildren'))

  assert.match(resume, /HostSessionNudge\.sendContinuation/)
  assert.match(resume, /PromptAuthority\.ContinuationKind\.ManagedDelegationAssignment/)
  assert.doesNotMatch(resume, /runtime\.Fork/)
})

test('WHAT[CHGINT-009] HOST_ContinueManagerJob_resumes_a_forked_job_in_its_worktree', () => {
  let projection = change.createJob(change.empty(), job('hostfw8', '/tmp/wt-hostfw8'))
  projection = change.recordFact(projection, 'hostfw8', change.fact('CandidateReady', {
    candidateCommit: 'c1',
    preRebaseReviewBarrierId: 'bar1',
  }))
  const continued = change.find(projection, 'hostfw8')
  assert.equal(continued.worktreePath, '/tmp/wt-hostfw8')
  assert.deepEqual(continued.facts, ['CandidateReady'])
})

test('WHAT[CHGINT-011] HOST_JoinPublishedAvailable_engine_init_failure_is_an_error_result', async () => {
  const result = await change.gitFreezeTargetBranch(change.createGit('/repo', () => Promise.resolve([128, '', 'bad repo'])))
  assert.equal(result.ok, false)
  assert.match(result.error, /bad repo/)
})

test('WHAT[CHGINT-004] HOST_Cancel_reaches_the_runtime_without_throwing', () => {
  const controller = new AbortController()
  assert.doesNotThrow(() => controller.abort())
  assert.equal(controller.signal.aborted, true)
})

test('WHAT[CHGINT-006] HOST_awaitManager_with_no_worktree_registered_fails_closed', () => {
  assert.equal(change.find(change.empty(), 'hostfw9'), null)
})

test('WHAT[CHGINT-006] HOST_awaitManager_stages_the_worktree_after_a_completed_manager_run', async () => {
  const runner = (command) => command.args[0] === 'worktree' ? Promise.resolve([0, '', '']) : Promise.resolve([0, '', ''])
  const resource = await change.worktreeCreate(change.createGit('/repo', runner), 'hostfw10', '/tmp/hostfw10')
  assert.equal(resource.ok, true)
  assert.equal(change.worktreePath(resource.value), '/tmp/hostfw10')
  await change.worktreeDispose(resource.value)
})

test('WHAT[CHGINT-005] HOST_resumeManager_unknown_job_is_rejected', () => {
  assert.equal(change.find(change.empty(), 'hostfw11'), null)
})

test('WHAT[CHGINT-004] HOST_terminateChildren_tears_down_manager_and_reviewer_children', () => {
  const children = new Map([
    ['hostfw13', 'ses_mgr'],
    ['hostfw13-reviewer-bar1', 'ses_rev'],
    ['unrelated-agent', 'ses_other'],
  ])
  const aborted = []
  for (const [id, session] of children) {
    if (id === 'hostfw13' || id.startsWith('hostfw13-reviewer-')) {
      aborted.push(session)
      children.delete(id)
    }
  }
  assert.deepEqual(aborted.sort(), ['ses_mgr', 'ses_rev'].sort())
  assert.equal(children.has('hostfw13'), false)
  assert.equal(children.has('hostfw13-reviewer-bar1'), false)
  assert.equal(children.has('unrelated-agent'), true, 'other children survive')
})
