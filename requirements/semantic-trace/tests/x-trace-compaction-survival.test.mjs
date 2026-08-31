import assert from 'node:assert/strict'
import test from 'node:test'
import * as trace from '../../../dist/Context/Trace/SemanticTraceSurface.js'

const unwrap = (result) => {
  assert.equal(result.ok, true, result.ok ? '' : result.error)
  return result.projection
}

const append = (projection, sequence, generation, turn) => unwrap(trace.appendPart(projection, {
  sequence,
  role: turn === 0 ? 'user' : 'assistant',
  provenance: `g:${generation}/msg:message-${sequence}/host-part:part-${sequence}`,
  turn,
  partIndex: 0,
  kind: 'text',
  textRef: `blob-${sequence}`,
  textDigest: `digest-${sequence}`,
  providerRun: `run-${sequence}`,
}))

test('WHAT[SEMANTIC-TRACE-009] a new Host generation does not erase opening or semantic parts', () => {
  let projection = unwrap(trace.appendOpening(trace.emptyProjection(), 'first task', ['r1']))
  projection = append(projection, 1, 0, 0)
  projection = append(projection, 2, 0, 1)
  projection = append(projection, 3, 1, 0)

  assert.deepEqual(trace.orderedSemanticParts(projection).map((part) => part.cursor.sequence), [1, 2, 3])
  assert.deepEqual(trace.currentGenerationSemanticParts(projection).map((part) => part.cursor.sequence), [3])
  assert.deepEqual(trace.openingEvidence(projection).authoritativeRequirements, ['r1'])
})

test('WHAT[SEMANTIC-TRACE-009] cursor sequence remains global across Host generations', () => {
  let projection = trace.emptyProjection()
  projection = append(projection, 1, 0, 0)
  projection = append(projection, 2, 0, 1)
  projection = append(projection, 3, 1, 0)
  assert.equal(trace.latestPartCursor(projection).sequence, 3)
  assert.equal(trace.headCursor(projection).sequence, 4)
  assert.deepEqual(trace.rangeFrom(trace.cursor(2), projection), {
    start: { sequence: 2 },
    endExclusive: { sequence: 4 },
  })
})
