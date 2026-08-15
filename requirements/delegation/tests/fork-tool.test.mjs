// Split from tests/unit/tools/fork-tool.test.mjs (cutover Wave 2a); owner: delegation
// VERIFY-009 coverage: Manager fork/nudge tool.
//
// The tool calls module-level HostForkRuntime functions, so the runtime is REAL:
// built on a fake ISessionHostPort plus a real AgentJournal in a temp dir. Fork,
// reuse, linkage and prompt-claim paths under test are then production code.

import assert from 'node:assert/strict'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import {
  agentJournal,
  lifecycleWorkRecordProjection,
  listItems,
  promptDispatcher,
  sessionId,
  toList,
  transportReceipt,
} from '../../verification-system/tests/support/domain.mjs'

const { instructions } = await import('../../../dist/Execution/Delegation/Fork/Surface.js')
const en = instructions('en')

const {
  HostToolArguments_$ctor_4E60E31B: makeArgs,
  HostToolContext,
  ToolHostCodec_factory,
} = await import('../../../dist/OpenCode/Codec/ToolHostCodec.js')
const { managerSpec, orchestratorSpec } = await import('../../../dist/Execution/Delegation/Fork/OpenCode/Tool.js')
const { ToolRuntimeScope } = await import('../../../dist/OpenCode/Tools/ToolRuntimeScope.js')
const hostRuntimeModule = await import('../../../dist/Execution/Delegation/Fork/Host/Runtime.js')
const { HostForkRuntime, HostForkRuntime__List: listRuntimeAgents, HostForkRuntime__get_PendingRuns: pendingRunsOf } = hostRuntimeModule
const failRun = Object.entries(hostRuntimeModule).find(([k]) => k.startsWith('HostForkRuntime__FailRun_'))?.[1]
const agentModule = await import('../../../dist/Execution/Delegation/Fork/Host/Agent.js')
const forkRuntime = Object.entries(agentModule).find(([k]) => k.includes('HostForkRuntime_Fork_'))?.[1]
const { Role } = await import('../../../dist/Foundation/Roles.js')
const { DelegatedToolEstimateProjection_remaining: estimateRemaining } = await import(
  '../../../dist/Execution/Delegation/DelegatedToolEstimateProjection.js'
)
const { OrchestratorHost, OrchestratorHost__JoinPublished: hostJoinPublished } = await import('../../../dist/Change/Host/Host.js')
const { OrchestratorHostDeps } = await import('../../../dist/Change/Host/Types.js')
const changeRuntimeModule = await import('../../../dist/Change/Runtime.js')
const createOrchestrator = Object.entries(changeRuntimeModule).find(([k]) => k.startsWith('Orchestrator_$ctor'))?.[1] ?? ((...args) => new changeRuntimeModule.Orchestrator(...args))
const { targetRef, commitHash, managerJobId, sessionId: makeSessionId, fact, stream } = await import('../../verification-system/tests/support/domain.mjs')

const chain = (kind, extra = {}) => ({
  kind,
  ...extra,
  int: () => chain(`${kind}-int`, extra),
  nonnegative: () => chain(`${kind}-nonnegative`, extra),
  describe: (description) => chain(`${kind}-described`, { ...extra, description }),
  optional: () => chain(`${kind}-optional`, extra),
})
const fakeSchema = {
  string: () => chain('string'),
  number: () => chain('number'),
  enum: (values) => chain('enum', { values }),
  union: (parts) => chain('union', { parts }),
}
const factory = ToolHostCodec_factory({ tool: { schema: fakeSchema } })

const context = (sessionId = 'ses_fork') =>
  new HostToolContext(sessionId, undefined, undefined, undefined, undefined, () => () => {})

const PARENT = sessionId('ses_fork')

const fakeSessions = (behaviour = {}) => {
  const calls = []
  let childSeq = 0
  let physicalSeq = 0
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
      physicalSeq += 1
      if (!behaviour.physicalAccept) {
        return promptDispatcher.admittedWithReceipt(transportReceipt(`receipt-${physicalSeq}`))
      }
      return promptDispatcher.admittedWithPhysicalMessage(`physical-${physicalSeq}`)
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
const liveScope = async (behaviour = {}) => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-forktool-'))
  const opened = await agentJournal.create({ directory: dir })
  assert.equal(opened.ok, true, 'journal must open')

  const sessions = fakeSessions(behaviour)
  const openingRecord = (sid) => lifecycleWorkRecordProjection.lifecycleWorkRecord(opened.journal, sid, true)
  const childRecord = (sid) => lifecycleWorkRecordProjection.lifecycleWorkRecord(opened.journal, sid, false)
  const runtime = new HostForkRuntime(
    PARENT,
    sessions,
    opened.journal,
    undefined, // onChildCreated
    undefined, // onChildCreatedDir
    undefined, // ptyPort
    undefined, // directoryFor
    undefined, // onRunStarted
    openingRecord,
    childRecord,
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
    (sid) => openingRecord(sessionId(sid)),
    (sid) => childRecord(sessionId(sid)),
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

test('DELEG_022_expected_tool_calls_rejects_negative_and_fractional_values_before_fork', async () => {
  for (const invalid of [-1, 1.5]) {
    const live = await liveScope()
    const spec = managerSpec(factory, live.scope)
    const result = await runManager(spec, 'Ada', 'do work', {
      calling: 'coder',
      expected_tool_calls: invalid,
    })
    assert.match(result, /non-negative integer|nonnegative integer|非负整数/i)
    assert.equal(live.sessions.calls.filter(([name]) => name === 'CreateChildSession').length, 0)
    live.cleanup()
  }
})

// ── fresh fork path (real runtime + journal) ─────────────────────────────────

test('FORK_calling_creates_machine_agent_but_returns_only_byname', async () => {
  const live = await liveScope()
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
  const live = await liveScope({ createError: 'host refused the fork' })
  const spec = managerSpec(factory, live.scope)
  const result = await runManager(spec, 'Ada', 'implement the feature', { calling: 'coder' })
  assert.match(result, /The charge could not be placed/)
  assert.doesNotMatch(result, /host refused|\berror\s*=/i)
  live.cleanup()
})

test('FORK_unknown_byname_does_not_echo_internal_identity', async () => {
  const live = await liveScope()
  const spec = managerSpec(factory, live.scope)
  const result = await runManager(spec, 'Nobody Here', 'do work')
  assert.match(result, /No continuing person is known by that name/)
  assert.doesNotMatch(result, /agent id|fast-|deep-|\berror\s*=/i)
  live.cleanup()
})

test('DELEG_021_unknown_attachment_is_refused_before_child_creation', async () => {
  const live = await liveScope()
  const spec = managerSpec(factory, live.scope)
  const result = await runManager(spec, 'Bob', 'do work', { calling: 'coder', attach: 'Ghost' })

  assert.match(result, /attachment.*name|known.*attachment/i)
  assert.doesNotMatch(result, /agent id|fast-|deep-|session/i)
  assert.equal(live.sessions.calls.filter(([name]) => name === 'CreateChildSession').length, 0)
  live.cleanup()
})

test('DELEG_021_self_attachment_is_refused_before_child_creation', async () => {
  const live = await liveScope()
  const spec = managerSpec(factory, live.scope)
  const result = await runManager(spec, 'Bob', 'do work', { calling: 'coder', attach: 'Bob' })

  assert.match(result, /cannot.*attach.*itself|cannot.*attach.*own|不能.*附/i)
  assert.equal(live.sessions.calls.filter(([name]) => name === 'CreateChildSession').length, 0)
  live.cleanup()
})

test('DELEG_021_fresh_fork_materializes_named_person_lwr_as_background', async () => {
  const live = await liveScope()
  const spec = managerSpec(factory, live.scope)

  assert.match(await runManager(spec, 'Ada', 'trace the retry path', { calling: 'coder' }), /Ada carries/)
  assert.match(
    await runManager(spec, 'Bob', 'use the existing evidence', { calling: 'coder', attach: 'Ada' }),
    /Bob carries/,
  )

  const prompts = live.sessions.calls.filter(([name]) => name === 'SendPrompt' || name === 'SendPromptAsync')
  const bobPrompt = prompts.at(-1)[2]
  assert.ok(bobPrompt.includes(en.Attachment))
  assert.match(bobPrompt, /trace the retry path/)
  assert.ok(
    bobPrompt.indexOf(en.Attachment) < bobPrompt.indexOf('trace the retry path'),
    'the canonical attachment framing precedes the attached LWR',
  )
  live.cleanup()
})

test('DELEG_021_busy_reuse_does_not_materialize_attachment_and_reports_deferral', async () => {
  const live = await liveScope({ physicalAccept: true })
  const spec = managerSpec(factory, live.scope)

  assert.match(await runManager(spec, 'Ada', 'trace the retry path', { calling: 'coder' }), /Ada carries/)
  assert.match(await runManager(spec, 'Bob', 'primary work', { calling: 'coder' }), /Bob carries/)

  const before = live.sessions.calls.length
  const result = await runManager(spec, 'Bob', 'add this charge too', { attach: 'Ada' })
  const afterCalls = live.sessions.calls.slice(before)

  assert.match(result, /busy.*attachment|attachment.*not.*attach|attachment.*not.*added/i)
  const promptTexts = afterCalls
    .filter(([name]) => name === 'SendPrompt' || name === 'SendPromptAsync')
    .map((call) => call[2])
  assert.ok(promptTexts.every((text) => !String(text).includes(en.Attachment)))
  assert.ok(promptTexts.every((text) => !String(text).includes('trace the retry path')))
  live.cleanup()
})

// ── reuse path: create by calling, continue by Byname ───────────────────────

test('FORK_existing_person_is_resolved_by_byname_not_agent_id', async () => {
  const live = await liveScope()
  const spec = managerSpec(factory, live.scope)

  const createdText = await runManager(spec, 'Ada', 'implement the feature', { calling: 'coder' })
  assert.match(createdText, /Ada carries this charge now/)

  const reused = await runManager(spec, 'Ada', 'continue the work')
  assert.doesNotMatch(reused, /No continuing person|fast-coder|agent id/i)

  const created = live.sessions.calls.filter(([name]) => name === 'CreateChildSession')
  assert.equal(created.length, 1, 'Byname continuation must not spawn a second session')
  live.cleanup()
})

test('DELEG_022_fork_explicit_replace_and_omitted_reuse_retains_remaining', async () => {
  const live = await liveScope({ physicalAccept: true })
  const spec = managerSpec(factory, live.scope)
  const child = sessionId('child-1')
  const remaining = () => {
    const state = agentJournal.snapshot(live.journal).AgentProjections.Sessions.get(child).DelegatedToolEstimate
    return estimateRemaining(state)
  }

  assert.match(
    await runManager(spec, 'Ada', 'implement the feature', { calling: 'coder', expected_tool_calls: 3 }),
    /Ada carries/,
  )
  assert.equal(remaining(), 3)

  assert.match(await runManager(spec, 'Ada', 'continue without recalibration'), /Ada carries/)
  assert.equal(remaining(), 3, 'omitting expected_tool_calls must retain current remaining')

  assert.match(
    await runManager(spec, 'Ada', 'recalibrate current work', { expected_tool_calls: 7 }),
    /Ada carries/,
  )
  assert.equal(remaining(), 7, 'an explicit estimate replaces the current remaining even on busy reuse')
  live.cleanup()
})

test('FORK_engineer_continuation_keeps_deep_coder', async () => {
  const live = await liveScope()
  const spec = managerSpec(factory, live.scope)

  assert.match(await runManager(spec, 'Ada', 'implement the feature', { calling: 'engineer' }), /Ada carries/)

  for (const run of pendingRunsOf(live.runtime).values()) {
    failRun(live.runtime, run, 'settled')
  }

  assert.match(await runManager(spec, 'Ada', 'continue the work'), /Ada carries/)

  const created = live.sessions.calls.filter(([name]) => name === 'CreateChildSession')
  assert.equal(created.length, 1, 'Byname continuation must not spawn a second session')
  assert.equal(created[0][1].Agent, 'deep-coder')

  const prompts = live.sessions.calls.filter(([name]) => name === 'SendPrompt' || name === 'SendPromptAsync')
  assert.equal(prompts.length, 2)
  for (const prompt of prompts) {
    assert.equal(prompt[3]?.Agent, 'deep-coder')
  }
  live.cleanup()
})

test('FORK_same_byname_cannot_be_reborn_with_a_new_calling', async () => {
  const live = await liveScope()
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
const liveOrchestrator = async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-orchtool-'))
  const opened = await agentJournal.create({ directory: dir })
  assert.equal(opened.ok, true, 'journal must open')

  const managerCalls = []
  const engine = createOrchestrator(
    fakeGitPort(),
    fakeManagerPort(managerCalls),
    '/repo',
    targetRef('main'),
    {
      AppendFact: async (streamId, factValue) => {
        const appended = await agentJournal.appendAgent(streamId, undefined, factValue, opened.journal)
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
    async () => undefined,
    async () => undefined,
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
  const live = await liveOrchestrator()
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
  await hostJoinPublished(live.host)
  live.cleanup()
})

test('FORK_orchestrator_resolves_continuation_by_road_byname', async () => {
  const live = await liveOrchestrator()
  const spec = orchestratorSpec(factory, live.scope)

  const forkedText = await spec.Execute(
    makeArgs({ calling: 'coordinator', name: 'North Road', charge: 'build the thing' }),
    context(),
  )
  assert.match(forkedText, /North Road has taken your charge/)

  const continued = await spec.Execute(makeArgs({ name: 'North Road', charge: 'keep going' }), context())
  assert.doesNotMatch(continued, /No continuing road|manager job|fast-manager|job_id/i)
  await hostJoinPublished(live.host)
  live.cleanup()
})

test('FORK_orchestrator_unknown_continuation_is_a_natural_consequence', async () => {
  const live = await liveOrchestrator()
  const spec = orchestratorSpec(factory, live.scope)
  const result = await spec.Execute(makeArgs({ name: 'Unknown Road', charge: 'nobody home' }), context())
  assert.match(result, /No continuing road is known by that name/)
  assert.doesNotMatch(result, /manager job|\berror\s*=/i)
  live.cleanup()
})

test('FORK_orchestrator_dirty_repo_rejects_the_road_without_internal_detail', async () => {
  const live = await liveOrchestrator()
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