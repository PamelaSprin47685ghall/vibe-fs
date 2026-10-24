import test from 'node:test'
import assert from 'node:assert/strict'
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
