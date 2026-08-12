// tests/unit/tools/list-tool.test.mjs — horizon(): natural-language roster, no id/status DTO.

import assert from 'node:assert/strict'
import test from 'node:test'

import { completionKind, handleId, handleProjection, roles, sessionId, toList } from '../support/domain.mjs'

const { HostToolContext } = await import('../../../dist/Infrastructure/OpenCode/Codec/ToolHostCodec.js')
const { spec } = await import('../../../dist/Infrastructure/OpenCode/Tools/HorizonTool.js')
const { ToolRuntimeScope } = await import('../../../dist/Infrastructure/OpenCode/Tools/ToolRuntimeScope.js')
const { HostForkRuntime } = await import('../../../dist/Session/HostForkRuntime.js')
const { SessionAgentProjection } = await import('../../../dist/Journal/AgentProjection.js')
const { CompletionCell$1_$ctor: completionCell } = await import('../../../dist/Session/ChildRun.js')
const { add: mapAdd, ofList: mapOfList } = await import('../../../dist/fable_modules/fable-library-js.5.13.0/Map.js')
const { compare } = await import('../../../dist/fable_modules/fable-library-js.5.13.0/Util.js')
const sessionMap = (entries) => mapOfList(entries, { Compare: compare })

const context = (session = 'ses_list') => new HostToolContext(session, undefined, undefined, undefined, undefined, () => () => {})

const PARENT = sessionId('ses_list')

const FORBIDDEN = /\b(agent_id|session_id|pty_id|child_session_id|status|kind|ordinal|has_pending_completion|current_run_id|fallback_peer|tier|role)\s*=/

const fakeJournal = (handles) => ({
  gate: { Enter: () => ({ Exit: () => {} }) },
  projection: {
    AgentProjections: {
      Sessions: sessionMap([[PARENT, new SessionAgentProjection(undefined, undefined, undefined, undefined, handles, undefined, undefined, undefined, undefined, undefined, undefined, undefined)]]),
    },
  },
})

const linked = (handle, child, target, role, byname = target) => {
  const applied = handleProjection.linkNamed(handle, child, target, byname, roles.of(role), handleProjection.empty)
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

const run = async (runtimeScope, session = 'ses_list') => spec(runtimeScope).Execute({}, context(session))

test('HORIZON_no_journal_reports_projection_unavailable', async () => {
  const scope = scopeFor(undefined, liveRuntime())
  const text = await run(scope)
  assert.match(text, /horizon is unavailable/i)
  assert.ok(!/\berror\s*=/.test(text))
})

test('HORIZON_runtime_error_is_surfaced', async () => {
  const scope = scopeFor(fakeJournal(handleProjection.empty), liveRuntime())
  scope.disposed = true
  const text = await run(scope)
  assert.match(text, /horizon cannot be seen/i)
  assert.ok(!/\berror\s*=/.test(text))
})

test('HORIZON_lists_active_agent_by_byname_and_open_terminals_in_natural_language', async () => {
  const handles = linked(handleId.agent('ag-1'), sessionId('child-1'), 'fast-coder', 'Coder', 'Ada')
  const scope = scopeFor(
    fakeJournal(handles),
    liveRuntime({
      agents: [runRecord('ag-1', { RunId: 'run-77' })],
      ptys: [ptyRecord('pty-2', 'npm test'), ptyRecord('pty-1', 'tail -f')],
    }),
  )
  const text = await run(scope)

  assert.match(text, /# Ada is still away\./)
  assert.doesNotMatch(text, /fast-coder/)
  assert.match(text, /# tail -f remains open\./)
  assert.match(text, /# npm test remains open\./)
  assert.ok(!FORBIDDEN.test(text))
})

test('HORIZON_completed_awaiting_join_reports_returned', async () => {
  const handles = completed(linked(handleId.agent('ag-1'), sessionId('child-1'), 'fast-coder', 'Coder'), handleId.agent('ag-1'))
  const scope = scopeFor(fakeJournal(handles), liveRuntime())
  const text = await run(scope)
  assert.match(text, /# fast-coder has returned\./)
  assert.ok(!FORBIDDEN.test(text))
})

test('HORIZON_active_agent_without_runtime_defaults_to_still_away', async () => {
  const handles = linked(handleId.agent('ag-2'), sessionId('child-2'), 'fast-coder', 'Coder')
  const scope = scopeFor(fakeJournal(handles), liveRuntime())
  const text = await run(scope)
  assert.match(text, /# fast-coder is still away\./)
  assert.ok(!FORBIDDEN.test(text))
})

test('HORIZON_unmanaged_target_agent_renders_bare_identity', async () => {
  const handles = linked(handleId.agent('ag-3'), sessionId('child-3'), 'some-raw-agent', 'DevOps')
  const scope = scopeFor(fakeJournal(handles), liveRuntime())
  const text = await run(scope)
  assert.match(text, /# some-raw-agent is still away\./)
  assert.ok(!FORBIDDEN.test(text))
})

test('HORIZON_empty_journal_lists_only_ptys', async () => {
  const scope = scopeFor(fakeJournal(handleProjection.empty), liveRuntime({ ptys: [ptyRecord('pty-9', 'watch logs')] }))
  const text = await run(scope)
  assert.match(text, /# watch logs remains open\./)
  assert.ok(!text.includes('fast-coder'))
})

test('HORIZON_empty_roster_has_quiet_instruction', async () => {
  const scope = scopeFor(fakeJournal(handleProjection.empty), liveRuntime())
  const text = await run(scope)
  assert.match(text, /Nothing beyond your immediate sight/)
})
