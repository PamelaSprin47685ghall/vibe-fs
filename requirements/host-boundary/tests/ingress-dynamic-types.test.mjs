import assert from 'node:assert/strict'
import test from 'node:test'
import * as toolHost from '../../../dist/OpenCode/Codec/ToolHostSurface.js'

test('WHAT[HOST-BOUNDARY-030] raw Host booleans arrays identities and agents reject coercion', () => {
  const malformed = toolHost.contextView(toolHost.contextDecode({
    sessionID: 7,
    agent: { toString: () => 'agent' },
    callID: ['call'],
    messageID: true,
    prompt: new String('prompt'),
  }))

  assert.deepEqual(malformed, {
    sessionId: '',
    agent: null,
    toolCallId: null,
    providerRunId: null,
    promptText: null,
  })
})

test('WHAT[HOST-BOUNDARY-030] optional Host arguments distinguish absence from malformed values', () => {
  for (const value of [null, undefined]) {
    const argumentsHandle = toolHost.makeArguments({ expected_tool_calls: value })
    assert.deepEqual(toolHost.argumentOptionalNonNegativeInteger(argumentsHandle, 'expected_tool_calls'), { ok: true, value: null })
  }

  for (const value of ['1', true, {}, [], new Number(1)]) {
    const argumentsHandle = toolHost.makeArguments({ expected_tool_calls: value })
    assert.deepEqual(toolHost.argumentOptionalNonNegativeInteger(argumentsHandle, 'expected_tool_calls'), { ok: false })
  }
})

test('WHAT[HOST-BOUNDARY-030] Host session observations and session.get agents reject coercion', () => {
  assert.deepEqual(
    toolHost.sessionObservation({ type: 'session.created', properties: { sessionID: 's1', info: { parentID: 'p1', agent: 'agent-a' } } }),
    { sessionId: 's1', hasParent: true, agent: 'agent-a' },
  )
  assert.equal(toolHost.sessionObservation({ type: 'session.created', properties: { sessionID: 7, agent: 'agent-a' } }), null)
  assert.deepEqual(
    toolHost.sessionObservation({ type: 'session.updated', properties: { sessionID: 's1', info: { parentID: 7, agent: {} } } }),
    { sessionId: 's1', hasParent: false, agent: null },
  )
  assert.equal(toolHost.sessionAgent({ agent: 7 }), null)
  assert.equal(toolHost.sessionAgent({ agent: { toString: () => 'agent-a' } }), null)
  assert.equal(toolHost.sessionAgent({ agent: ' agent-a ' }), ' agent-a ')
  assert.equal(toolHost.sessionAgent({ data: { agent: 'wrapped-agent' } }), 'wrapped-agent')
})
