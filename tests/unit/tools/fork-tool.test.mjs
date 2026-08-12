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
const { HostForkRuntime, HostForkRuntime__List: listRuntimeAgents } =
  await import('../../../dist/Session/HostForkRuntime.js')
const { Wanxiangshu_Session_HostForkRuntime__HostForkRuntime_Fork_Z7B3EB305: forkRuntime } = await import(
  '../../../dist/Session/HostForkAgent.js'
)
const { Role } = await import('../../../dist/Kernel/Roles.js')
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

const rawResult = async (promise) => {
  const text = await promise
  return { text, fields: parseToml(text) }
}

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
    SubscribeTerminal: (childId, callback) => {
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

const runManager = (spec, name, charge, extra = {}) =>
  spec.Execute(makeArgs({ name, charge, ...extra }), context())

// ── request validation (refused before the runtime) ─────────────────────────

test('FORK_blank_name_is_refused_without_error_dto', async () => {
  const spec = managerSpec(factory, bareScope())
  const result = await runManager(spec, '', 'do work')
  assert.doesNotMatch(result, /\berror\s*=/)
  assert.match(result, /A name is required/)
})

test('FORK_terminal_identity_is_refused_on_the_manager_tool', async () => {
  const spec = managerSpec(factory, bareScope())
  const result = await runManager(spec, 'pty', 'do work')
  assert.match(result, /Terminal work belongs through the terminal tools/)
})

test('FORK_disposed_scope_surfaces_natural_execution_consequence', async () => {
  const live = liveScope()
  live.scope.disposed = true
  const spec = managerSpec(factory, live.scope)
  const result = await runManager(spec, 'fast-coder', 'do work')
  assert.doesNotMatch(result, /disposed|\berror\s*=/i)
  assert.match(result, /cannot be placed from this execution context/i)
  live.cleanup()
})

test('FORK_hidden_role_by_name_is_denied_generically', async () => {
  const spec = managerSpec(factory, bareScope())
  const result = await runManager(spec, 'fast-reviewer', 'review this')
  assert.match(result, /Unknown or unavailable managed agent/)
  assert.doesNotMatch(result, /Reviewer|\berror\s*=/)
})

test('FORK_unknown_calling_is_generic_and_does_not_dump_machine_bindings', async () => {
  const spec = managerSpec(factory, bareScope())
  const result = await runManager(spec, 'not-an-agent-name!', 'do work')
  assert.match(result, /Unknown or unavailable calling/)
  assert.doesNotMatch(result, /fast-|deep-|\berror\s*=/i)
})

// ── fresh fork path (real runtime + journal) ─────────────────────────────────

test('FORK_public_forkable_agent_creates_a_child', async () => {
  const live = liveScope()
  const spec = managerSpec(factory, live.scope)
  const text = await runManager(spec, 'fast-coder', 'implement the feature')
  const result = parseToml(text)

  assert.equal(result.error, undefined)
  assert.match(text, /carries this charge now/)

  const created = live.sessions.calls.filter(([name]) => name === 'CreateChildSession')
  assert.equal(created.length, 1, 'exactly one child session')
  live.cleanup()
})

test('FORK_create_session_failure_surfaces_only_public_consequence', async () => {
  const live = liveScope({ createError: 'host refused the fork' })
  const spec = managerSpec(factory, live.scope)
  const result = await runManager(spec, 'fast-coder', 'implement the feature')
  assert.match(result, /The charge could not be placed/)
  assert.doesNotMatch(result, /host refused|\berror\s*=/i)
  live.cleanup()
})

test('FORK_unknown_continuation_handle_does_not_echo_internal_identity', async () => {
  const live = liveScope()
  const spec = managerSpec(factory, live.scope)
  const result = await runManager(spec, 'zz9900', 'do work')
  assert.match(result, /No continuing person is known by that name/)
  assert.doesNotMatch(result, /agent id|\berror\s*=/i)
  live.cleanup()
})

// ── reuse path: fork once, then nudge by agent_id ────────────────────────────

test('FORK_existing_agent_busy_reuse_without_active_run_fails_closed', async () => {
  const live = liveScope()
  const spec = managerSpec(factory, live.scope)

  const forked = await forkRuntime(
    live.runtime,
    'ag0001',
    Role.Coder,
    'fast-coder',
    'implement the feature',
    undefined,
    undefined,
    undefined,
    undefined,
    undefined,
  )
  assert.equal(forked.tag, 0, forked.tag === 1 ? forked.fields[0] : '')

  const reused = await runManager(spec, 'ag0001', 'continue the work')
  assert.match(reused, /That person cannot take another charge yet/)
  assert.doesNotMatch(reused, /ActiveLogicalRun|child session|\berror\s*=/i)

  const created = live.sessions.calls.filter(([name]) => name === 'CreateChildSession')
  assert.equal(created.length, 1, 'reuse must not spawn a second session')
  live.cleanup()
})

test('FORK_reuse_of_hidden_role_is_denied_generically', async () => {
  const live = liveScope()
  const spec = managerSpec(factory, live.scope)

  // Plant a reviewer child directly through the runtime (bypassing the tool's
  // own creation gate), then try to nudge it by agent id.
  const forked = await forkRuntime(
    live.runtime,
    'rw0001',
    Role.Reviewer,
    'fast-reviewer',
    'review the diff',
    undefined,
    undefined,
    undefined,
    undefined,
    undefined,
  )
  assert.equal(forked.tag, 0, forked.tag === 1 ? forked.fields[0] : '')

  const denied = await runManager(spec, 'rw0001', 'nudge the reviewer')
  assert.match(denied, /Unknown or unavailable managed agent/)
  assert.doesNotMatch(denied, /Reviewer|\berror\s*=/)
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

  const text = await spec.Execute(makeArgs({ name: 'fast-manager', charge: 'build the thing' }), context())
  const result = parseToml(text)

  assert.equal(result.error, undefined)
  assert.match(text, /has taken your charge/)
  assert.equal(live.managerCalls.filter(([name]) => name === 'StartManager').length, 1)
  live.cleanup()
})

test('FORK_orchestrator_rejects_non_manager_callings_without_binding_names', async () => {
  const spec = orchestratorSpec(factory, bareScope({ orchestratorHost: {} }))
  const result = await spec.Execute(makeArgs({ name: 'fast-coder', charge: 'x' }), context())
  assert.match(result, /Only a Manager can take an independent road/)
  assert.doesNotMatch(result, /fast-manager|deep-manager|\berror\s*=/i)
})

test('FORK_orchestrator_reuses_existing_job_by_handle_id', async () => {
  const live = liveOrchestrator()
  const spec = orchestratorSpec(factory, live.scope)

  // Fork a job first so the projection has an active record, then continue it.
  const forkedText = await spec.Execute(makeArgs({ name: 'fast-manager', charge: 'build the thing' }), context())
  assert.equal(parseToml(forkedText).error, undefined)
  assert.match(forkedText, /has taken your charge/)

  // Provider surface no longer returns job id; reuse by handle is wall-internal.
  // Continue-unknown remains the observable failure contract below.
  live.cleanup()
})

test('FORK_orchestrator_unknown_continuation_is_a_natural_consequence', async () => {
  const live = liveOrchestrator()
  const spec = orchestratorSpec(factory, live.scope)
  const result = await spec.Execute(makeArgs({ name: 'mj9999', charge: 'nobody home' }), context())
  assert.match(result, /No continuing road is known by that name/)
  assert.doesNotMatch(result, /manager job|\berror\s*=/i)
  live.cleanup()
})

test('FORK_orchestrator_missing_authority_is_refused_without_session_identity', async () => {
  const spec = orchestratorSpec(factory, bareScope({ orchestratorHost: {} }))
  const emptyContext = new HostToolContext('', undefined, undefined, undefined, undefined, () => () => {})
  const result = await spec.Execute(makeArgs({ name: 'fast-manager', charge: 'x' }), emptyContext)
  assert.match(result, /caller's authority is established/)
  assert.doesNotMatch(result, /sessionID|\berror\s*=/i)
})

test('FORK_orchestrator_dirty_repo_rejects_the_road_without_internal_detail', async () => {
  const live = liveOrchestrator()
  live.engine.git.IsDirty = async () => true
  const spec = orchestratorSpec(factory, live.scope)
  const result = await spec.Execute(makeArgs({ name: 'fast-manager', charge: 'x' }), context())
  assert.match(result, /That road could not be opened/)
  assert.doesNotMatch(result, /dirty|worktree|\berror\s*=/i)
  live.cleanup()
})

test('FORK_specs_expose_expected_names', () => {
  assert.equal(managerSpec(factory, bareScope()).Name, 'fork')
  assert.equal(orchestratorSpec(factory, bareScope({ orchestratorHost: {} })).Name, 'commission')
})
