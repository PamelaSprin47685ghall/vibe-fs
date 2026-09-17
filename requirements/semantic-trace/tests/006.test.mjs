import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const trace = await import("../../../dist/Context/Trace/SemanticTraceSurface.js");

const unwrap = (result) => {
  assert.equal(result.ok, true, result.ok ? '' : result.error)
  return result.projection
}
const append = (projection, sequence, options = {}) => unwrap(trace.appendPart(projection, {
  sequence,
  role: 'assistant',
  provenance: `g:0/msg:${options.messageId ?? 'message-a'}/host-part:${options.hostToolPartId ?? `part-${sequence}`}`,
  turn: options.turn ?? 0,
  partIndex: options.partIndex ?? sequence - 1,
  kind: options.kind ?? 'tool_call',
  toolName: options.toolName ?? 'todowrite',
  textRef: `blob-${sequence}`,
  textDigest: `digest-${sequence}`,
  providerRun: options.providerRun ?? 'provider-run-a',
  toolCallId: options.toolCallId ?? 'call-a',
  hostToolPartId: options.hostToolPartId ?? `part-${sequence}`,
}))
const fixture = () => {
  let projection = trace.emptyProjection()
  projection = append(projection, 1, { messageId: 'message-a', hostToolPartId: 'part-a', partIndex: 0 })
  projection = append(projection, 2, { messageId: 'message-a', hostToolPartId: 'part-b', partIndex: 1, kind: 'tool_result' })
  projection = append(projection, 3, { messageId: 'message-b', providerRun: 'provider-run-b', toolCallId: 'call-b', hostToolPartId: 'part-c', turn: 1, partIndex: 0 })
  return projection
}

test('WHAT[SEMANTIC-TRACE-006] Host message set resolves only to its exact contiguous range', () => {
  const projection = fixture()
  assert.deepEqual(trace.tryContiguousHostRange(['message-a'], projection), {
    start: { sequence: 1 },
    endExclusive: { sequence: 3 },
  })
  assert.equal(trace.tryContiguousHostRange(['message-a', 'missing'], projection), undefined)
})
test('WHAT[SEMANTIC-TRACE-006] range and frontier queries preserve half-open boundaries', () => {
  const projection = fixture()
  const range = trace.tryContiguousHostRange(['message-a'], projection)
  assert.deepEqual(trace.slice(range, projection).map((part) => part.cursor.sequence), [1, 2])
  assert.deepEqual(trace.rangeOfPart(trace.orderedSemanticParts(projection)[1]), {
    start: { sequence: 2 },
    endExclusive: { sequence: 3 },
  })
  assert.deepEqual(trace.semanticCursorAfter(trace.cursor(2), projection), { turn: 1, partIndex: 0 })
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const trace = await import("../../../dist/Context/Trace/SemanticTraceSurface.js");


test('WHAT[SEMANTIC-TRACE-006] range vocabulary is half-open', () => {
  const range = trace.createRange(trace.cursor(1), trace.cursor(3))
  assert.equal(trace.rangeContains(trace.cursor(0), range), false)
  assert.equal(trace.rangeContains(trace.cursor(1), range), true)
  assert.equal(trace.rangeContains(trace.cursor(2), range), true)
  assert.equal(trace.rangeContains(trace.cursor(3), range), false)
  assert.equal(trace.rangeIsEmpty(trace.createRange(trace.cursor(2), trace.cursor(2))), true)
})
}
