import assert from 'node:assert/strict'
import test from 'node:test'
import * as trace from '../../../dist/Context/Trace/SemanticTraceSurface.js'



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
