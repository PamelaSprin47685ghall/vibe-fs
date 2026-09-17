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

test('WHAT[SEMANTIC-TRACE-004] stable Host message identity resolves at a durable cursor', () => {
  const projection = fixture()
  assert.equal(trace.tryHostMessageIdAt(trace.cursor(2), projection), 'message-a')
  assert.deepEqual(trace.partsForHostMessageIds(['message-b'], projection).map((part) => part.cursor.sequence), [3])
  assert.equal(trace.tryTurnOfHostMessageId('message-b', projection), 1)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const trace = await import("../../../dist/Context/Trace/SemanticTraceSurface.js");

const unwrap = (result) => {
  assert.equal(result.ok, true, result.ok ? '' : result.error)
  return result.projection
}
const append = (projection, sequence, generation, run, turn = 0) => unwrap(trace.appendPart(projection, {
  sequence,
  role: 'assistant',
  provenance: `g:${generation}/msg:${run}/host-part:part-${sequence}`,
  turn,
  partIndex: 0,
  kind: 'text',
  textRef: `blob-${sequence}`,
  textDigest: `digest-${sequence}`,
  providerRun: run,
}))

test('WHAT[SEMANTIC-TRACE-004] provider runs segment the ordered semantic projection', () => {
  let projection = trace.emptyProjection()
  projection = append(projection, 1, 0, 'run-a')
  projection = append(projection, 2, 0, 'run-b', 1)
  assert.deepEqual(trace.providerRunParts('run-a', projection).map((part) => part.cursor.sequence), [1])
  assert.deepEqual(trace.providerRunParts('run-b', projection).map((part) => part.cursor.sequence), [2])
})
test('WHAT[SEMANTIC-TRACE-004] a new provenance generation changes only the current-generation query', () => {
  let projection = trace.emptyProjection()
  projection = append(projection, 1, 0, 'run-before')
  projection = append(projection, 2, 1, 'run-after')
  assert.deepEqual(trace.orderedSemanticParts(projection).map((part) => part.cursor.sequence), [1, 2])
  assert.deepEqual(trace.currentGenerationSemanticParts(projection).map((part) => part.cursor.sequence), [2])
  assert.equal(trace.currentGenerationSemanticParts(projection)[0].generation, 1)
})
}
