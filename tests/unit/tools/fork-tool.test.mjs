// tests/unit/tools/fork-tool.test.mjs — VERIFY-009 coverage: Manager fork/nudge tool.
//
// The tool calls module-level HostForkRuntime functions, so the runtime is REAL:
// built on a fake ISessionHostPort plus a real AgentJournal in a temp dir. Fork,
// reuse, linkage and prompt-claim paths under test are then production code.

import assert from 'node:assert/strict'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import { agentJournal, sessionId, toList } from '../support/domain.mjs'

const {
  HostToolArguments_$ctor_4E60E31B: makeArgs,
  HostToolContext,
  ToolHostCodec_factory,
} = await import('../../../dist/Infrastructure/OpenCode/Codec/ToolHostCodec.js')
const { managerSpec, orchestratorSpec } = await import('../../../dist/Infrastructure/OpenCode/Tools/ForkTool.js')
const { ToolRuntimeScope } = await import('../../../dist/Infrastructure/OpenCode/Tools/ToolRuntimeScope.js')
const { HostForkRuntime } = await import('../../../dist/Session/HostForkRuntime.js')
const { OrchestratorHost } = await import('../../../dist/Infrastructure/OpenCode/Orchestration/Host.js')
const { OrchestratorHostDeps } = await import('../../../dist/Infrastructure/OpenCode/Orchestration/Types.js')
const { Orchestrator_$ctor_2E3EDB2: createOrchestrator } = await import(
  '../../../dist/Application/Orchestration/Runtime.js'
)
const { targetRef, commitHash, managerJobId, sessionId: makeSessionId, fact, stream } = await import('../support/domain.mjs')

const fakeSchema = {
  string: () => ({ kind: 'string', optional: () => ({ kind: 'string-optional' }) }),
  enum: (values) => ({
    describe: (description) => ({
      optional: () => ({ kind: 'enum-described-optional', values, description }),
    }),
    optional: () => ({ kind: 'enum-optional', values }),
  }),
  union: (parts) => ({ kind: 'union', parts }),
}
const factory = ToolHostCodec_factory({ tool: { schema: fakeSchema } })

const context = (sessionId = 'ses_fork') =>
  new HostToolContext(sessionId, undefined, undefined, undefined, undefined, () => () => {})

const parseToml = (text) =>
  Object.fromEntries(
    text
      .split('\n')
      .filter((line) => line.includes(' = '))
      .map((line) => {
        const [name, ...rest] = line.split(' = ')
        const raw = rest.join(' = ')
        return [name, raw.startsWith('"') ? JSON.parse(raw) : raw]
      }),
  )

const PARENT = sessionId('ses_fork')

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
      return { tag: 0, fields: [] }
    },
    SendPromptAsync: async (...args) => {
      calls.push(['SendPromptAsync', ...args])
      return { tag: 0, fields: [] }
    },
    SubscribeTerminal: async (childId, callback) => {
      calls.push(['SubscribeTerminal', childId])
      return { Dispose: () => {} }
    },
    ListChildren: async () => ({ tag: 0, fields: [toList([])] }),
  }
}

/** { scope, runtime, sessions, journal, cleanup } — real runtime, fake host. */
const liveScope = (behaviour = {}) => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-forktool-'))
  const opened = agentJournal.create({ directory: dir })
  assert.equal(opened.ok, true, 'journal must open')

  const sessions = fakeSessions(behaviour)
  const runtime = new HostForkRuntime(
    PARENT,
    sessions,
    opened.journal,
    undefined, // onChildCreated
    undefined, // onChildCreatedDir
    undefined, // ptyPort
    undefined, // directoryFor
    undefined, // onRunStarted
    undefined, // parentWorkRecordFor
    undefined, // childWorkRecordFor
    undefined, // sessionSnapshot
    undefined, // cancelSignals
  )

  const scope = new ToolRuntimeScope(
    sessions,
    opened.journal,
    undefined,
    undefined,
    new Map(),
    () => undefined,
    new Set(),
    new Map(),
    undefined,
    undefined,
    undefined,
    undefined,
    undefined,
  )
  scope.runtimes.set('ses_fork', runtime)

  return {
    scope,
    runtime,
    sessions,
    journal: opened.journal,
    cleanup: () => {
      try {
        opened.dispose()
      } catch {}
      rmSync(dir, { recursive: true, force: true })
    },
  }
}

/** A scope with no runtime seeded; an orchestrator host can still be pre-seeded. */
const bareScope = ({ orchestratorHost, sessions } = {}) => {
  const scope = new ToolRuntimeScope(
    sessions,
    undefined,
    undefined,
    undefined,
    new Map(),
    () => undefined,
    new Set(),
    new Map(),
    undefined,
    undefined,
    undefined,
    undefined,
    undefined,
  )
  if (orchestratorHost) scope.orchestratorHosts.set('ses_fork', orchestratorHost)
  return scope
}

const runManager = (spec, agent, prompt, extra = {}) =>
  spec.Execute(makeArgs({ agent, prompt, ...extra }), context())

// ── request validation (refused before the runtime) ─────────────────────────

test('FORK_blank_agent_is_refused', async () => {
  const spec = managerSpec(factory, bareScope())
  const result = parseToml(await runManager(spec, '', 'do work'))
  assert.match(result.error, /agent is required/)
})

test('FORK_pty_name_is_refused_on_the_manager_tool', async () => {
  const spec = managerSpec(factory, bareScope())
  const result = parseToml(await runManager(spec, 'pty', 'do work'))
  assert.match(result.error, /fork-pty tool/)
})

test('FORK_invalid_tdd_phase_is_refused_before_any_spawn', async () => {
  const live = liveScope()
  const spec = managerSpec(factory, live.scope)
  const result = parseToml(await runManager(spec, 'fast-coder', 'do work', { tdd: 'blue' }))

  assert.match(String(result.error), /tdd|TDD|red|green/i)
  assert.deepEqual(live.sessions.calls, [], 'a parse failure must not reach the session host')
  live.cleanup()
})

test('FORK_disposed_scope_surfaces_runtime_error', async () => {
  const live = liveScope()
  live.scope.disposed = true
  const spec = managerSpec(factory, live.scope)
  const result = parseToml(await runManager(spec, 'fast-coder', 'do work'))
  assert.match(result.error, /disposed/)
  live.cleanup()
})

test('FORK_hidden_role_by_name_is_denied_generically', async () => {
  const spec = managerSpec(factory, bareScope())
  const result = parseToml(await runManager(spec, 'fast-reviewer', 'review this'))
  assert.equal(result.error, 'Unknown or unavailable managed agent.')
})

test('FORK_known_but_not_forkable_role_is_explained', async () => {
  const live = liveScope()
  const spec = managerSpec(factory, live.scope)
  const result = parseToml(await runManager(spec, 'fast-inspector', 'inspect'))
  assert.match(result.error, /not creatable via Manager fork/)
  live.cleanup()
})

test('FORK_garbage_agent_name_reports_parse_error', async () => {
  const spec = managerSpec(factory, bareScope())
  const result = parseToml(await runManager(spec, 'not-an-agent-name!', 'do work'))
  assert.ok(result.error, 'an error must be returned')
  assert.match(result.error, /managed agent|fast-|deep-/i)
})

// ── fresh fork path (real runtime + journal) ─────────────────────────────────

test('FORK_public_forkable_agent_creates_a_child', async () => {
  const live = liveScope()
  const spec = managerSpec(factory, live.scope)
  const result = parseToml(await runManager(spec, 'fast-coder', 'implement the feature'))

  assert.equal(result.agent, 'fast-coder')
  assert.equal(result.role, 'coder')
  assert.equal(result.tier, 'fast')
  assert.equal(result.fallback_peer, 'deep-coder')
  assert.ok(result.agent_id, 'agent_id present')

  const created = live.sessions.calls.filter(([name]) => name === 'CreateChildSession')
  assert.equal(created.length, 1, 'exactly one child session')
  live.cleanup()
})

test('FORK_tdd_phase_composes_assignment_into_first_prompt', async () => {
  const live = liveScope()
  const spec = managerSpec(factory, live.scope)
  const result = parseToml(await runManager(spec, 'fast-coder', 'add the parser', { tdd: 'red' }))

  assert.equal(result.agent, 'fast-coder')
  const sent = live.sessions.calls.filter(([name]) => name === 'SendPrompt' || name === 'SendPromptAsync')
  assert.ok(sent.length >= 1, 'the first prompt must be sent')
  const text = JSON.stringify(sent.map(([, ...args]) => args))
  assert.match(text, /add the parser/)
  live.cleanup()
})

test('FORK_create_session_failure_surfaces_host_error', async () => {
  const live = liveScope({ createError: 'host refused the fork' })
  const spec = managerSpec(factory, live.scope)
  const result = parseToml(await runManager(spec, 'fast-coder', 'implement the feature'))
  assert.equal(result.error, 'host refused the fork')
  live.cleanup()
})

test('FORK_handle_shaped_unknown_agent_reports_id', async () => {
  const live = liveScope()
  const spec = managerSpec(factory, live.scope)
  const result = parseToml(await runManager(spec, 'zz9900', 'do work'))
  assert.match(result.error, /Unknown agent id: zz9900/)
  live.cleanup()
})

// ── reuse path: fork once, then nudge by agent_id ────────────────────────────

test('FORK_existing_agent_reuses_with_composed_assignment', async () => {
  const live = liveScope()
  const spec = managerSpec(factory, live.scope)

  const first = parseToml(await runManager(spec, 'fast-coder', 'implement the feature'))
  assert.equal(first.agent, 'fast-coder')
  const agentId = first.agent_id

  const reused = parseToml(await runManager(spec, agentId, 'continue the work'))
  assert.equal(reused.agent_id, agentId)
  assert.equal(reused.agent, 'fast-coder')
  assert.equal(reused.role, 'coder')

  // Exactly one spawn: the reuse nudged the existing child.
  const created = live.sessions.calls.filter(([name]) => name === 'CreateChildSession')
  assert.equal(created.length, 1, 'reuse must not spawn a second session')
  live.cleanup()
})

test('FORK_reuse_of_hidden_role_is_denied_generically', async () => {
  const live = liveScope()
  const spec = managerSpec(factory, live.scope)

  // Plant a reviewer child directly through the runtime (bypassing the tool's
  // own creation gate), then try to nudge it by agent id.
  const reviewerRuntime = live.runtime
  const forked = await reviewerRuntime.Fork(
    'rw0001',
    undefined,
    'fast-reviewer',
    'review the diff',
    undefined,
    undefined,
    undefined,
    undefined,
    undefined,
  )
  assert.equal(forked.tag, 0, forked.tag === 1 ? forked.fields[0] : '')

  const denied = parseToml(await runManager(spec, 'rw0001', 'nudge the reviewer'))
  assert.equal(denied.error, 'Unknown or unavailable managed agent.')
  live.cleanup()
})

// ── orchestrator fork-manager ────────────────────────────────────────────────
//
// A REAL OrchestratorHost whose engine cell is seeded with a REAL Orchestrator
// over a fake GitPort / ManagerPort. The publication program runs in the
// background against the same fakes and terminates (AwaitManager → Ok).

const fakeGitPort = () => ({
  IsDirty: async () => false,
  CreateWorktree: async (jobId, path) => ({
    tag: 0,
    fields: [{ fields: [`manager/${jobId.fields?.[0] ?? jobId}`], tag: 0, cases: () => ['WorktreeIdentity'] }],
  }),
  FreezeTargetBranch: async () => ({ tag: 0, fields: [targetRef('main')] }),
  Rebase: async () => ({ tag: 0, fields: [] }),
  ConflictedFiles: async () => ({ tag: 0, fields: [toList([])] }),
  FfMerge: async () => ({ tag: 0, fields: [commitHash('cafe01')] }),
  RemoveWorktree: async () => ({ tag: 0, fields: [] }),
  HasRebaseHead: async () => false,
  ListWorktrees: async () => ({ tag: 0, fields: [toList([])] }),
  ListManagerBranches: async () => ({ tag: 0, fields: [toList([])] }),
  DeleteBranch: async () => ({ tag: 0, fields: [] }),
  ReadHead: async () => ({ tag: 0, fields: [commitHash('beef02')] }),
  GetTargetHead: async () => ({ tag: 0, fields: [commitHash('beef02')] }),
})

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

/** Real OrchestratorHost + seeded real engine; journal is a real AgentJournal. */
const liveOrchestrator = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-orchtool-'))
  const opened = agentJournal.create({ directory: dir })
  assert.equal(opened.ok, true, 'journal must open')

  const managerCalls = []
  const engine = createOrchestrator(
    fakeGitPort(),
    fakeManagerPort(managerCalls),
    '/repo',
    targetRef('main'),
    {
      AppendFact: (streamId, factValue) => {
        const appended = agentJournal.appendAgent(streamId, undefined, factValue, opened.journal)
        return appended.ok ? { tag: 0, fields: [appended.value] } : { tag: 1, fields: ['append failed'] }
      },
      Snapshot: () => agentJournal.snapshot(opened.journal),
    },
    '/repo',
  )

  const sessions = fakeSessions()
  const deps = new OrchestratorHostDeps(
    sessions,
    opened.journal,
    undefined,
    () => {},
    () => {},
    () => {},
    () => {},
    '/repo',
    '',
    () => undefined,
    () => undefined,
  )
  const host = new OrchestratorHost(deps, makeSessionId('ses_fork'))
  host.engineInstance = engine

  const scope = bareScope({ orchestratorHost: host, sessions })
  return {
    scope,
    host,
    engine,
    managerCalls,
    journal: opened.journal,
    cleanup: () => {
      try {
        opened.dispose()
      } catch {}
      rmSync(dir, { recursive: true, force: true })
    },
  }
}

test('FORK_orchestrator_forks_a_public_manager_job', async () => {
  const live = liveOrchestrator()
  const spec = orchestratorSpec(factory, live.scope)

  const result = parseToml(await spec.Execute(makeArgs({ agent: 'fast-manager', prompt: 'build the thing' }), context()))

  assert.equal(result.agent, 'fast-manager')
  assert.equal(result.role, 'manager')
  assert.ok(result.worktree, 'worktree path present')
  assert.ok(result.agent_id, 'job id present')
  assert.equal(live.managerCalls.filter(([name]) => name === 'StartManager').length, 1)
  live.cleanup()
})

test('FORK_orchestrator_rejects_non_manager_agents', async () => {
  const spec = orchestratorSpec(factory, bareScope({ orchestratorHost: {} }))
  const result = parseToml(await spec.Execute(makeArgs({ agent: 'fast-coder', prompt: 'x' }), context()))
  assert.match(result.error, /only fork fast-manager or deep-manager/)
})

test('FORK_orchestrator_reuses_existing_job_by_handle_id', async () => {
  const live = liveOrchestrator()
  const spec = orchestratorSpec(factory, live.scope)

  // Fork a job first so the projection has an active record, then continue it.
  const forked = parseToml(await spec.Execute(makeArgs({ agent: 'fast-manager', prompt: 'build the thing' }), context()))
  assert.ok(forked.agent_id, 'job id present')

  const continued = parseToml(await spec.Execute(makeArgs({ agent: forked.agent_id, prompt: 'also do this' }), context()))
  assert.equal(continued.reused, 'true')
  assert.equal(continued.agent_id, forked.agent_id)
  assert.ok(continued.worktree, 'worktree path present')
  assert.deepEqual(
    live.managerCalls.filter(([name]) => name === 'ResumeManager'),
    [['ResumeManager', 'also do this']],
  )
  live.cleanup()
})

test('FORK_orchestrator_continue_unknown_job_is_an_error', async () => {
  const live = liveOrchestrator()
  const spec = orchestratorSpec(factory, live.scope)
  const result = parseToml(await spec.Execute(makeArgs({ agent: 'mj9999', prompt: 'nobody home' }), context()))
  assert.match(result.error, /Unknown manager job: mj9999/)
  live.cleanup()
})

test('FORK_orchestrator_missing_session_is_refused', async () => {
  const spec = orchestratorSpec(factory, bareScope({ orchestratorHost: {} }))
  const emptyContext = new HostToolContext('', undefined, undefined, undefined, undefined, () => () => {})
  const result = parseToml(await spec.Execute(makeArgs({ agent: 'fast-manager', prompt: 'x' }), emptyContext))
  assert.match(result.error, /Missing sessionID/)
})

test('FORK_orchestrator_dirty_repo_rejects_the_fork', async () => {
  const live = liveOrchestrator()
  live.engine.git.IsDirty = async () => true
  const spec = orchestratorSpec(factory, live.scope)
  const result = parseToml(await spec.Execute(makeArgs({ agent: 'fast-manager', prompt: 'x' }), context()))
  assert.ok(result.error, 'a dirty repo must reject the fork')
  live.cleanup()
})

test('FORK_specs_expose_expected_names', () => {
  assert.equal(managerSpec(factory, bareScope()).Name, 'fork')
  assert.equal(orchestratorSpec(factory, bareScope({ orchestratorHost: {} })).Name, 'fork-manager')
})
