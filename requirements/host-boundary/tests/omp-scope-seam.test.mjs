// Host physical canary: raw OMP adapter seam / host-shape
import test from 'node:test'
import assert from 'node:assert/strict'
import { activateOnOmp } from '../../../dist/Omp/Adapter.js'
import { AgentRole } from '../../../dist/Host/Ports/Vocabulary.js'
import { AgentRoleSpec } from '../../../dist/Host/Ports/Lifecycle.js'
import { ContextEdit } from '../../../dist/Host/Ports/TurnTransforms.js'
import { ofArray } from '../../../dist/fable_modules/fable-library-js.5.13.0/List.js'
import {
  resetScopeState,
  turnBudget,
  isScopeInitialized,
  endsWithSideRequest,
  isScopePrompt,
  clearSessionState,
} from '../../../dist/Context/Scope/Surface.js'

function createOmpTestEnvironment() {
  const eventHandlers = new Map()
  const registeredTools = []
  let turnContextMessagesMode = 'keep'
  let observedCatalog = null

  const mockPi = {
    logger: {
      debug: () => {},
      info: () => {},
      warn: () => {},
      error: () => {},
    },
    on: (eventName, handler) => {
      eventHandlers.set(eventName, handler)
    },
    registerTool: (toolDef) => {
      registeredTools.push(toolDef)
    },
  }

  const mockPlugin = {
    Activate: async (ports) => {
      observedCatalog = ports.Agents
      const hooks = {
        OnSignal: async () => {},
        Turn: {
          ContextMessages: async (_ctx, _messages) => {
            if (turnContextMessagesMode === 'keep') {
              return new ContextEdit(0, [])
            } else if (turnContextMessagesMode === 'lossy-replace') {
              return new ContextEdit(1, [
                {
                  Address: null,
                  Role: { tag: 0 },
                  Parts: [{ tag: 0, fields: ['lossy prose only'] }],
                },
              ])
            }
            return new ContextEdit(0, [])
          },
          SystemPrompt: async (_ctx, prompt) => prompt,
        },
        ToolGate: null,
      }
      return { tag: 0, fields: [hooks] }
    },
  }

  const agentManager = new AgentRoleSpec(new AgentRole(1), null, null, null)
  const agentEngineer = new AgentRoleSpec(new AgentRole(3), null, null, null)
  const agentsList = ofArray([agentManager, agentEngineer])

  return {
    mockPi,
    mockPlugin,
    agentsList,
    eventHandlers,
    registeredTools,
    getObservedCatalog: () => observedCatalog,
    setTurnMode: (mode) => {
      turnContextMessagesMode = mode
    },
  }
}

test('WHAT[context-compression-033] resetScope tool registration on OMP pi and exposure in role tool policies', async () => {
  const env = createOmpTestEnvironment()
  await activateOnOmp(env.mockPi, env.mockPlugin, env.agentsList)

  const sessionStartHandler = env.eventHandlers.get('session_start')
  assert.ok(sessionStartHandler, 'session_start handler must be registered')

  const sessionCtx = {
    cwd: process.cwd(),
    sessionManager: {
      getSessionId: () => 'ses_omp_test_035_1',
      getSessionDir: () => process.cwd(),
      getHeader: () => ({ parentSession: null }),
    },
  }
  await sessionStartHandler({}, sessionCtx)

  const resetScopeDef = env.registeredTools.find((t) => t.name === 'resetScope')
  assert.ok(resetScopeDef, 'resetScope tool must be registered on pi')
  assert.equal(resetScopeDef.loadMode, 'essential', 'resetScope loadMode must be essential')
  assert.equal(resetScopeDef.concurrency, 'exclusive', 'resetScope concurrency must be exclusive')
  assert.ok(resetScopeDef.parameters.properties.oldScopeVerboseVerbatimExperience, 'has oldScopeVerboseVerbatimExperience')
  assert.ok(resetScopeDef.parameters.properties.newScopeObjective, 'has newScopeObjective')
  assert.ok(resetScopeDef.parameters.properties.newScopeTurnBudget, 'has newScopeTurnBudget')
  assert.deepEqual(resetScopeDef.parameters.required, ['newScopeObjective', 'newScopeTurnBudget'])

  const catalog = env.getObservedCatalog()
  assert.ok(catalog, 'Agents catalog port was bound')
  const rolesOutcome = await catalog.Roles()
  assert.ok(rolesOutcome.fields && rolesOutcome.fields[0], 'Roles() returned Ok')
  const effectiveRoles = rolesOutcome.fields[0]
  for (const roleSpec of effectiveRoles) {
    assert.ok(roleSpec.ToolPolicy, 'role must have ToolPolicy defined')
    assert.equal(roleSpec.ToolPolicy.get('resetScope'), true, 'resetScope must be explicitly allowed for role')
  }
})

test('WHAT[context-compression-034] OMP before_agent_start injects scope protocol system prompt idempotently', async () => {
  const env = createOmpTestEnvironment()
  await activateOnOmp(env.mockPi, env.mockPlugin, env.agentsList)

  const sessionCtx = {
    cwd: process.cwd(),
    sessionManager: {
      getSessionId: () => 'ses_omp_test_035_2',
      getSessionDir: () => process.cwd(),
      getHeader: () => ({ parentSession: null }),
    },
  }
  await env.eventHandlers.get('session_start')({}, sessionCtx)

  const beforeAgentStartHandler = env.eventHandlers.get('before_agent_start')
  assert.ok(beforeAgentStartHandler, 'before_agent_start handler must be registered')

  const initialEvent = {
    systemPrompt: ['You are a helpful programming assistant.'],
  }
  const result1 = await beforeAgentStartHandler(initialEvent, sessionCtx)
  assert.ok(result1 && result1.systemPrompt, 'before_agent_start returned systemPrompt')
  assert.ok(
    result1.systemPrompt.some((p) => p.includes('## Scope & Context Management (`resetScope`)')),
    'systemPrompt must contain ## Scope & Context Management (`resetScope`) section',
  )

  // Idempotency: repeated call does not duplicate section
  const result2 = await beforeAgentStartHandler({ systemPrompt: result1.systemPrompt }, sessionCtx)
  assert.equal(result2.systemPrompt.length, result1.systemPrompt.length, 'injection must be idempotent')
})

test('WHAT[context-compression-033] OMP context handler preserves raw messages, tool pairing and metadata on Keep', async () => {
  const env = createOmpTestEnvironment()
  await activateOnOmp(env.mockPi, env.mockPlugin, env.agentsList)

  const sid = 'ses_omp_test_035_3'
  clearSessionState(sid)
  const sessionCtx = {
    cwd: process.cwd(),
    sessionManager: {
      getSessionId: () => sid,
      getSessionDir: () => process.cwd(),
      getHeader: () => ({ parentSession: null }),
    },
  }
  await env.eventHandlers.get('session_start')({}, sessionCtx)

  const rawOmpMessages = [
    {
      role: 'user',
      content: [{ type: 'text', text: 'Task 1: explore code' }],
      attribution: 'user',
      timestamp: 1000,
    },
    {
      role: 'assistant',
      content: [
        { type: 'text', text: 'reading...' },
        { type: 'toolCall', id: 'call_read_1', name: 'read', args: { path: 'file1.txt' } },
      ],
      timestamp: 1010,
    },
    {
      role: 'toolResult',
      toolCallId: 'call_read_1',
      content: 'data 1',
      timestamp: 1020,
    },
    {
      role: 'assistant',
      content: [
        {
          type: 'toolCall',
          id: 'call_reset_1',
          name: 'resetScope',
          args: { newScopeObjective: 'Task 2: modify', newScopeTurnBudget: 5 },
        },
      ],
      timestamp: 1030,
    },
    {
      role: 'toolResult',
      toolCallId: 'call_reset_1',
      content: 'Scope reset. Objective: Task 2: modify\0\uFEFF[Remaining turnBudget: 5]',
      timestamp: 1040,
    },
    {
      role: 'assistant',
      content: [
        { type: 'text', text: 'editing...' },
        { type: 'toolCall', id: 'call_edit_1', name: 'edit', args: { path: 'file2.txt' } },
      ],
      timestamp: 1050,
    },
    {
      role: 'toolResult',
      toolCallId: 'call_edit_1',
      content: 'edit success',
      timestamp: 1060,
    },
  ]

  env.setTurnMode('keep')
  const contextHandler = env.eventHandlers.get('context')
  const result = await contextHandler({ messages: rawOmpMessages }, sessionCtx)
  assert.ok(result && result.messages, 'context handler returned modified messages')
  const kept = result.messages

  // Messages before reset (call_read_1 and its result) must be purged;
  // reset call/result, post-barrier edit call/result, and budget 0 weak steer survive
  assert.equal(kept.length, 6, 'pre-barrier tools must be purged, active scope and reset call kept')

  assert.equal(kept[0].role, 'user')
  assert.equal(kept[0].attribution, 'user')
  assert.equal(kept[0].timestamp, 1000)

  assert.equal(kept[1].role, 'assistant')
  assert.equal(kept[1].content[0].id, 'call_reset_1')

  assert.equal(kept[2].role, 'toolResult')
  assert.equal(kept[2].toolCallId, 'call_reset_1')
  assert.equal(kept[2].timestamp, 1040)

  assert.equal(kept[3].role, 'assistant')
  assert.equal(kept[3].content[1].id, 'call_edit_1')

  assert.equal(kept[4].role, 'toolResult')
  assert.equal(kept[4].toolCallId, 'call_edit_1')
  assert.equal(kept[4].timestamp, 1060)
  assert.equal(kept[4].content, 'edit success')

  clearSessionState(sid)
})

test('WHAT[context-compression-033] OMP context handler safely refuses lossy Replace to prevent tool-result corruption', async () => {
  const env = createOmpTestEnvironment()
  await activateOnOmp(env.mockPi, env.mockPlugin, env.agentsList)

  const sid = 'ses_omp_test_035_4'
  clearSessionState(sid)
  const sessionCtx = {
    cwd: process.cwd(),
    sessionManager: {
      getSessionId: () => sid,
      getSessionDir: () => process.cwd(),
      getHeader: () => ({ parentSession: null }),
    },
  }
  await env.eventHandlers.get('session_start')({}, sessionCtx)

  const rawOmpMessages = [
    { role: 'user', content: [{ type: 'text', text: 'goal' }], timestamp: 100 },
    {
      role: 'assistant',
      content: [{ type: 'toolCall', id: 'call_res', name: 'resetScope', args: { newScopeObjective: 'g', newScopeTurnBudget: 3 } }],
    },
    { role: 'toolResult', toolCallId: 'call_res', content: 'Scope reset. Objective: g\0\uFEFF[Remaining turnBudget: 3]' },
    {
      role: 'assistant',
      content: [{ type: 'toolCall', id: 'call_work', name: 'read', args: {} }],
    },
    { role: 'toolResult', toolCallId: 'call_work', content: 'output' },
  ]

  env.setTurnMode('lossy-replace')
  const contextHandler = env.eventHandlers.get('context')
  const result = await contextHandler({ messages: rawOmpMessages }, sessionCtx)
  assert.ok(result && result.messages, 'context handler returned fallback messages')

  // The lossy edit (which dropped toolResult messages) must be refused,
  // and the raw post-scope messages must be returned intact.
  const guarded = result.messages
  assert.ok(guarded.some((m) => m.role === 'toolResult' && m.toolCallId === 'call_res'), 'resetScope result preserved')
  assert.ok(guarded.some((m) => m.role === 'toolResult' && m.toolCallId === 'call_work'), 'work toolResult preserved')

  clearSessionState(sid)
})

test('WHAT[context-compression-034] OMP side-request snapshots do not decrement/replace turnBudget, move prompt boundary, or receive weak steer', async () => {
  const env = createOmpTestEnvironment()
  await activateOnOmp(env.mockPi, env.mockPlugin, env.agentsList)

  const sid = 'ses_omp_test_035_5'
  clearSessionState(sid)
  const sessionCtx = {
    cwd: process.cwd(),
    sessionManager: {
      getSessionId: () => sid,
      getSessionDir: () => process.cwd(),
      getHeader: () => ({ parentSession: null }),
    },
  }
  await env.eventHandlers.get('session_start')({}, sessionCtx)

  resetScopeState(sid, 5)
  assert.equal(turnBudget(sid), 5, 'turnBudget initialized to 5')
  assert.equal(isScopeInitialized(sid), true, 'scope is initialized')

  const sideSnapshot = [
    {
      role: 'user',
      content: [{ type: 'text', text: 'primary task' }],
      attribution: 'user',
      timestamp: 1000,
    },
    {
      role: 'assistant',
      content: [
        { type: 'toolCall', id: 'call_res', name: 'resetScope', args: { newScopeObjective: 'phase', newScopeTurnBudget: 5 } },
      ],
    },
    { role: 'toolResult', toolCallId: 'call_res', content: 'Scope reset. Objective: phase\0\uFEFF[Remaining turnBudget: 5]' },
    {
      role: 'user',
      content: [{ type: 'text', text: 'summarize conversation title' }],
      attribution: 'agent',
      steering: false,
      timestamp: 2000,
    },
  ]

  assert.equal(endsWithSideRequest(sideSnapshot), true, 'endsWithSideRequest recognizes agent side prompt')
  assert.equal(isScopePrompt(sideSnapshot[sideSnapshot.length - 1]), false, 'isScopePrompt returns false for agent side prompt')

  env.setTurnMode('keep')
  const contextHandler = env.eventHandlers.get('context')
  const result = await contextHandler({ messages: sideSnapshot }, sessionCtx)

  // Side request must NOT decrement or reset turnBudget (must stay 5)
  assert.equal(turnBudget(sid), 5, 'side request must NOT decrement or reset turnBudget')
  assert.equal(isScopeInitialized(sid), true, 'side request must NOT un-initialize scope')

  const sideMessages = result ? result.messages : sideSnapshot
  const lastMsg = sideMessages[sideMessages.length - 1]
  assert.equal(lastMsg.content[0].text, 'summarize conversation title', 'side prompt must NOT have user reminder appended')
  assert.equal(sideMessages.some((m) => m.steering === true), false, 'side request must NOT receive weak steer')

  clearSessionState(sid)
})
