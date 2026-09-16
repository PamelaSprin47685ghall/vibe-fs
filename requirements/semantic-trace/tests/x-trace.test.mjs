import assert from 'node:assert/strict'
import test from 'node:test'
import * as trace from '../../../dist/Context/Trace/SemanticTraceSurface.js'

test('WHAT[SEMANTIC-TRACE-003] cursor vocabulary is monotonic and opaque', () => {
  const origin = trace.originCursor
  const second = trace.next(origin)
  const third = trace.next(second)
  assert.deepEqual([origin.sequence, second.sequence, third.sequence], [0, 1, 2])
  assert.equal(trace.isAfter(second, origin), true)
  assert.equal(trace.isAtOrAfter(second, second), true)
  assert.equal(trace.isBefore(origin, second), true)
})

test('WHAT[SEMANTIC-TRACE-006] range vocabulary is half-open', () => {
  const range = trace.createRange(trace.cursor(1), trace.cursor(3))
  assert.equal(trace.rangeContains(trace.cursor(0), range), false)
  assert.equal(trace.rangeContains(trace.cursor(1), range), true)
  assert.equal(trace.rangeContains(trace.cursor(2), range), true)
  assert.equal(trace.rangeContains(trace.cursor(3), range), false)
  assert.equal(trace.rangeIsEmpty(trace.createRange(trace.cursor(2), trace.cursor(2))), true)
})

test('WHAT[SEMANTIC-TRACE-007] flatten is the single semantic source', () => {
  const flat = trace.flatten([
    { role: 'user', parts: [trace.semanticText('task'), trace.semanticToolCall('read', '{}')] },
    { role: 'assistant', parts: [trace.semanticReasoning('considered'), trace.semanticText('done')] },
  ])
  assert.deepEqual(flat.map((entry) => entry.role), ['user', 'user', 'assistant', 'assistant'])
  assert.deepEqual(flat.map((entry) => entry.part.kind), ['text', 'tool-call', 'reasoning', 'text'])
})

test('WHAT[SEMANTIC-TRACE-005] canonical render is deterministic and omits provenance', () => {
  const items = [
    { cursor: trace.cursor(0), role: 'user', provenance: 'run-secret/msg-secret', part: trace.semanticText('Fix it.') },
    { cursor: trace.cursor(1), role: 'assistant', provenance: 'run-secret/msg-secret', part: trace.semanticReasoning('considered') },
    { cursor: trace.cursor(2), role: 'assistant', provenance: 'run-secret/msg-secret', part: trace.semanticToolCall('read', '{}') },
  ]
  const first = trace.render(items)
  assert.equal(trace.render(items), first)
  assert.match(first, /user: Fix it\./)
  assert.match(first, /\[tool call\] read \{\}/)
  assert.equal(first.includes('secret'), false)
  assert.equal(trace.render([]), '')
})
