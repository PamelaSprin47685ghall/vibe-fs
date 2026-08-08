// tests/unit/tools/list-tool.test.mjs — ListTool: durable handle view joined with physical PTY records.
//
// Only the journal persistence plumbing is faked: `handleProjection` reads
// `snapshot(journal).AgentProjections`, so the fake journal serves a projection
// whose Handles cell is built with the REAL HandleProjection algebra. The
// tool's own execute/agentEntry/ptyEntry code is production.

import assert from 'node:assert/strict'
import test from 'node:test'
import { parse as parseToml } from 'smol-toml'

import { completionKind, handleId, handleOwnership, handleProjection, roles, sessionId, toList } from '../support/domain.mjs'

const { HostToolContext } = await import('../../../dist/Infrastructure/OpenCode/Codec/ToolHostCodec.js')
const { spec } = await import('../../../dist/Infrastructure/OpenCode/Tools/ListTool.js')
const { ToolRuntimeScope } = await import('../../../dist/Infrastructure/OpenCode/Tools/ToolRuntimeScope.js')
const { HostForkRuntime } = await import('../../../dist/Session/HostForkRuntime.js')
const { SessionAgentProjection } = await import('../../../dist/Journal/AgentProjection.js')
const { CompletionCell$1_$ctor: completionCell } = await import('../../../dist/Session/ChildRun.js')
const { add: mapAdd, ofList: mapOfList } = await import('../../../dist/fable_modules/fable-library-js.5.13.0/Map.js')
const { compare } = await import('../../../dist/fable_modules/fable-library-js.5.13.0/Util.js')
const sessionMap = (entries) => mapOfList(entries, { Compare: compare })

const context = (session = 'ses_list') => new HostToolContext(session, undefined, undefined, undefined, undefined, () => () => {})

const PARENT = sessionId('ses_list')

const fakeJournal = (handles) => ({
  gate: { Enter: () => ({ Exit: () => {} }) },
  projection: {
    AgentProjections: {
      Sessions: sessionMap([[PARENT, new SessionAgentProjection(undefined, undefined, undefined, undefined, handles, undefined, undefined, undefined, undefined, undefined, undefined, undefined)]]),
    },
  },
})

const linked = (handle, child, target, role) => {
  const applied = handleProjection.link(handle, child, target, roles.of(role), handleProjection.empty)
  assert.equal(applied.ok, true, applied.ok ? '' : applied.error)
  return applied.value
}

const completed = (projection, handle) => {
  const applied = handleProjection.complete(handle, completionKind.of('Terminal'), projection)
  assert.equal(applied.ok, true, applied.ok ? '' : applied.error)
  return applied.value
}

const ptyRecord = (ptyId, command = 'ls -la') => ({
  PtyId: ptyId,
  AgentId: undefined,
  Command: command,
  StartedAt: new Date('2026-08-08T10:00:00.000Z'),
})

/** Fake ChildRun: ForkRuntime.List folds it through the REAL toRecord/status. */
const runRecord = (agentId, overrides = {}) => ({
  AgentId: agentId,
  RunId: undefined,
  AgentName: 'fast-coder',
  Role: undefined,
  Prompt: 'do work',
  ChildSessionId: undefined,
  Completion: completionCell(),
  Cancellation: { IsCancellationRequested: () => false },
  CreatedAt: new Date('2026-08-08T09:00:00.000Z'),
  ...overrides,
})

const fakeSessions = () => ({
  CreateChildSession: async () => ({ tag: 0, fields: [sessionId('child-0')] }),
  AbortSession: async () => ({ tag: 0, fields: [] }),
  SendPrompt: async () => ({ tag: 0, fields: [] }),
  SendPromptAsync: async () => ({ tag: 0, fields: [] }),
  SubscribeTerminal: () => ({ Dispose: () => {} }),
  ListChildren: async () => ({ tag: 0, fields: [toList([])] }),
})

const scopeFor = (journal, runtime) => {
  const scope = new ToolRuntimeScope(
    fakeSessions(),
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
  scope.runtimes.set('ses_list', runtime)
  return scope
}

/** Real HostForkRuntime seeded with physical agents/ptys (List folds them). */
const liveRuntime = ({ agents = [], ptys = [] } = {}) => {
  const runtime = new HostForkRuntime(
    sessionId('ses_list'),
    fakeSessions(),
    undefined,
    undefined,
    undefined,
    undefined,
    undefined,
    undefined,
    undefined,
    undefined,
    undefined,
    undefined,
    undefined,
  )
  let agentMap = mapOfList([], { Compare: compare })
  for (const run of agents) agentMap = mapAdd(run.AgentId, run, agentMap)
  runtime.runtime.agents = agentMap
  let ptyMap = mapOfList([], { Compare: compare })
  for (const pty of ptys) ptyMap = mapAdd(pty.PtyId, pty, ptyMap)
  runtime.runtime.ptys = ptyMap
  return runtime
}

const run = async (runtimeScope, session = 'ses_list') =>
  parseToml(await spec(runtimeScope).Execute({}, context(session)))

test('LIST_no_journal_reports_projection_unavailable', async () => {
  const scope = scopeFor(undefined, liveRuntime())
  const result = await run(scope)
  assert.equal(result.error, 'HandleProjection unavailable: durable journal is not configured')
})

test('LIST_runtime_error_is_surfaced', async () => {
  const scope = scopeFor(fakeJournal(handleProjection.empty), liveRuntime())
  scope.disposed = true
  const result = await run(scope)
  assert.equal(result.error, 'Tool runtime scope is disposed')
})

test('LIST_lists_active_agent_with_runtime_join', async () => {
  const handles = linked(handleId.agent('ag-1'), sessionId('child-1'), 'fast-coder', 'Coder')
  const scope = scopeFor(
    fakeJournal(handles),
    liveRuntime({
      agents: [runRecord('ag-1', { RunId: 'run-77' })],
      ptys: [ptyRecord('pty-2', 'npm test'), ptyRecord('pty-1', 'tail -f')],
    }),
  )
  const result = await run(scope)
  const items = result.item ?? []
  const agent = items.find((i) => i.kind === 'agent')
  const ptys = items.filter((i) => i.kind === 'pty')

  assert.equal(agent.agent_id, 'ag-1')
  assert.equal(agent.child_session_id, 'child-1')
  assert.equal(agent.status, 'busy')
  assert.equal(agent.has_pending_completion, false)
  assert.equal(agent.current_run_id, 'run-77')
  assert.equal(agent.last_completion_status, undefined)
  assert.equal(agent.agent, 'fast-coder')
  assert.equal(agent.role, 'coder')
  assert.equal(agent.tier, 'fast')
  assert.equal(agent.fallback_peer, 'deep-coder')

  assert.equal(ptys.length, 2, 'both ptys listed')
  assert.equal(ptys[0].pty_id, 'pty-1', 'ptys sorted by id')
  assert.equal(ptys[1].pty_id, 'pty-2')
  assert.equal(ptys[0].command, 'tail -f')
  const started = new Date('2026-08-08T10:00:00.000Z')
  const offset = -started.getTimezoneOffset()
  const pad = (n) => String(n).padStart(2, '0')
  const expectedO = `${started.getFullYear()}-${pad(started.getMonth() + 1)}-${pad(started.getDate())}T${pad(started.getHours())}:${pad(started.getMinutes())}:${pad(started.getSeconds())}.${String(started.getMilliseconds()).padStart(3, '0')}${offset >= 0 ? '+' : '-'}${pad(Math.floor(Math.abs(offset) / 60))}:${pad(Math.abs(offset) % 60)}`
  assert.equal(ptys[0].started_at, expectedO)
})

test('LIST_completed_awaiting_join_handle_reports_status_and_pending', async () => {
  const handles = completed(linked(handleId.agent('ag-1'), sessionId('child-1'), 'fast-coder', 'Coder'), handleId.agent('ag-1'))
  const scope = scopeFor(fakeJournal(handles), liveRuntime())
  const result = await run(scope)
  const agent = result.item.find((i) => i.kind === 'agent')

  assert.equal(agent.status, 'completed-awaiting-join')
  assert.equal(agent.has_pending_completion, true)
})

test('LIST_active_agent_without_runtime_defaults_to_running', async () => {
  const handles = linked(handleId.agent('ag-2'), sessionId('child-2'), 'fast-coder', 'Coder')
  const scope = scopeFor(fakeJournal(handles), liveRuntime())
  const result = await run(scope)
  const agent = result.item.find((i) => i.kind === 'agent')
  assert.equal(agent.agent_id, 'ag-2')
  assert.equal(agent.status, 'running')
  assert.equal(agent.has_pending_completion, false)
})

test('LIST_unmanaged_target_agent_renders_bare_identity', async () => {
  const handles = linked(handleId.agent('ag-3'), sessionId('child-3'), 'some-raw-agent', 'DevOps')
  const scope = scopeFor(fakeJournal(handles), liveRuntime())
  const result = await run(scope)
  const agent = result.item.find((i) => i.kind === 'agent')
  assert.equal(agent.agent, 'some-raw-agent')
  assert.equal(agent.role, 'devops')
  assert.equal(agent.tier, undefined)
})

test('LIST_empty_journal_lists_only_ptys', async () => {
  const scope = scopeFor(fakeJournal(handleProjection.empty), liveRuntime({ ptys: [ptyRecord('pty-9')] }))
  const result = await run(scope)
  assert.equal(result.item.length, 1)
  assert.equal(result.item[0].kind, 'pty')
})
