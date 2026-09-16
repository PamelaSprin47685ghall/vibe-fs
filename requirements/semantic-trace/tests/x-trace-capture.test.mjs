import assert from 'node:assert/strict'
import test from 'node:test'
import * as trace from '../../../dist/Context/Trace/SemanticTraceSurface.js'

test('WHAT[SEMANTIC-TRACE-002] capture mapper copies text and reasoning semantics', () => {
  assert.deepEqual(trace.mapPart(trace.textPart('hello')), { kind: 'text', text: 'hello' })
  assert.deepEqual(trace.mapPart(trace.reasoningPart('considering')), { kind: 'reasoning', text: 'considering' })
})

test('WHAT[SEMANTIC-TRACE-002] capture mapper drops transport call identities', () => {
  assert.deepEqual(trace.mapPart(trace.toolCallPart('call-1', 'read', '{}')), {
    kind: 'tool-call',
    name: 'read',
    args: '{}',
  })
  assert.deepEqual(trace.mapPart(trace.toolResultPart('call-1', 'output')), {
    kind: 'tool-result',
    result: 'output',
  })
})

test('WHAT[SEMANTIC-TRACE-002] activity bookkeeping has no semantic part', () => {
  assert.equal(trace.mapPart(trace.activityPart('step-start')), undefined)
})
