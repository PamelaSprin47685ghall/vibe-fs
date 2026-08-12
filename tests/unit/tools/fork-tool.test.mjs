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

import { agentJournal, listItems, sessionId, toList } from '../support/domain.mjs'

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
const bareScope = ({ orchestratorHost, sessions, journal } = {}) => {
  const scope = new ToolRuntimeScope(
    sessions,
    journal,
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

test('FORK_name_without_calling_is_continuation_only', async () => {
  const spec = managerSpec(factory, bareScope())
  const result = await runManager(spec, 'Ada', 'do work')
  assert.match(result, /No continuing person is known by that name/)
  assert.doesNotMatch(result, /fast-|deep-|agent id/i)
})

test('FORK_disposed_scope_surfaces_natural_execution_consequence', async () => {
  const live = liveScope()
  live.scope.disposed = true
  const spec = managerSpec(factory, live.scope)
  const result = await runManager(spec, 'Ada', 'do work', { calling: 'coder' })
  assert.doesNotMatch(result, /disposed|\berror\s*=/i)
  assert.match(result, /cannot be placed from this execution context/i)
  live.cleanup()
})

test('FORK_unavailable_calling_is_denied_generically', async () => {
  const spec = managerSpec(factory, bareScope())
  const result = await runManager(spec, 'Rhea', 'review this', { calling: 'examiner' })
  assert.match(result, /Unknown or unavailable calling/)
  assert.doesNotMatch(result, /Reviewer|fast-|deep-|\berror\s*=/i)
})

test('FORK_unknown_calling_is_generic_and_does_not_dump_machine_bindings', async () => {
  const spec = managerSpec(factory, bareScope())
  const result = await runManager(spec, 'Ada', 'do work', { calling: 'wizard' })
  assert.match(result, /Unknown or unavailable calling/)
  assert.doesNotMatch(result, /fast-|deep-|\berror\s*=/i)
})

// ── fresh fork path (real runtime + journal) ─────────────────────────────────

test('FORK_calling_creates_machine_agent_but_returns_only_byname', async () => {
  const live = liveScope()
  const spec = managerSpec(factory, live.scope)
  const text = await runManager(spec, 'Ada', 'implement the feature', { calling: 'coder' })

  assert.match(text, /Ada carries this charge now/)
  assert.doesNotMatch(text, /fast-coder|agent_id|\berror\s*=/)

  const created = live.sessions.calls.filter(([name]) => name === 'CreateChildSession')
  assert.equal(created.length, 1, 'exactly one child session')
  assert.equal(created[0][1].Agent, 'fast-coder', 'calling resolves to Host machine binding')
  live.cleanup()
})

test('FORK_create_session_failure_surfaces_only_public_consequence', async () => {
  const live = liveScope({ createError: 'host refused the fork' })
  const spec = managerSpec(factory, live.scope)
  const result = await runManager(spec, 'Ada', 'implement the feature', { calling: 'coder' })
  assert.match(result, /The charge could not be placed/)
  assert.doesNotMatch(result, /host refused|\berror\s*=/i)
  live.cleanup()
})

test('FORK_unknown_byname_does_not_echo_internal_identity', async () => {
  const live = liveScope()
  const spec = managerSpec(factory, live.scope)
  const result = await runManager(spec, 'Nobody Here', 'do work')
  assert.match(result, /No continuing person is known by that name/)
  assert.doesNotMatch(result, /agent id|fast-|deep-|\berror\s*=/i)
  live.cleanup()
})

// ── reuse path: create by calling, continue by Byname ───────────────────────

test('FORK_existing_person_is_resolved_by_byname_not_agent_id', async () => {
  const live = liveScope()
  const spec = managerSpec(factory, live.scope)

  const createdText = await runManager(spec, 'Ada', 'implement the feature', { calling: 'coder' })
  assert.match(createdText, /Ada carries this charge now/)

  const reused = await runManager(spec, 'Ada', 'continue the work')
  assert.doesNotMatch(reused, /No continuing person|fast-coder|agent id/i)

  const created = live.sessions.calls.filter(([name]) => name === 'CreateChildSession')
  assert.equal(created.length, 1, 'Byname continuation must not spawn a second session')
  live.cleanup()
})

test('FORK_same_byname_cannot_be_reborn_with_a_new_calling', async () => {
  const live = liveScope()
  const spec = managerSpec(factory, live.scope)

  assert.match(await runManager(spec, 'Ada', 'first charge', { calling: 'coder' }), /Ada carries/)
  const denied = await runManager(spec, 'Ada', 'different person', { calling: 'engineer' })
  assert.match(denied, /name already belongs to someone/i)
  assert.doesNotMatch(denied, /fast-|deep-|agent id/i)

  const created = live.sessions.calls.filter(([name]) => name === 'CreateChildSession')
  assert.equal(created.length, 1, 'Byname is not reusable for a different logical person')
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

  const scope = bareScope({ orchestratorHost: host, sessions, journal: opened.journal })
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

test('FORK_orchestrator_calling_opens_machine_manager_but_returns_only_road_byname', async () => {
  const live = liveOrchestrator()
  const spec = orchestratorSpec(factory, live.scope)

  const text = await spec.Execute(
    makeArgs({ calling: 'coordinator', name: 'North Road', charge: 'build the thing' }),
    context(),
  )

  assert.match(text, /North Road has taken your charge/)
  assert.doesNotMatch(text, /fast-manager|job_id|worktree|\berror\s*=/i)
  const starts = live.managerCalls.filter(([name]) => name === 'StartManager')
  assert.equal(starts.length, 1)
  assert.equal(starts[0][1].ManagerAgent, 'fast-manager')
  live.cleanup()
})

test('FORK_orchestrator_rejects_unknown_calling_without_binding_names', async () => {
  const spec = orchestratorSpec(factory, bareScope({ orchestratorHost: {} }))
  const result = await spec.Execute(makeArgs({ calling: 'coder', name: 'Road', charge: 'x' }), context())
  assert.match(result, /Unknown or unavailable calling/)
  assert.doesNotMatch(result, /fast-manager|deep-manager|\berror\s*=/i)
})

test('FORK_orchestrator_resolves_continuation_by_road_byname', async () => {
  const live = liveOrchestrator()
  const spec = orchestratorSpec(factory, live.scope)

  const forkedText = await spec.Execute(
    makeArgs({ calling: 'coordinator', name: 'North Road', charge: 'build the thing' }),
    context(),
  )
  assert.match(forkedText, /North Road has taken your charge/)

  const continued = await spec.Execute(makeArgs({ name: 'North Road', charge: 'keep going' }), context())
  assert.doesNotMatch(continued, /No continuing road|manager job|fast-manager|job_id/i)
  live.cleanup()
})

test('FORK_orchestrator_unknown_continuation_is_a_natural_consequence', async () => {
  const live = liveOrchestrator()
  const spec = orchestratorSpec(factory, live.scope)
  const result = await spec.Execute(makeArgs({ name: 'Unknown Road', charge: 'nobody home' }), context())
  assert.match(result, /No continuing road is known by that name/)
  assert.doesNotMatch(result, /manager job|\berror\s*=/i)
  live.cleanup()
})

test('FORK_orchestrator_missing_authority_is_refused_without_session_identity', async () => {
  const spec = orchestratorSpec(factory, bareScope({ orchestratorHost: {} }))
  const emptyContext = new HostToolContext('', undefined, undefined, undefined, undefined, () => () => {})
  const result = await spec.Execute(
    makeArgs({ calling: 'coordinator', name: 'North Road', charge: 'x' }),
    emptyContext,
  )
  assert.match(result, /caller's authority is established/)
  assert.doesNotMatch(result, /sessionID|\berror\s*=/i)
})

test('FORK_orchestrator_dirty_repo_rejects_the_road_without_internal_detail', async () => {
  const live = liveOrchestrator()
  live.engine.git.IsDirty = async () => true
  const spec = orchestratorSpec(factory, live.scope)
  const result = await spec.Execute(
    makeArgs({ calling: 'coordinator', name: 'North Road', charge: 'x' }),
    context(),
  )
  assert.match(result, /That road could not be opened/)
  assert.doesNotMatch(result, /dirty|worktree|\berror\s*=/i)
  live.cleanup()
})

test('FORK_specs_expose_expected_names_and_only_manager_fork_carries_keywords', () => {
  const fork = managerSpec(factory, bareScope())
  const commission = orchestratorSpec(factory, bareScope({ orchestratorHost: {} }))
  assert.equal(fork.Name, 'fork')
  assert.equal(commission.Name, 'commission')
  assert.deepEqual(listItems(fork.Arguments).map(([name]) => name), ['calling', 'name', 'charge', 'keywords'])
  assert.deepEqual(listItems(commission.Arguments).map(([name]) => name), ['calling', 'name', 'charge'])
})

test('FORK_non_repository_target_rejects_nonempty_warm_start_keywords_before_creation', async () => {
  const live = liveScope()
  const spec = managerSpec(factory, live.scope)
  const result = await runManager(spec, 'Web Road', 'browse', { calling: 'navigator', keywords: 'repository clue' })
  assert.match(result, /only available when fork targets Coder, Inspector, or DevOps/)
  assert.doesNotMatch(result, /\berror\s*=/i)
  assert.equal(listItems(listRuntimeAgents(live.runtime)[0]).length, 0)
  live.cleanup()
})