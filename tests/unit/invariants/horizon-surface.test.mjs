// EXEC-005 / EXEC-030 — horizon output must not carry id/status/state-machine DTO.

import assert from 'node:assert/strict'
import test from 'node:test'

import {
  completionKind,
  handleId,
  handleProjection,
  mapOfEntries,
  roles,
  sessionId,
  structuralComparer,
  toList,
} from '../support/domain.mjs'

const { HostToolContext } = await import('../../../dist/Infrastructure/OpenCode/Codec/ToolHostCodec.js')
const { spec } = await import('../../../dist/Infrastructure/OpenCode/Tools/ListTool.js')
const { ToolRuntimeScope } = await import('../../../dist/Infrastructure/OpenCode/Tools/ToolRuntimeScope.js')
const { HostForkRuntime } = await import('../../../dist/Session/HostForkRuntime.js')
const { SessionAgentProjection } = await import('../../../dist/Journal/AgentProjection.js')
const { CompletionCell$1_$ctor: completionCell } = await import('../../../dist/Session/ChildRun.js')

const FORBIDDEN = /\b(agent_id|session_id|pty_id|child_session_id|status|kind|ordinal|has_pending_completion|current_run_id|fallback_peer|tier|role)\s*=|completed-awaiting-join|running|busy/

const sessionMap = (entries) => mapOfEntries(entries, structuralComparer)

const context = () => new HostToolContext('ses_horizon', undefined, undefined, undefined, undefined, () => () => {})

const fakeJournal = (handles) => ({
  gate: { Enter: () => ({ Exit: () => {} }) },
  projection: {
    AgentProjections: {
      Sessions: sessionMap([[sessionId('ses_horizon'), new SessionAgentProjection(undefined, undefined, undefined, undefined, handles, undefined, undefined, undefined, undefined, undefined, undefined, undefined)]]),
    },
  },
})

const scopeFor = (journal, runtime) => {
  const scope = new ToolRuntimeScope(
    {
      CreateChildSession: async () => ({ tag: 0, fields: [sessionId('child-0')] }),
      AbortSession: async () => ({ tag: 0, fields: [] }),
      SendPrompt: async () => ({ tag: 0, fields: [] }),
      SendPromptAsync: async () => ({ tag: 0, fields: [] }),
      SubscribeTerminal: () => ({ Dispose: () => {} }),
      ListChildren: async () => ({ tag: 0, fields: [toList([])] }),
    },
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
  scope.runtimes.set('ses_horizon', runtime)
  return scope
}

const runtimeWithAgent = () => {
  const runtime = new HostForkRuntime(
    sessionId('ses_horizon'),
    {
      CreateChildSession: async () => ({ tag: 0, fields: [sessionId('child-0')] }),
      AbortSession: async () => ({ tag: 0, fields: [] }),
      SendPrompt: async () => ({ tag: 0, fields: [] }),
      SendPromptAsync: async () => ({ tag: 0, fields: [] }),
      SubscribeTerminal: () => ({ Dispose: () => {} }),
      ListChildren: async () => ({ tag: 0, fields: [toList([])] }),
    },
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
  runtime.runtime.agents = mapOfEntries([
    [
      'ag-1',
      {
        AgentId: 'ag-1',
        AgentName: 'fast-coder',
        Prompt: 'work',
        Completion: completionCell(),
        Cancellation: { IsCancellationRequested: () => false },
        CreatedAt: new Date(),
      },
    ],
  ])
  return runtime
}

test('HORIZON_SURFACE_has_no_legacy_roster_dto', async () => {
  const handles = handleProjection.link(
    handleId.agent('ag-1'),
    sessionId('child-1'),
    'fast-coder',
    roles.of('Coder'),
    handleProjection.empty,
  ).value

  const text = await spec(scopeFor(fakeJournal(handles), runtimeWithAgent())).Execute({}, context())
  assert.match(text, /# fast-coder is still away\./)
  assert.ok(!FORBIDDEN.test(text), text)
})
