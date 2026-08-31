import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'
import * as trace from '../../../dist/Context/Trace/SemanticTraceSurface.js'
import { analyzeOwnerDependencies } from '../../../scripts/checks/owner-dependencies.mjs'

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

test('WHAT[SEMANTIC-TRACE-005] raw projection storage is rejected while copied semantic query is admitted', () => {
  const projectionProvider = 'src/Wanxiangshu/Context/Trace/Projection.fs'
  const cursorProvider = 'src/Wanxiangshu/Context/Trace/Cursor.fs'
  const consumer = 'src/Wanxiangshu/Foreign/Consumer.fs'
  const use = (provider, symbol) => ({
    consumerPath: consumer,
    providerPaths: [provider],
    symbol,
    symbolKind: 'FSharpMemberOrFunctionOrValue',
    line: 7,
    column: 4,
    isNamespace: false,
    isModule: false,
    isFromOpenStatement: false,
    isFromPattern: false,
    isFromType: false,
    isFromUse: true,
    missingDeclaration: false,
  })
  const contracts = {
    schema_version: 1,
    contracts: [
      {
        path: projectionProvider,
        owner: 'semantic-trace',
        node: 'semantic-trace-replayable-contract-cutover',
        contract: 'SemanticTrace.Contract',
        kind: 'published-contract',
        consumers: ['foreign-owner'],
        symbols: ['Wanxiangshu.Context.Trace.XTraceProjection.orderedSemanticParts'],
        justification: 'Only copied semantic evidence crosses this proof edge.',
      },
    ],
    physical_adapters: [],
    composition_roots: [],
    requirement_dependencies: [],
    owner_cycle_justifications: [],
  }
  const projectionQuery = 'Wanxiangshu.Context.Trace.XTraceProjection.orderedSemanticParts'
  const input = (provider, symbol) => ({
    compilePaths: [projectionProvider, cursorProvider, consumer],
    semanticOwners: {
      owners: ['semantic-trace', 'foreign-owner'],
      ownership: [
        { path: projectionProvider, owner: 'semantic-trace' },
        { path: cursorProvider, owner: 'semantic-trace' },
        { path: consumer, owner: 'foreign-owner' },
      ],
    },
    publishedContracts: contracts,
    symbolUses: [
      use(projectionProvider, projectionQuery),
      ...(provider && symbol ? [use(provider, symbol)] : []),
    ],
  })

  const admitted = analyzeOwnerDependencies(input())
  assert.equal(admitted.ok, true, JSON.stringify(admitted.violations))

  const forbidden = [
    [projectionProvider, 'Wanxiangshu.Context.Trace.XTraceProjectionState.Parts'],
    [projectionProvider, 'Wanxiangshu.Context.Trace.XTracePartRef.Cursor'],
    [projectionProvider, 'Wanxiangshu.Context.Trace.XTraceTerminalRef.Frontier'],
    [projectionProvider, 'Wanxiangshu.Context.Trace.XTraceProjection.parts'],
    [projectionProvider, 'Wanxiangshu.Context.Trace.XTraceProjection.head'],
    [projectionProvider, 'Wanxiangshu.Context.Trace.XTraceProjection.headSequence'],
    [projectionProvider, 'Wanxiangshu.Context.Trace.XTraceProjection.currentGenerationParts'],
    [projectionProvider, 'Wanxiangshu.Context.Trace.XTraceProjection.latestTerminal'],
    [projectionProvider, 'Wanxiangshu.Context.Trace.XTraceProjection.terminalForProviderRun'],
    [projectionProvider, 'Wanxiangshu.Context.Trace.XTraceProjection.semanticCursorFor'],
    [projectionProvider, 'Wanxiangshu.Context.Trace.XTraceProjection.tryHostMessageId'],
    [cursorProvider, 'Wanxiangshu.Context.Trace.XTraceCursor.Sequence'],
    [cursorProvider, 'Wanxiangshu.Context.Trace.RecordCoverage.IngestedThrough'],
    [cursorProvider, 'Wanxiangshu.Context.Trace.XTraceRange.StartInclusive'],
    [cursorProvider, 'Wanxiangshu.Context.Trace.XTraceRange.EndExclusive'],
  ]

  for (const [provider, symbol] of forbidden) {
    assert.ok(
      analyzeOwnerDependencies(input(provider, symbol)).violations
        .some((violation) => ['unauthorized-contract-symbol', 'cross-owner-private-import', 'foreign-execution-position'].includes(violation.code)),
      `${symbol} must remain owner-private`,
    )
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
