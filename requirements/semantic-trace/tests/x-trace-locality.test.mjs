import assert from 'node:assert/strict'
import test from 'node:test'
import * as trace from '../../../dist/Context/Trace/SemanticTraceSurface.js'

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

test('WHAT[SEMANTIC-TRACE-002] provider-run query returns copied semantic evidence', () => {
  const parts = trace.providerRunParts('provider-run-a', fixture())
  assert.deepEqual(parts.map((part) => part.cursor.sequence), [1, 2])
  assert.deepEqual(parts.map((part) => part.providerRun), ['provider-run-a', 'provider-run-a'])
})

test('WHAT[SEMANTIC-TRACE-002] exact provider tool and Host identities are queryable', () => {
  const projection = fixture()
  assert.deepEqual(trace.toolResultParts('provider-run-a', 'call-a', projection).map((part) => part.cursor.sequence), [2])
  assert.deepEqual(
    trace.toolPartsForHostIdentity('provider-run-a', 'call-a', 'part-a', projection).map((part) => part.cursor.sequence),
    [1],
  )
})

test('WHAT[SEMANTIC-TRACE-004] stable Host message identity resolves at a durable cursor', () => {
  const projection = fixture()
  assert.equal(trace.tryHostMessageIdAt(trace.cursor(2), projection), 'message-a')
  assert.deepEqual(trace.partsForHostMessageIds(['message-b'], projection).map((part) => part.cursor.sequence), [3])
  assert.equal(trace.tryTurnOfHostMessageId('message-b', projection), 1)
})

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
