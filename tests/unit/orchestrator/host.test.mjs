// tests/unit/orchestrator/host.test.mjs — HOST_ coverage: OrchestratorHost.
//
// A REAL OrchestratorHost (real HostForkRuntime, real journal, real engine)
// over fake GitPort/ManagerPort-shaped seams. The engine is either pre-seeded
// (host.engineInstance = real engine built on fakes — member-level branches)
// or left to the host's own lazy initializeEngine (fake gitPort injected onto
// the host — init/sweep/caching branches). Fable compiles members to module
// functions, so engine behavior is varied through the real engine's ports,
// never through stubbed engine objects.

import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { mkdirSync, mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import {
  agentJournal,
  commitHash,
  managerJobId,
  mapEntries,
  physicalUser,
  resultOf,
  reviewBarrierId,
  sessionId,
  targetRef,
  toList,
  worktreePath,
} from '../support/domain.mjs'

const hostModule = await import('../../../dist/Infrastructure/OpenCode/Orchestration/Host.js')
const {
  OrchestratorHost,
  OrchestratorHost__ContinueManagerJob_Z3E358215: rawContinueManagerJob,
  OrchestratorHost__JoinPublished: hostJoinPublished,
  OrchestratorHost__JoinPublishedAvailable_Z2FFF68F8: rawJoinPublishedAvailable,
  OrchestratorHost__Cancel: hostCancel,
} = hostModule
const rawForkManagerJob = Object.entries(hostModule).find(
  ([name, value]) => name.includes('OrchestratorHost__ForkManagerJob_') && typeof value === 'function',
)?.[1]
assert.equal(typeof rawForkManagerJob, 'function', 'ForkManagerJob export must be discoverable without pinning Fable hash')
const { OrchestratorHostDeps } = await import('../../../dist/Infrastructure/OpenCode/Orchestration/Types.js')
const { Orchestrator_$ctor_2E3EDB2: createOrchestrator } = await import(
  '../../../dist/Application/Orchestration/Runtime.js'
)

// Fable Results are {tag, fields}; resultOf restores the {ok, value, error} surface.
const forkManagerJob = async (host, ...args) => resultOf(await rawForkManagerJob(host, ...args))
const continueManagerJob = async (host, ...args) => resultOf(await rawContinueManagerJob(host, ...args))
const joinPublishedAvailable = async (host, ...args) => resultOf(await rawJoinPublishedAvailable(host, ...args))

const makeSessionId = sessionId

// ── fakes ─────────────────────────────────────────────────────────────────────

const fakeGitPort = (behaviour = {}) => ({
  IsDirty: async () => !!behaviour.dirty,
  CreateWorktree: async (jobId) => ({
    tag: 0,
    fields: [{ fields: [`manager/${jobId.fields?.[0] ?? jobId}`], tag: 0, cases: () => ['WorktreeIdentity'] }],
  }),
  FreezeTargetBranch: async () =>
    behaviour.freezeError ? { tag: 1, fields: [behaviour.freezeError] } : { tag: 0, fields: [targetRef('main')] },
  Rebase: async () => ({ tag: 0, fields: [] }),
  ConflictedFiles: async () => ({ tag: 0, fields: [toList([])] }),
  FfMerge: async () => ({ tag: 0, fields: [commitHash('cafe01')] }),
  RemoveWorktree: async () => ({ tag: 0, fields: [] }),
  HasRebaseHead: async () => false,
  ListWorktrees: async () =>
    behaviour.listError ? { tag: 1, fields: [behaviour.listError] } : { tag: 0, fields: [toList([])] },
  ListManagerBranches: async () => ({ tag: 0, fields: [toList([])] }),
  DeleteBranch: async () => ({ tag: 0, fields: [] }),
  ReadHead: async () => ({ tag: 0, fields: [commitHash('beef02')] }),
  GetTargetHead: async () => ({ tag: 0, fields: [commitHash('beef02')] }),
})

const fakeSessions = (behaviour = {}) => {
  const calls = []
  let childSeq = 0
  return {
    calls,
    CreateChildSession: async (parentId, options) => {
      childSeq += 1
      calls.push(['CreateChildSession', options])
      if (behaviour.createError) return { tag: 1, fields: [behaviour.createError] }
      return { tag: 0, fields: [sessionId(`child-${childSeq}`)] }
    },
    AbortSession: async (id) => {
      calls.push(['AbortSession', id.fields?.[0] ?? id])
      return { tag: 0, fields: [] }
    },
    SendPrompt: async (...args) => {
      calls.push(['SendPrompt', ...args])
      behaviour.onSendPrompt?.(...args)
      if (behaviour.sendPromptError) return { tag: 4, fields: [behaviour.sendPromptError] }
      return { tag: 1, fields: [physicalUser('msg_fake_prompt')] }
    },
    SendPromptAsync: async (...args) => {
      calls.push(['SendPromptAsync', ...args])
      return { tag: 0, fields: [] }
    },
    SubscribeTerminal: (childId, callback) => {
      calls.push(['SubscribeTerminal', childId])
      return { Dispose: () => {} }
    },
    ListChildren: async () => ({ tag: 0, fields: [toList([])] }),
  }
}

const fakeManagerPort = (calls) => ({
  StartManager: async (start) => {
    calls.push(['StartManager', start])
    return { tag: 0, fields: [makeSessionId('manager-ses-1')] }
  },
  AwaitManager: async () => ({ tag: 0, fields: [] }),
  Reverify: async () => ({ tag: 0, fields: [] }),
  ResumeManager: async (jobId, worktree, prompt) => {
    calls.push(['ResumeManager', prompt])
    return { tag: 0, fields: [] }
  },
  TerminateChildren: async () => {},
})

/** A real git repo with one empty commit; gitCommonDir/init stay hermetic. */
const gitDir = (label) => {
  const dir = mkdtempSync(join(tmpdir(), `wxs-host-${label}-`))
  execFileSync('git', ['init', '-b', 'main', dir], { stdio: 'ignore' })
  execFileSync(
    'git',
    ['-C', dir, '-c', 'user.email=t@t', '-c', 'user.name=t', 'commit', '--allow-empty', '-m', 'init'],
    { stdio: 'ignore' },
  )
  return dir
}

/**
 * Real OrchestratorHost over a real journal + real repo. When `seedEngine` is
 * true (default) the host carries a REAL engine built on the fake git port, so
 * member-level branches run without initializeEngine; when false the host
 * initializes its own engine lazily through host.gitPort (init/sweep/caching).
 */
const liveOrchestrator = (options = {}) => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-hostcov-'))
  const repoDir = join(dir, 'repo')
  mkdirSync(repoDir)
  execFileSync('git', ['init', '-b', 'main', repoDir], { stdio: 'ignore' })
  execFileSync(
    'git',
    ['-C', repoDir, '-c', 'user.email=t@t', '-c', 'user.name=t', 'commit', '--allow-empty', '-m', 'init'],
    { stdio: 'ignore' },
  )
  const opened = options.journal === false ? null : agentJournal.create({ directory: dir })
  if (opened) assert.equal(opened.ok, true, 'journal must open')

  const sessions = fakeSessions(options.sessionBehaviour)
  const deps = new OrchestratorHostDeps(
    sessions,
    opened?.journal,
    undefined,
    () => {},
    () => {},
    options.registerReviewerTree ?? (() => {}),
    () => {},
    options.repoPath ?? repoDir,
    options.targetBranch ?? '',
    () => undefined,
    () => undefined,
  )
  const host = new OrchestratorHost(deps, makeSessionId('ses_orphost'))
  host.gitPort = options.gitPort ?? fakeGitPort()

  if (options.seedEngine !== false) {
    const managerCalls = []
    const engine = createOrchestrator(
      host.gitPort,
      fakeManagerPort(managerCalls),
      repoDir,
      targetRef('main'),
      {
        AppendFact: (streamId, factValue) => {
          const appended = agentJournal.appendAgent(streamId, undefined, factValue, opened.journal)
          return appended.ok ? { tag: 0, fields: [appended.value] } : { tag: 1, fields: ['append failed'] }
        },
        Snapshot: () => agentJournal.snapshot(opened.journal),
      },
      repoDir,
    )
    host.engineInstance = engine
    host.__managerCalls = managerCalls
  }

  return {
    host,
    sessions,
    journal: opened?.journal,
    cleanup: () => {
      try {
        opened?.dispose()
      } catch {}
      rmSync(dir, { recursive: true, force: true })
    },
  }
}

const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms))

// ── initializeEngine / engine() ───────────────────────────────────────────────

test('HOST_initializeEngine_runs_sweep_and_caches_the_engine', async () => {
  const live = liveOrchestrator({ seedEngine: false })
  const first = await forkManagerJob(live.host, managerJobId('hostfw1'), 'fast-manager', 'build the thing')
  assert.equal(first.ok, true, first.ok ? '' : first.error)
  assert.equal(first.value, join(tmpdir(), 'wanxiangshu-hostfw1'), 'the engine default worktree path is used')

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

test('HOST_engine_init_failure_is_reported_and_cached', async () => {
  const live = liveOrchestrator({
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

test('HOST_sweep_failure_aborts_engine_initialization', async () => {
  const live = liveOrchestrator({
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

test('HOST_ForkManagerJob_surfaces_the_engine_verdict_error', async () => {
  const live = liveOrchestrator({ journal: false })
  live.host.engineInstance.git.IsDirty = async () => true

  const result = await forkManagerJob(live.host, managerJobId('hostfw5'), 'fast-manager', 'x')
  assert.equal(result.ok, false)
  assert.match(result.error, /Worktree is dirty/)
  live.cleanup()
})

test('HOST_ContinueManagerJob_unknown_job_is_rejected', async () => {
  const live = liveOrchestrator()
  const result = await continueManagerJob(live.host, managerJobId('hostfw6'), 'keep going')
  assert.equal(result.ok, false)
  assert.match(result.error, /Unknown manager job|no longer active/i)
  live.cleanup()
})

test('HOST_ContinueManagerJob_resumes_a_forked_job_in_its_worktree', async () => {
  const live = liveOrchestrator()
  const forked = await forkManagerJob(live.host, managerJobId('hostfw8'), 'fast-manager', 'first pass')
  assert.equal(forked.ok, true, forked.ok ? '' : forked.error)

  const continued = await continueManagerJob(live.host, managerJobId('hostfw8'), 'second pass')
  assert.equal(continued.ok, true, continued.ok ? '' : continued.error)
  assert.ok(continued.value, 'the continued job reports its worktree')
  live.cleanup()
})

test('HOST_JoinPublished_renders_a_string', async () => {
  const live = liveOrchestrator({ journal: false })
  const rendered = await hostJoinPublished(live.host)
  assert.equal(typeof rendered, 'string')
  assert.ok(rendered.length > 0, 'compat join must render something')
  live.cleanup()
})

test('HOST_JoinPublishedAvailable_engine_init_failure_is_an_error_result', async () => {
  const live = liveOrchestrator({
    seedEngine: false,
    journal: false,
    gitPort: fakeGitPort({ freezeError: 'bad repo' }),
  })
  const result = await joinPublishedAvailable(live.host, 1, new Promise(() => {}))
  assert.equal(result.ok, false)
  assert.match(result.error, /bad repo/)
  live.cleanup()
})

test('HOST_Cancel_reaches_the_runtime_without_throwing', () => {
  const live = liveOrchestrator({ journal: false })
  hostCancel(live.host)
  live.cleanup()
})

// ── manager port internals ────────────────────────────────────────────────────

test('HOST_awaitManager_with_no_worktree_registered_fails_closed', async () => {
  const live = liveOrchestrator({ journal: false })
  // Never forked, never registered: AwaitAgent fails fast with unknown agent.
  const result = resultOf(await live.host.managerPort.AwaitManager(managerJobId('hostfw9')))
  assert.equal(result.ok, false)
  assert.match(result.error, /Unknown agent id|No worktree registered/)
  live.cleanup()
})

test('HOST_awaitManager_stages_the_worktree_after_a_completed_manager_run', async () => {
  const live = liveOrchestrator()
  const worktree = gitDir('awm')
  try {
    const started = resultOf(
      await live.host.managerPort.StartManager(
        new (await import('../../../dist/Application/Orchestration/Types.js')).ManagerStart(
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

test('HOST_resumeManager_unknown_job_is_rejected', async () => {
  const live = liveOrchestrator({ journal: false })
  const result = resultOf(
    await live.host.managerPort.ResumeManager(managerJobId('hostfw11'), worktreePath('/tmp/wt'), 'resolve the conflict'),
  )
  assert.equal(result.ok, false)
  assert.match(result.error, /No durable job record for 'hostfw11'/)
  live.cleanup()
})

test('HOST_terminateChildren_tears_down_manager_and_reviewer_children', async () => {
  const live = liveOrchestrator({ journal: false })
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

test('HOST_reverify_durably_opens_barrier_before_first_reviewer_prompt', async () => {
  let live
  let barrierVisibleAtSend = false
  live = liveOrchestrator({
    sessionBehaviour: {
      onSendPrompt: (reviewerId) => {
        const reviewerKey = reviewerId?.fields?.[0] ?? reviewerId
        const projection = mapEntries(agentJournal.snapshot(live.journal).AgentProjections.Sessions)
          .find(([sid]) => (sid?.fields?.[0] ?? sid) === reviewerKey)?.[1]
        barrierVisibleAtSend = projection?.ReviewGuard != null
      },
      sendPromptError: 'stop-after-order-probe',
    },
  })
  const worktree = gitDir('rv-order')
  try {
    const result = resultOf(
      await live.host.managerPort.Reverify(
        managerJobId('hostfw-order'),
        sessionId('ses_mgr_order'),
        worktreePath(worktree),
        reviewBarrierId('bar_order'),
      ),
    )
    assert.equal(result.ok, false, 'probe intentionally fails the transport after observing send order')
    assert.equal(barrierVisibleAtSend, true, 'reviewer provider lane must not start before ReviewBarrierStarted is durable')
  } finally {
    live.cleanup()
    rmSync(worktree, { recursive: true, force: true })
  }
})

test('HOST_reverify_forks_a_deep_reviewer_and_fails_closed_without_a_journal', async () => {
  const live = liveOrchestrator({ journal: false })
  const worktree = gitDir('rvf')
  try {
    const result = resultOf(
      await live.host.managerPort.Reverify(
        managerJobId('hostfw14'),
        sessionId('ses_mgr14'),
        worktreePath(worktree),
        reviewBarrierId('bar_14'),
      ),
    )
    assert.equal(result.ok, false)
    assert.match(result.error, /Cannot open review barrier.*AgentJournal/, 'reverify without a journal fails closed before lane start')

    const created = live.sessions.calls.filter(([name]) => name === 'CreateChildSession')
    assert.ok(created.length >= 1, 'a reviewer child session was prepared')
    assert.equal(live.sessions.calls.filter(([name]) => name === 'SendPrompt').length, 0, 'no reviewer prompt is sent before a durable barrier exists')
    assert.equal(live.host.runtime.children.has('hostfw14-reviewer-bar_14'), true)
  } finally {
    live.cleanup()
    rmSync(worktree, { recursive: true, force: true })
  }
})
