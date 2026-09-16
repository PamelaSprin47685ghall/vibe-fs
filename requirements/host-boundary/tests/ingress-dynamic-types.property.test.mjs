import assert from 'node:assert/strict'
import test from 'node:test'
import fc from 'fast-check'
import * as toolHost from '../../../dist/OpenCode/Codec/ToolHostSurface.js'

const malformedString = fc.anything({ withBoxedValues: true }).filter(value => typeof value !== 'string')

test('WHAT[HOST-BOUNDARY-030] malformed Host values never gain authority or throw', () => {
  fc.assert(fc.property(malformedString, value => {
    assert.doesNotThrow(() => toolHost.contextDecode({ sessionID: value, agent: value, callID: value, messageID: value }))
    assert.deepEqual(toolHost.contextView(toolHost.contextDecode({ sessionID: value, agent: value })), {
      sessionId: '',
      agent: null,
      toolCallId: null,
      providerRunId: null,
      promptText: null,
    })
    assert.doesNotThrow(() => toolHost.sessionObservation({ type: 'session.created', properties: { sessionID: value, agent: value } }))
    assert.equal(toolHost.sessionObservation({ type: 'session.created', properties: { sessionID: value, agent: value } }), null)
    assert.equal(toolHost.sessionAgent({ agent: value }), null)
  }), { seed: 43030, numRuns: 180 })
})
