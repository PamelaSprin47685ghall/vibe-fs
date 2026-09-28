import assert from 'node:assert/strict'
import test from 'node:test'
import * as trace from '../../../dist/Context/Trace/SemanticTraceSurface.js'



test('WHAT[semantic-trace-005] canonical render is deterministic and omits provenance', () => {
  const items = [
    { cursor: trace.cursor(0), role: 'user', provenance: 'run-secret/msg-secret', part: trace.semanticText('Fix it.') },
    { cursor: trace.cursor(1), role: 'assistant', provenance: 'run-secret/msg-secret', part: trace.semanticReasoning('considered') },
    { cursor: trace.cursor(2), role: 'assistant', provenance: 'run-secret/msg-secret', part: trace.semanticToolCall('read', '{}') },
  ]
  const first = trace.render(items)
  assert.equal(trace.render(items), first)
  assert.equal(trace.render(items.map((item) => ({ ...item, provenance: 'other-transport' }))), first)
  assert.notEqual(trace.render([{ ...items[0], part: trace.semanticText('Different task') }]), trace.render([items[0]]))
  assert.match(first, /user: Fix it\./)
  assert.match(first, /\[tool call\] read \{\}/)
  assert.equal(first.includes('secret'), false)
  assert.equal(trace.render([]), '')
})

test.todo('WHAT[semantic-trace-005] independently compile a consumer and reject access to opaque trace state, cursor and append refs; GAP-101')
