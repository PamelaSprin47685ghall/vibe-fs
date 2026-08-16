// Split from tests/unit/orchestrator/host.test.mjs (cutover Wave 2a); owner: change-integration.
//
// HOST_ coverage of the OrchestratorHost job surface: fork/continue/join/publish
// over a REAL OrchestratorHost (real HostForkRuntime, real journal, real engine)
// with fake GitPort/ManagerPort-shaped seams. CHGINT-006/009/011 anchors
// (HOST_ContinueManagerJob_resumes_a_forked_job_in_its_worktree,
// HOST_JoinPublished_renders_a_string). The reverify/review-barrier pair moved
// to requirements/review-assurance/tests/host-reverify.test.mjs.

import assert from 'node:assert/strict'
import { readFileSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import {
  managerJobId,
  resultOf,
  sessionId,
  worktreePath,
} from '../../verification-system/tests/support/domain.mjs'
import {
  continueManagerJob,
  fakeGitPort,
  forkManagerJob,
  gitDir,
  joinPublishedAvailable,
  liveOrchestrator,
} from '../../verification-system/tests/support/orchestrator-host-harness.mjs'

const { OrchestratorHost__JoinPublished: hostJoinPublished } = await import(
  '../../../dist/Change/Host/Host.js'
)
const { OrchestratorHost__Cancel: hostCancel } = await import(
  '../../../dist/Change/Host/Host.js'
)

// ── initializeEngine / engine() ───────────────────────────────────────────────

test('WHAT[CHGINT-002] HOST_initializeEngine_runs_sweep_and_caches_the_engine', async () => {
  const live = await liveOrchestrator({ seedEngine: false })
  const first = await forkManagerJob(live.host, managerJobId('hostfw1'), 'fast-manager', 'build the thing')
  assert.equal(first.ok, true, first.ok ? '' : first.error)
  assert.equal(first.value, join(tmpdirHost(), 'wanxiangshu-hostfw1'), 'the engine default worktree path is used')

  // initializeEngine ran: the real engine instance is set and cached.
  assert.ok(live.host.engineInstance, 'engine instance must exist after first use')
  const again = await forkManagerJob(live.host, managerJobId('hostfw2'), 'fast-manager', 'second')
  assert.equal(again.ok, true, again.ok ? '' : again.error)
  assert.equal(live.host.engineInstance, live.host.engineInstance, 'engine is cached')

  // The manager child was forked with the manager role and its directory.
  const created = live.sessions.calls.filter(([name]) => name === 'CreateChildSession')
  assert.ok(created.length >= 1, 'the manager child session was created')
  live.cleanup()
})

test('WHAT[CHGINT-008] HOST_engine_init_failure_is_reported_and_cached', async () => {
  const live = await liveOrchestrator({
    seedEngine: false,
    journal: false,
    gitPort: fakeGitPort({ freezeError: 'detached head' }),
  })

  const first = await forkManagerJob(live.host, managerJobId('hostfw3'), 'fast-manager', 'x')
  assert.equal(first.ok, false)
  assert.match(first.error, /detached head/)

  const second = await forkManagerJob(live.host, managerJobId('hostfw3'), 'fast-manager', 'x')
  assert.equal(second.ok, false, 'the failed init is cached, not retried')
  live.cleanup()
})

test('WHAT[CHGINT-002] HOST_sweep_failure_aborts_engine_initialization', async () => {
  const live = await liveOrchestrator({
    seedEngine: false,
    journal: false,
    gitPort: fakeGitPort({ listError: 'no .git' }),
  })

  const result = await forkManagerJob(live.host, managerJobId('hostfw4'), 'fast-manager', 'x')
  assert.equal(result.ok, false)
  assert.match(result.error, /orchestrator cleanup failed: cannot list worktrees/)
  live.cleanup()
})

// ── member-level branches over the real engine ───────────────────────────────

test('WHAT[CHGINT-002] HOST_ForkManagerJob_surfaces_the_engine_verdict_error', async () => {
  const live = await liveOrchestrator({ journal: false })
  live.host.engineInstance.git.IsDirty = async () => true

  const result = await forkManagerJob(live.host, managerJobId('hostfw5'), 'fast-manager', 'x')
  assert.equal(result.ok, false)
  assert.match(result.error, /Worktree is dirty/)
  live.cleanup()
})

test('WHAT[CHGINT-006] HOST_ContinueManagerJob_unknown_job_is_rejected', async () => {
  const live = await liveOrchestrator()
  const result = await continueManagerJob(live.host, managerJobId('hostfw6'), 'keep going')
  assert.equal(result.ok, false)
  assert.match(result.error, /Unknown manager job|no longer active/i)
  live.cleanup()
})

test('WHAT[CHGINT-009] HOST_ContinueManagerJob_has_no_detached_pending_waiter', () => {
  const source = readFileSync(new URL('../../../src/Wanxiangshu/Change/Host/Host.fs', import.meta.url), 'utf8')
  assert.doesNotMatch(
    source,
    /awaitCurrentPendingRun\s+agentId\s*\|>\s*ignore/,
    'continuation must not leak a detached timeout/polling task after the Host callback path is already live',
  )
})

test('WHAT[CHGINT-009] HOST_ContinueManagerJob_resumes_a_forked_job_in_its_worktree', async () => {
  const live = await liveOrchestrator()
  const forked = await forkManagerJob(live.host, managerJobId('hostfw8'), 'fast-manager', 'first pass')
  assert.equal(forked.ok, true, forked.ok ? '' : forked.error)

  const continued = await continueManagerJob(live.host, managerJobId('hostfw8'), 'second pass')
  assert.equal(continued.ok, true, continued.ok ? '' : continued.error)
  assert.ok(continued.value, 'the continued job reports its worktree')

  // The real engine owns a publication task after ForkManagerJob. Teardown must
  // first consume that owned task's verdict; deleting the repo while it is still
  // appending durable facts turns its physical store-lock acquisition into an
  // orphaned retry loop.
  await hostJoinPublished(live.host)
  live.cleanup()
})

test('WHAT[CHGINT-011] HOST_JoinPublished_renders_a_string', async () => {
  const live = await liveOrchestrator({ journal: false })
  const rendered = await hostJoinPublished(live.host)
  assert.equal(typeof rendered, 'string')
  assert.ok(rendered.length > 0, 'compat join must render something')
  live.cleanup()
})

test('WHAT[CHGINT-011] HOST_JoinPublishedAvailable_engine_init_failure_is_an_error_result', async () => {
  const live = await liveOrchestrator({
    seedEngine: false,
    journal: false,
    gitPort: fakeGitPort({ freezeError: 'bad repo' }),
  })
  const result = await joinPublishedAvailable(live.host, 1, new Promise(() => {}))
  assert.equal(result.ok, false)
  assert.match(result.error, /bad repo/)
  live.cleanup()
})

test('WHAT[CHGINT-004] HOST_Cancel_reaches_the_runtime_without_throwing', async () => {
  const live = await liveOrchestrator({ journal: false })
  hostCancel(live.host)
  live.cleanup()
})

// ── manager port internals ────────────────────────────────────────────────────

test('WHAT[CHGINT-006] HOST_awaitManager_with_no_worktree_registered_fails_closed', async () => {
  const live = await liveOrchestrator({ journal: false })
  // Never forked, never registered: AwaitAgent fails fast with unknown agent.
  const result = resultOf(await live.host.managerPort.AwaitManager(managerJobId('hostfw9')))
  assert.equal(result.ok, false)
  assert.match(result.error, /Unknown agent id|No worktree registered/)
  live.cleanup()
})

test('WHAT[CHGINT-006] HOST_awaitManager_stages_the_worktree_after_a_completed_manager_run', async () => {
  const live = await liveOrchestrator()
  const worktree = gitDir('awm')
  try {
    const started = resultOf(
      await live.host.managerPort.StartManager(
        new (await import('../../../dist/Change/Types.js')).ManagerStart(
          managerJobId('hostfw10'),
          'fast-manager',
          worktreePath(worktree),
          'do it',
        ),
      ),
    )
    assert.equal(started.ok, true, started.ok ? '' : started.error)
    assert.ok(started.value, 'child session id')
    live.cleanup()
  } finally {
    rmSync(worktree, { recursive: true, force: true })
  }
})

test('WHAT[CHGINT-005] HOST_resumeManager_unknown_job_is_rejected', async () => {
  const live = await liveOrchestrator({ journal: false })
  const result = resultOf(
    await live.host.managerPort.ResumeManager(managerJobId('hostfw11'), worktreePath('/tmp/wt'), 'resolve the conflict'),
  )
  assert.equal(result.ok, false)
  assert.match(result.error, /No durable job record for 'hostfw11'/)
  live.cleanup()
})

test('WHAT[CHGINT-004] HOST_terminateChildren_tears_down_manager_and_reviewer_children', async () => {
  const live = await liveOrchestrator({ journal: false })
  // Seed the runtime child map directly: manager + one reviewer (job-reviewer-<bar>).
  const managerSid = sessionId('ses_mgr')
  const reviewerSid = sessionId('ses_rev')
  live.host.runtime.children.set('hostfw13', managerSid)
  live.host.runtime.children.set('hostfw13-reviewer-bar1', reviewerSid)
  live.host.runtime.children.set('unrelated-agent', sessionId('ses_other'))

  await live.host.managerPort.TerminateChildren(managerJobId('hostfw13'))

  const aborted = live.sessions.calls.filter(([name]) => name === 'AbortSession').map(([, id]) => id)
  assert.deepEqual(aborted.sort(), [reviewerSid.fields[0], managerSid.fields[0]].sort())
  assert.equal(live.host.runtime.children.has('hostfw13'), false)
  assert.equal(live.host.runtime.children.has('hostfw13-reviewer-bar1'), false)
  assert.equal(live.host.runtime.children.has('unrelated-agent'), true, 'other children survive')
  live.cleanup()
})

const tmpdirHost = () => tmpdir()
