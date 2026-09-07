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

test('WHAT[SEMANTIC-TRACE-008] published trace contract contains exact implementation vocabulary', () => {
  const registry = JSON.parse(
    readFileSync(new URL('../../../scripts/checks/published-contracts.json', import.meta.url), 'utf8'),
  )
  const rows = registry.contracts.filter((entry) => entry.contract === 'SemanticTrace.Contract')
  assert.ok(rows.length > 0)
  assert.ok(rows.every((row) => row.symbols.every((symbol) => !symbol.includes('*'))))
  const published = new Set(rows.flatMap((row) => row.symbols))
  for (const [ownerType, field] of [
    ['XTraceProjectionState', 'Opening'],
    ['XTraceProjectionState', 'Parts'],
    ['XTraceProjectionState', 'Terminals'],
    ['XTraceCursor', 'Sequence'],
  ]) assert.equal(published.has(['Wanxiangshu.Context.Trace', ownerType, field].join('.')), false)
  for (const operation of [
    'Wanxiangshu.Context.Trace.XTraceProjection.orderedSemanticParts',
    'Wanxiangshu.Context.Trace.XTraceProjection.currentGenerationSemanticParts',
    'Wanxiangshu.Context.Trace.XTraceProjection.tryContiguousHostRange',
    'Wanxiangshu.Context.Trace.XTraceProjection.hasSemanticParts',
    'Wanxiangshu.Context.Trace.XTraceCapture.captureSessionMessagesWithReceipt',
    'Wanxiangshu.Context.Trace.XTraceCapture.captureObservedMessagesWithReceipt',
    'Wanxiangshu.Context.Trace.XTraceCapture.stableCaptureEligibility',
    'Wanxiangshu.Context.Trace.TerminalReporter.completeWithEvidence',
  ]) assert.equal(published.has(operation), true, `${operation} must be published`)
})
