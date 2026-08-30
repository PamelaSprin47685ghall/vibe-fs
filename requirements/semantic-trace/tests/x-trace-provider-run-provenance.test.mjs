import assert from 'node:assert/strict'
import test from 'node:test'
import * as trace from '../../../dist/Context/Trace/SemanticTraceSurface.js'

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
