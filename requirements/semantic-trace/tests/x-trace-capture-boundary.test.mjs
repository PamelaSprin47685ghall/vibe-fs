import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'
import * as trace from '../../../dist/Context/Trace/SemanticTraceSurface.js'

const unwrap = (result) => {
  assert.equal(result.ok, true, result.ok ? '' : result.error)
  return result.projection
}

const partDescriptor = {
  sequence: 1,
  role: 'assistant',
  provenance: 'g:0/msg:message-1/host-part:part-1',
  turn: 0,
  partIndex: 0,
  kind: 'tool_call',
  toolName: 'read',
  textRef: 'blob-1',
  textDigest: 'digest-1',
  providerRun: 'provider-run-1',
  toolCallId: 'call-1',
  hostToolPartId: 'part-1',
}

test('WHAT[SEMANTIC-TRACE-002] copied semantic evidence excludes transport metadata', () => {
  const projection = unwrap(trace.appendPart(trace.emptyProjection(), partDescriptor))
  const evidence = trace.orderedSemanticParts(projection)[0]
  assert.equal(evidence.providerRun, 'provider-run-1')
  assert.equal(evidence.toolName, 'read')
  for (const forbidden of ['usage', 'cost', 'timestamp', 'elapsed', 'directory', 'finishReason', 'runtimeId', 'tokens', 'uiDelta']) {
    assert.equal(Object.hasOwn(evidence, forbidden), false, `semantic evidence must not carry ${forbidden}`)
  }
})

test('WHAT[SEMANTIC-TRACE-008] semantic surface admits only the three append transitions', () => {
  let projection = trace.emptyProjection()
  projection = unwrap(trace.appendOpening(projection, 'task', []))
  projection = unwrap(trace.appendPart(projection, partDescriptor))
  projection = unwrap(trace.appendTerminal(projection, {
    textRef: 'terminal-blob', textDigest: 'terminal-digest', providerRun: 'terminal-run',
  }))
  assert.equal(trace.hasOpening(projection), true)
  assert.equal(trace.hasSemanticParts(projection), true)
  assert.equal(trace.latestTerminalEvidence(projection).providerRun, 'terminal-run')
  assert.equal(trace.appendSpeculative, undefined)
})

test('WHAT[SEMANTIC-TRACE-008] no generic fact or full-history fold crosses the owner surface', () => {
  for (const forbidden of ['fact', 'envelope', 'fold', 'replay', 'session', 'appendReanchor']) {
    assert.equal(trace[forbidden], undefined, `${forbidden} must not bypass semantic-trace owner vocabulary`)
  }
})

test('WHAT[SEMANTIC-TRACE-008] signed trace contracts expose operations while keeping projection state opaque', () => {
  const projection = readFileSync(new URL('../../../src/Wanxiangshu/Context/Trace/Projection.fsi', import.meta.url), 'utf8')
  const cursor = readFileSync(new URL('../../../src/Wanxiangshu/Context/Trace/Cursor.fsi', import.meta.url), 'utf8')
  const capture = readFileSync(new URL('../../../src/Wanxiangshu/Context/Trace/Capture.fsi', import.meta.url), 'utf8')
  const terminal = readFileSync(new URL('../../../src/Wanxiangshu/Context/Trace/TerminalReporter.fsi', import.meta.url), 'utf8')

  assert.match(projection, /type XTraceProjectionState =\s*private/)
  assert.match(cursor, /^type XTraceCursor$/m)
  for (const operation of [
    'orderedSemanticParts',
    'currentGenerationSemanticParts',
    'tryContiguousHostRange',
    'hasSemanticParts',
  ]) assert.match(projection, new RegExp(`\\b${operation}:`), `${operation} must be signed`)
  for (const operation of [
    'captureSessionMessagesWithReceipt',
    'captureObservedMessagesWithReceipt',
    'stableCaptureEligibility',
  ]) assert.match(capture, new RegExp(`\\b${operation}:`), `${operation} must be signed`)
  assert.match(terminal, /\bcompleteWithEvidence:/)
})
