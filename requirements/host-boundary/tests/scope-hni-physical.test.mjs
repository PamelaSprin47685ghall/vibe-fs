import test from 'node:test'
import assert from 'node:assert/strict'
import { createWith } from '../../../dist/OpenCode/Host/ProviderSystemTransform.js'
import { normalTransform, DeliveryAdjudication } from '../../../dist/OpenCode/Plugin/PluginTransforms.js'
import { resetScopeState, clearSessionState, turnBudget, isScopeInitialized } from '../../../dist/Context/Scope/Surface.js'
import { ofArray } from '../../../dist/fable_modules/fable-library-js.5.13.0/List.js'
import { SessionMessage, SessionToolPart, SnapshotToolPartState } from '../../../dist/Foundation/HostSessionSnapshot.js'
import { MessagePart } from '../../../dist/Foundation/HostMessage.js'
import { ToolCallIdModule_create, HostToolPartIdModule_create } from '../../../dist/Foundation/Identity.js'
import { filterSnapshot } from '../../../dist/Context/Scope/SnapshotScope.js'
import { withExecutablePlugin } from '../../verification-system/tests/support/plugin-fixture.mjs'

const SEP = '\0\uFEFF'
const user = (id, text, created) => ({ info: { id, role: 'user', time: { created } }, parts: [{ type: 'text', text }] })
const assistant = (id, ...parts) => ({ info: { id, role: 'assistant', time: { created: 2 } }, parts })
const tool = (callID, name, output, status = 'completed') => ({ type: 'tool', callID, tool: name, state: { status, input: {}, ...(status === 'error' ? { error: output } : { output }) } })
const toolText = (msg) => msg.parts[0].state.output ?? msg.parts[0].state.error

function safeCaps() {
  const yes = async () => {}
  return {
    BeginPhysicalProviderAttempt: yes,
    BindSessionStartedAt: async () => null,
    ApplyRelayProjection: async () => null,
    ApplyTenureIsolation: yes,
    CaptureXTraceMessages: async () => ({ Current: null, RawMessages: [], Receipt: null }),
    RefreshCompanionXTrace: () => {},
    ApplyCompanion: yes,
    ApplyXWire: yes,
    FreezeProviderAttemptPlan: yes,
    InjectPairGuideline: yes,
    SettleAndReplaceDeferredInspections: yes,
    SanitizeMessages: () => {},
    AdjudicateDelivery: async () => new DeliveryAdjudication(0, []),
    StopAfterDelivery: yes,
  }
}

async function outbound(sessionId, messages) {
  const out = { messages }
  await normalTransform(safeCaps(), sessionId, {}, out)
  return out.messages
}

test('WHAT[context-compression-039] OpenCode sends a scoped request copy; completed results receive initialization reminder without changing the Host history', async () => {
  const sid = 'ses_scope_039_initial'
  clearSessionState(sid)
  const raw = [user('u1', 'read this file', 1), assistant('a1', tool('c1', 'read', 'file body'))]
  const original = [...raw]
  const copy = await outbound(sid, raw)
  assert.equal(copy, raw, 'OpenCode requires the provider request array to be edited in place')
  assert.match(copy[0].parts[0].text, /Then call resetScope immediately/)
  assert.equal(copy[0].content, undefined)
  assert.equal(copy[1].parts[0].state.output, 'file body' + SEP + copy[0].parts[0].text.split(SEP).at(-1))
  assert.equal(original[0].parts[0].text, 'read this file')
  assert.equal(toolText(original[1]), 'file body')
  clearSessionState(sid)
})

test('WHAT[context-compression-039] OpenCode applies a reset, preserves only its call and later work, and reads the new budget', async () => {
  const sid = 'ses_scope_039_reset'
  clearSessionState(sid)
  const operator = user('u1', 'scope initial', 100)
  await outbound(sid, [operator])
  resetScopeState(sid, 4)
  const raw = [operator, assistant('a1', tool('old', 'read', 'old')), assistant('a2', tool('reset', 'resetScope', 'Scope reset')), assistant('a3', tool('next', 'read', 'new'))]
  const original = [...raw]
  const copy = await outbound(sid, raw)
  assert.equal(copy.length, 3)
  assert.equal(copy[1].parts[0].tool, 'resetScope')
  assert.equal(copy[1].parts[0].state.output, 'Scope reset' + SEP + '[Remaining turnBudget: 3]')
  assert.equal(copy[2].parts[0].state.output, 'new' + SEP + '[Remaining turnBudget: 3]')
  assert.equal(original[3].parts[0].state.output, 'new', 'the original result object is not edited')
  assert.equal(turnBudget(sid), 3)
  clearSessionState(sid)
})

const call = (id, tool) => new MessagePart(2, [id, tool, '{}'])
const returned = (id, text) => new MessagePart(3, [id, text])
const text = (body) => new MessagePart(0, [body])
const msg = (id, role, parts, toolParts = []) => new SessionMessage(
  id, role, undefined, undefined, undefined, undefined, undefined, undefined,
  true, false, undefined, parts, parts.map(() => undefined), toolParts,
)
const physicalResult = (id, output) => new SessionToolPart(
  HostToolPartIdModule_create('part-' + id), ToolCallIdModule_create(id),
  id === 'reset' ? 'resetScope' : 'read', '{}', new SnapshotToolPartState(1, [output]),
)

test('WHAT[context-compression-040] SnapshotScope keeps the result co-located with an earlier reset call', () => {
  const raw = [
    msg('u1', 'user', [text('Task')]),
    msg('a1', 'assistant', [text('work'), call('old','read'), returned('old','old result'), call('r1','resetScope'), returned('r1','first reset')]),
    msg('a2', 'assistant', [call('new','read'), call('r2','resetScope'), returned('r2','second reset')]),
  ]
  const retained = filterSnapshot(ofArray(raw))
  assert.deepEqual(retained.map(m => m.id), ['u1', 'a1', 'a2'])
  assert.deepEqual(retained[1].parts.map(p => p.id), ['r1', 'r1'], 'both halves of the older reset must survive')
  assert.deepEqual(retained[2].parts.map(p => p.id), ['r2', 'r2'])
  assert.equal(raw[1].Parts.length, 5, 'the session snapshot remains unchanged')
})

test('WHAT[context-compression-040] SnapshotScope preserves completed reset tool states and removes unrelated old work', () => {
  const raw = [
    msg('u1', 'user', [text('Task')]),
    msg('a1', 'assistant', [call('old', 'read'), call('reset', 'resetScope')], [physicalResult('old', 'read output'), physicalResult('reset', 'Scope reset.')]),
    msg('a2', 'assistant', [call('r2', 'resetScope')]),
  ]
  const kept = filterSnapshot(ofArray(raw))
  assert.equal(kept.length, 3)
  assert.deepEqual(kept[1].parts.map(p => p.id), ['reset','reset'], 'the prior reset result is paired with its call')
  assert.deepEqual(kept[2].parts.map(p => p.id), ['r2'])
})

test('WHAT[context-compression-041] OpenCode session.deleted releases only its own scope actor', async () => {
  await withExecutablePlugin(async (hooks) => {
    const closed = 'ses_deleted_scope_041'
    const active = 'ses_active_scope_041'
    clearSessionState(closed)
    clearSessionState(active)
    resetScopeState(closed, 5)
    resetScopeState(active, 7)
    assert.equal(turnBudget(closed), 5)
    await hooks.event({event: {type: 'session.deleted', properties: {sessionID: closed}}})
    assert.equal(turnBudget(closed), 0)
    assert.equal(isScopeInitialized(closed), false)
    assert.equal(turnBudget(active), 7, 'other session actor must not be touched')
    clearSessionState(active)
  })
})


test('WHAT[prefix-stability-016] OpenCode provider transform preserves old completed output bytes after turnBudget decreases', async () => {
  const sid = 'ses_scope_016_cache'
  clearSessionState(sid)
  const operator = user('user-016', 'do the work', 100)
  await outbound(sid, [operator])
  resetScopeState(sid, 7)
  const reset = assistant('asst-reset-016', tool('reset-016', 'resetScope', 'Scope reset'))
  const rawResetText = reset.parts[0].state.output
  const first = await outbound(sid, [operator, reset])
  const firstRendered = JSON.stringify(first[1])
  assert.match(first[1].parts[0].state.output, /Remaining turnBudget: 6/)

  const read = assistant('asst-read-016', tool('read-016', 'read', 'new data'))
  const second = await outbound(sid, [operator, reset, read])
  assert.equal(JSON.stringify(second[1]), firstRendered,
    'the historical reset result must remain a verbatim prefix, including its old budget suffix')
  assert.match(second[2].parts[0].state.output, /Remaining turnBudget: 5/)
  assert.equal(reset.parts[0].state.output, rawResetText,
    'raw Host history must remain untouched even though the outgoing array changed')
  clearSessionState(sid)
})


test('WHAT[delegation-negotiation-001] the Host records only the question while the provider sees an immutable parent prefix', async () => {
  const {
    negotiationRememberSystem, negotiationRememberMessages,
    negotiationRememberModel, negotiationModelFor,
    negotiationStartTemporary, negotiationProjectSystem,
    negotiationProjectMessages, negotiationIsTemporary,
    negotiationFinishTemporary, negotiationForgetSession,
  } = await import('../../../dist/OpenCode/Plugin/PluginTransforms.js')
  const parent = 'ses-neg-parent-001', temporary = 'ses-neg-grandchild-001'
  const prefix = [
    { info: { id: 'u1', sessionID: parent, role: 'user' }, parts: [{ type: 'text', text: 'Existing instruction' }] },
    { info: { id: 'a1', sessionID: parent, role: 'assistant' }, parts: [{ type: 'text', text: 'Already answered' }] },
  ]
  const originalBytes = JSON.stringify(prefix)
  const system = ['Common law', 'Role law', 'Provider configuration']
  negotiationRememberSystem({ sessionID: parent }, { system })
  negotiationRememberMessages(parent, { messages: prefix })
  const withoutModel = negotiationStartTemporary(temporary, parent)
  assert.equal(withoutModel.tag, 1, 'do not improvise a different model when the delegator model is unknown')
  negotiationRememberModel({ sessionID: parent, model: { providerID: 'provider', id: 'deep-model' }, message: {model:{variant:'high'}} })
  assert.deepEqual(negotiationModelFor(parent), { providerID: 'provider', modelID: 'deep-model', variant: 'high' })
  const admitted = negotiationStartTemporary(temporary, parent)
  assert.equal(admitted.tag, 0, 'the parent must have a complete provider wire')
  assert.equal(negotiationIsTemporary(temporary), true)

  const hostRecord = { info: { id: 'tmp-question', sessionID: temporary, role: 'user' }, parts: [{ type: 'text', text: 'Which parser?' }] }
  const outgoing = { messages: [hostRecord] }
  const hostArray = outgoing.messages
  const tmpSystem = { system: ['Different grandchild system'] }
  const liveSystemArray = tmpSystem.system
  assert.equal(negotiationProjectSystem({ sessionID: temporary }, tmpSystem), true)
  assert.equal(tmpSystem.system, liveSystemArray, 'OpenCode may retain the original system array identity')
  assert.deepEqual(tmpSystem.system, system, 'system bytes are exactly the last parent provider system')
  assert.equal(negotiationProjectMessages(temporary, outgoing), true)
  assert.equal(outgoing.messages, hostArray, 'Host retains the same array object')
  assert.deepEqual(outgoing.messages.slice(0, prefix.length), prefix, 'provider sees byte-identical parent prefix')
  assert.equal(outgoing.messages.at(-1).parts[0].text, 'Which parser?')
  assert.equal(JSON.stringify(prefix), originalBytes, 'source provider cache snapshot never changes')
  assert.equal(JSON.stringify(hostRecord), JSON.stringify(hostArray.at(-1)), 'the temporary Host user record is untouched')

  negotiationFinishTemporary(temporary)
  assert.equal(negotiationIsTemporary(temporary), false)
  negotiationForgetSession(parent)
})

test('WHAT[delegation-negotiation-001] temporary session tool calls are forbidden before execution', async () => {
  const {
    negotiationRememberSystem, negotiationRememberMessages, negotiationRememberModel,
    negotiationStartTemporary, negotiationFinishTemporary, negotiationForgetSession,
    negotiationToolViolationCount,
  } = await import('../../../dist/OpenCode/Plugin/PluginTransforms.js')
  await withExecutablePlugin(async (hooks) => {
    const parent = 'ses-neg-tool-parent', temp = 'ses-neg-tool-temp'
    negotiationRememberSystem({ sessionID: parent }, { system: ['system'] })
    negotiationRememberMessages(parent, { messages: [user('parent-u', 'task', 42)] })
    negotiationRememberModel({ sessionID: parent, model: { providerID: 'provider', modelID: 'deep-model' } })
    assert.equal(negotiationStartTemporary(temp, parent).tag, 0)
    assert.equal(negotiationToolViolationCount(temp), 0)
    try {
      await assert.rejects(
        Promise.resolve().then(() => hooks['tool.execute.before'](
          { sessionID: temp, tool: 'read', callID: 'forbidden-1', args: {} },
          {},
        )),
        /Ephemeral negotiation is read-only/,
      )
      assert.equal(negotiationToolViolationCount(temp), 1, 'a vetoed tool call is recorded for retry')
    } finally {
      negotiationFinishTemporary(temp)
      negotiationForgetSession(parent)
    }
  })
})


test('WHAT[delegation-negotiation-002] forged answer metadata cannot unlock resetScope without physical acceptance', async () => {
  const {
    bindDelegation, askNegotiation, prepareNegotiationReply,
    negotiationRequired, clearSessionState,
  } = await import('../../../dist/Context/Scope/Surface.js')
  const recipient = 'ses-neg-steer-002'
  const delegator = 'ses-neg-parent-002'
  clearSessionState(recipient)
  bindDelegation(recipient, delegator)
  const initial = user('a-original', 'Implement parser', 1)
  const first = await outbound(recipient, [initial])
  assert.equal(negotiationRequired(recipient), true)
  assert.match(first[0].parts[0].text, /negotiate\(questions: string\) NOW/)
  assert.doesNotMatch(first[0].parts[0].text, /Then call resetScope immediately/)

  const request = askNegotiation(recipient, 'Which parser and byte invariant?')
  assert.equal(request.ok, true)
  assert.equal(prepareNegotiationReply(request.receipt, 'Use the parser module. Preserve input bytes.').ok, true)

  const reply = {
    info: { id: 'u-answer', role: 'user', sessionID: recipient, time: { created: 2 } },
    parts: [{
      type: 'text',
      text: 'Delegator answered negotiate(questions):\nUse the parser module. Preserve input bytes.',
      metadata: { wanxiangshu_negotiation_receipt: request.receipt },
    }],
  }
  const now = await outbound(recipient, [initial, reply])
  assert.equal(negotiationRequired(recipient), true,
    'a provider transform must never secretly acknowledge a forged steer')
  assert.doesNotMatch(now.at(-1).parts[0].text, /Then call resetScope immediately/)
  assert.equal(reply.parts[0].text.includes('Preserve input bytes'), true)
  clearSessionState(recipient)
})


test('WHAT[delegation-negotiation-003] OpenCode registered provider hooks replay the parent system and transcript for a temp grandchild', async () => {
  const {
    negotiationRememberSystem, negotiationRememberMessages, negotiationRememberModel,
    negotiationStartTemporary, negotiationFinishTemporary, negotiationForgetSession,
  } = await import('../../../dist/OpenCode/Plugin/PluginTransforms.js')
  await withExecutablePlugin(async (hooks) => {
    const parent = 'ses-btw-hook-parent', temp = 'ses-btw-hook-grandchild'
    const original = [
      { info: { role: 'user', sessionID: parent, id: 'host-u-1' }, parts: [{ type: 'text', text: 'Prior parent task' }] },
      { info: { role: 'assistant', sessionID: parent, id: 'host-a-1' }, parts: [{ type: 'text', text: 'Prior parent result' }] },
    ]
    const originalBytes = JSON.stringify(original)
    const savedSystem = ['Parent common law', 'Parent actual provider system']
    negotiationRememberSystem({ sessionID: parent }, { system: savedSystem })
    negotiationRememberMessages(parent, { messages: original })
    negotiationRememberModel({ sessionID: parent, model: { providerID: 'provider', modelID: 'parent-model' } })
    assert.equal(negotiationStartTemporary(temp, parent).tag, 0)
    try {
      const input = { sessionID: temp }
      const system = { system: ['Temporary session system'] }
      const sameSystem = system.system
      await hooks['experimental.chat.system.transform'](input, system)
      assert.equal(system.system, sameSystem)
      assert.deepEqual(system.system, savedSystem)
      const question = { info: { role: 'user', sessionID: temp, id: 'host-btw-question' }, parts: [{ type: 'text', text: 'Where is the parser?' }] }
      const messages = { messages: [question] }
      const sameMessages = messages.messages
      await hooks['experimental.chat.messages.transform'](input, messages)
      assert.equal(messages.messages, sameMessages)
      assert.deepEqual(messages.messages.slice(0, original.length), original)
      assert.equal(messages.messages.at(-1).parts[0].text, 'Where is the parser?')
      assert.equal(JSON.stringify(original), originalBytes, 'the parent provider snapshot remains immutable')
    } finally {
      negotiationFinishTemporary(temp)
      negotiationForgetSession(parent)
    }
  })
})

test('WHAT[delegation-negotiation-004] registered negotiate sends ephemeral BTW and delivers an authenticated steer after a vetoed tool attempt', async () => {
  const { acceptAuthorityRoot } = await import('../../verification-system/tests/support/plugin-fixture.mjs')
  const { bindDelegation, negotiationRequired, clearSessionState } =
    await import('../../../dist/Context/Scope/Surface.js')
  const {
    negotiationRememberSystem, negotiationRememberMessages,
    negotiationRememberModel, negotiationForgetSession,
  } = await import('../../../dist/OpenCode/Plugin/PluginTransforms.js')

  await withExecutablePlugin(async (hooks, _dir, _created, runtime) => {
    const parent = 'ses-negotiate-real-parent', child = 'ses-negotiate-real-child'
    await acceptAuthorityRoot(runtime, parent, 'manager')
    await acceptAuthorityRoot(runtime, child, 'engineer')
    clearSessionState(child)
    bindDelegation(child, parent)
    const transcript = [
      { info: { id: 'parent-u', role: 'user', sessionID: parent }, parts: [{ type: 'text', text: 'Known parent history' }] },
    ]
    negotiationRememberSystem({ sessionID: parent }, { system: ['Reusable parent system'] })
    negotiationRememberMessages(parent, { messages: transcript })
    negotiationRememberModel({ sessionID: parent, model: { providerID: 'provider', modelID: 'parent-model' } })

    const session = runtime.client.session
    let prompts = 0
    let blocked = 0
    session.prompt = async (args) => {
      prompts += 1
      const tempId = args.path.id
      const system = { system: ['temporary system'] }
      await hooks['experimental.chat.system.transform']({ sessionID: tempId }, system)
      assert.deepEqual(system.system, ['Reusable parent system'])
      const messages = { messages: [{ info: { role: 'user', sessionID: tempId }, parts: args.body.parts }] }
      await hooks['experimental.chat.messages.transform']({ sessionID: tempId }, messages)
      assert.deepEqual(messages.messages[0], transcript[0])
      assert.equal(args.body.model?.modelID, 'parent-model')
      if (prompts === 1) {
        await assert.rejects(
          hooks['tool.execute.before']({ sessionID: tempId, tool: 'read', callID: 'btw-forbidden' }, { args: {} }),
          /Ephemeral negotiation is read-only/,
        )
        blocked += 1
        throw new Error('tool call intercepted before execution')
      }
      return { data: { info: { role: 'assistant' }, parts: [{ type: 'text', text: 'Use the byte-preserving parser.' }] } }
    }
    const premature = await hooks.tool.resetScope.execute(
      { newScopeObjective: 'too early', newScopeTurnBudget: 2 },
      { sessionID: child, agent: 'engineer', callID: 'premature-reset' },
    )
    assert.match(premature, /unavailable until the first negotiate/)
    assert.equal(negotiationRequired(child), true, 'a rejected reset cannot unlock the initial gate')
    const result = await hooks.tool.negotiate.execute(
      { questions: 'Which parser? Preserve which invariant?' },
      { sessionID: child, agent: 'engineer', callID: 'negotiate-1' },
    )
    assert.match(result, /Questions accepted/)
    const outgoingGuidance = () => runtime.prompts.filter(prompt =>
      prompt.path?.id === child && prompt.body?.parts?.some(p => p.type === 'text' && /byte-preserving parser/.test(p.text)),
    )
    for (let i = 0; i < 160 && outgoingGuidance().length === 0; i++) {
      await new Promise(resolve => setTimeout(resolve, 20))
    }
    assert.equal(blocked, 1, 'the first ephemeral tool call was vetoed before execution')
    assert.equal(prompts, 2, 'the temporary answer retried without losing the cached parent prefix')
    assert.equal(outgoingGuidance().length, 1, 'one genuine PromptDispatcher continuation is queued to the child')
    const guidance = outgoingGuidance()[0]
    const visibleAnswer = guidance.body.parts.find(part => part.type === 'text')?.text
    assert.match(visibleAnswer, /byte-preserving parser/)
    assert.ok(guidance.body.metadata?.wanxiangshu_prompt_key,
      'real continuation must carry a durable PromptKey')
    assert.equal(guidance.body.noReply, undefined,
      'do not fake a private promptAsync steer with a status-dependent noReply flag')
    const childHistory = (await session.messages({ path: { id: child } })).data
    assert.equal(childHistory.some(message =>
      message.role === 'user' && message.parts.some(part => /byte-preserving parser/.test(part.text ?? '')),
    ), true, 'the answer must be a visible, Host-owned user message')
    for (let i = 0; i < 80 && negotiationRequired(child); i++) {
      await new Promise(resolve => setTimeout(resolve, 20))
    }
    assert.equal(negotiationRequired(child), false,
      'only the real PhysicalAccepted callback, not the dispatch queue receipt, unlocks resetScope')
    negotiationForgetSession(parent)
    clearSessionState(child)
  })
})


test('WHAT[delegation-negotiation-005] Advisor Host permissions expose only negotiate and resetScope', async () => {
  const { advisorAgentConfig } = await import('../../../dist/OpenCode/Tools/StaticTools.js')
  const { rolePredicate } = await import('../../../dist/OpenCode/Tools/ToolRegistrySurface.js')
  const agent = advisorAgentConfig('Advisor role law')
  const permission = agent.permission
  const allowed = Object.entries(permission).filter(([, status]) => status === 'allow').map(([name]) => name).sort()
  assert.deepEqual(allowed, ['negotiate', 'resetScope'])
  assert.equal(permission.external_directory, 'deny', 'Advisor never receives broad external path permission')
  for (const name of ['skill', 'assume', 'fetch', 'bash-honeypot', 'todowrite-for-engineer']) {
    assert.equal(permission[name], 'deny', name + ' must be explicitly denied in Host agent config')
    assert.equal(rolePredicate(name, 'advisor'), false, name + ' must be denied at the product tool admission layer')
  }
  assert.equal(rolePredicate('negotiate', 'advisor'), true)
  assert.equal(rolePredicate('resetScope', 'advisor'), true)
})

test('WHAT[delegation-negotiation-006] user-facing root hides negotiate while a real managed child keeps it', async () => {
  await withExecutablePlugin(async (hooks, _directory, _ids, runtime) => {
    const root = 'ses-user-facing-negotiate-006'
    const message = {
      id: 'user-root-negotiate-006', sessionID: root, role: 'user', agent: 'manager',
      model: { providerID: 'provider', modelID: 'manager-model' },
      tools: { fetch: true },
    }
    const parts = [{ type: 'text', text: 'User instruction, no delegator above this session' }]
    runtime.pushHostMessage(root, { info: message, parts })
    await hooks['chat.message']({ sessionID: root, messageID: message.id, agent: 'manager' }, { message, parts })
    assert.equal(message.tools.negotiate, false, 'the root provider request MUST NOT expose negotiate')
    assert.equal(message.tools.fetch, true, 'unrelated tools remain unchanged')
    await assert.rejects(
      hooks['tool.execute.before']({ sessionID: root, tool: 'negotiate', callID: 'n-root-006' }, { args: { questions: 'Bad root attempt' } }),
      /user-facing sessions/,
      'even forged tool calls must be stopped before execution',
    )
  })

  const { withRealFork, forkCall } = await import('../../delegation/tests/support/real-fork.mjs')
  await withRealFork('ses-parent-negotiate-006', async ({ hooks, createdIds }) => {
    const receipt = await forkCall(hooks, 'ses-parent-negotiate-006', 'Ada', 'Current instruction only')
    assert.match(receipt, /Ada/)
    assert.equal(createdIds.length, 1)
    const child = createdIds[0]
    const message = {
      id: 'user-managed-negotiate-006', sessionID: child, role: 'user', agent: 'engineer',
      model: { providerID: 'provider', modelID: 'engineer-model' },
    }
    const parts = [{ type: 'text', text: 'Current instruction only' }]
    await hooks['chat.message']({ sessionID: child, messageID: message.id, agent: 'engineer' }, { message, parts })
    assert.notEqual(message.tools?.negotiate, false, 'a managed child must not lose first negotiation')
    await hooks['tool.execute.before'](
      { sessionID: child, tool: 'negotiate', callID: 'n-child-006' },
      { args: { questions: 'Which parser should I use?' } },
    )
  })
})


test('WHAT[delegation-negotiation-007] detached queue receipt cannot masquerade as a physically visible steer', async () => {
  const { acceptAuthorityRoot } = await import('../../verification-system/tests/support/plugin-fixture.mjs')
  const { bindDelegation, negotiationRequired, clearSessionState } =
    await import('../../../dist/Context/Scope/Surface.js')
  const {
    negotiationRememberSystem, negotiationRememberMessages,
    negotiationRememberModel, negotiationForgetSession,
  } = await import('../../../dist/OpenCode/Plugin/PluginTransforms.js')
  await withExecutablePlugin(async (hooks, _directory, _created, runtime) => {
    const parent = 'ses-neg-queued-parent', child = 'ses-neg-queued-child'
    await acceptAuthorityRoot(runtime, parent, 'manager')
    await acceptAuthorityRoot(runtime, child, 'engineer')
    bindDelegation(child, parent)
    negotiationRememberSystem({ sessionID: parent }, { system: ['system'] })
    negotiationRememberMessages(parent, {
      messages: [{ info: { role: 'user', id: 'p1', sessionID: parent }, parts: [{ type: 'text', text: 'parent' }] }],
    })
    negotiationRememberModel({ sessionID: parent, model: { providerID: 'provider', modelID: 'model' } })
    runtime.client.session.prompt = async () =>
      ({ data: { info: { role: 'assistant' }, parts: [{ type: 'text', text: 'The answer.' }] } })
    runtime.nextPromptReceiptOnly()
    const response = await hooks.tool.negotiate.execute(
      { questions: 'Which file?' },
      { sessionID: child, agent: 'engineer', callID: 'neg-queued-1' },
    )
    assert.match(response, /Questions accepted/)
    for (let i = 0; i < 80 && !runtime.prompts.some(item =>
      item.path?.id === child && item.body.parts.some(part => /The answer/.test(part.text ?? '')),
    ); i++) await new Promise(resolve => setTimeout(resolve, 20))
    const queued = runtime.prompts.find(item =>
      item.path?.id === child && item.body.parts.some(part => /The answer/.test(part.text ?? '')),
    )
    assert.ok(queued, 'Dispatcher must attempt a visible Host send')
    assert.ok(queued.body.metadata?.wanxiangshu_prompt_key, 'even a detached queue is durably keyed')
    assert.equal(negotiationRequired(child), true,
      'a detached transport receipt with no physical user message must not unlock resetScope')
    const history = (await runtime.client.session.messages({ path: { id: child } })).data
    assert.equal(history.some(message =>
      message.parts?.some(part => /The answer/.test(part.text ?? '')),
    ), false, 'no covert provider transform may inject an answer into the raw Host history')
    negotiationForgetSession(parent)
    clearSessionState(child)
  })
})

test('WHAT[context-compression-039] real OpenCode system transform upgrades stale owned scope sections without duplicate output', async () => {
  const transform = createWith(() => undefined, async () => undefined)
  const stale = '## Scope & Context Management (`resetScope`)\n[System: Then call resetScope immediately, before doing anything else.]'
  const system = ['unrelated host prompt', stale]
  const output = { system }
  await transform({ sessionID: 'ce017-stale-scope' })(output)
  assert.equal(output.system, system, 'reconcile an existing section in place')
  assert.equal(system.length, 2)
  assert.equal(system[0], 'unrelated host prompt')
  assert.match(system[1], /Scope & Context Management|Scope 与上下文/)
  assert.doesNotMatch(system[1], /\[System:|Then call resetScope immediately/)
  const first = system[1]
  await transform({ sessionID: 'ce017-stale-scope' })(output)
  assert.equal(system.length, 2, 'repeated current section must not be duplicated')
  assert.equal(system[1], first, 're-entry preserves identical current bytes')

  const staleZh = '## Scope 与上下文\n[System: 先自行判定是否应重置]'
  const zhSystem = ['another host prompt', staleZh]
  const zhOutput = { system: zhSystem }
  await transform({ sessionID: 'ce017-stale-chinese' })(zhOutput)
  assert.equal(zhSystem.length, 2)
  assert.doesNotMatch(zhSystem[1], /先自行判定|\[System:/)
  await transform({ sessionID: 'ce017-stale-chinese' })(zhOutput)
  assert.equal(zhSystem.length, 2, 'Chinese section must not duplicate on replay')

  const fresh = { system: ['an independent system prompt'] }
  await transform({ sessionID: 'ce017-fresh-scope' })(fresh)
  assert.equal(fresh.system.length, 2)
  assert.equal(fresh.system[0], 'an independent system prompt')
  assert.match(fresh.system[1], /Scope & Context Management|Scope 与上下文/)
  assert.doesNotMatch(fresh.system[1], /\[System:/)
})
