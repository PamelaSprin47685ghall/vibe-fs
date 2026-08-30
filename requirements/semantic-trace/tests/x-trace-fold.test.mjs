import assert from 'node:assert/strict'
import test from 'node:test'
import * as trace from '../../../dist/Context/Trace/SemanticTraceSurface.js'

const unwrap = (result) => {
  assert.equal(result.ok, true, result.ok ? '' : result.error)
  return result.projection
}

const part = (sequence, overrides = {}) => ({
  sequence,
  role: 'assistant',
  provenance: `g:0/msg:run-${sequence}/host-part:part-${sequence}`,
  turn: 0,
  partIndex: sequence - 1,
  kind: 'text',
  textRef: `blob-${sequence}`,
  textDigest: `digest-${sequence}`,
  providerRun: `run-${sequence}`,
  ...overrides,
})

test('WHAT[SEMANTIC-TRACE-001] opening evidence is copied verbatim and idempotent', () => {
  const first = unwrap(trace.appendOpening(trace.emptyProjection(), 'first task', ['r1', 'r2']))
  const second = unwrap(trace.appendOpening(first, 'first task', ['r1', 'r2']))
  assert.deepEqual(trace.openingEvidence(second), {
    assignmentText: 'first task',
    authoritativeRequirements: ['r1', 'r2'],
    constitutiveBody: '',
  })
  assert.equal(trace.hasOpening(second), true)
})

test('WHAT[SEMANTIC-TRACE-010] conflicting opening is rejected', () => {
  const projection = unwrap(trace.appendOpening(trace.emptyProjection(), 'first task', []))
  const rejected = trace.appendOpening(projection, 'second task', [])
  assert.equal(rejected.ok, false)
  assert.equal(rejected.error, 'opening-already-captured')
})

test('WHAT[SEMANTIC-TRACE-001] semantic parts append in strict cursor order', () => {
  let projection = trace.emptyProjection()
  projection = unwrap(trace.appendPart(projection, part(1)))
  projection = unwrap(trace.appendPart(projection, part(2, { kind: 'reasoning' })))
  projection = unwrap(trace.appendPart(projection, part(3, { kind: 'tool_call', toolName: 'read' })))
  assert.deepEqual(trace.orderedSemanticParts(projection).map((value) => value.cursor.sequence), [1, 2, 3])
  assert.deepEqual(trace.partKinds(projection), ['text', 'reasoning', 'tool_call'])
  assert.equal(trace.headCursor(projection).sequence, 4)
})

test('WHAT[SEMANTIC-TRACE-003] duplicate and retreating cursors are rejected', () => {
  const projection = unwrap(trace.appendPart(trace.emptyProjection(), part(5)))
  assert.equal(trace.appendPart(projection, part(5)).ok, false)
  assert.equal(trace.appendPart(projection, part(3)).ok, false)
})

test('WHAT[SEMANTIC-TRACE-001] terminal evidence is idempotent per provider run', () => {
  const terminal = { textRef: 'blob-terminal', textDigest: 'digest-terminal', providerRun: 'run-terminal' }
  const first = unwrap(trace.appendTerminal(trace.emptyProjection(), terminal))
  const second = unwrap(trace.appendTerminal(first, terminal))
  assert.deepEqual(trace.latestTerminalEvidence(second), {
    ...terminal,
    frontier: { sequence: 0 },
  })
  assert.deepEqual(trace.terminalEvidenceForProviderRun('run-terminal', second), trace.latestTerminalEvidence(second))
})

test('WHAT[SEMANTIC-TRACE-001] distinct provider runs retain distinct terminal evidence', () => {
  let projection = unwrap(trace.appendTerminal(trace.emptyProjection(), {
    textRef: 'blob-one', textDigest: 'digest-one', providerRun: 'run-one',
  }))
  projection = unwrap(trace.appendTerminal(projection, {
    textRef: 'blob-two', textDigest: 'digest-two', providerRun: 'run-two',
  }))
  assert.equal(trace.terminalEvidenceForProviderRun('run-one', projection).textRef, 'blob-one')
  assert.equal(trace.latestTerminalEvidence(projection).providerRun, 'run-two')
})

test('WHAT[SEMANTIC-TRACE-001] one provider run cannot publish conflicting terminal evidence', () => {
  const projection = unwrap(trace.appendTerminal(trace.emptyProjection(), {
    textRef: 'blob-one', textDigest: 'digest-one', providerRun: 'same-run',
  }))
  const rejected = trace.appendTerminal(projection, {
    textRef: 'blob-two', textDigest: 'digest-two', providerRun: 'same-run',
  })
  assert.equal(rejected.ok, false)
  assert.equal(rejected.error, 'terminal-already-captured')
})
