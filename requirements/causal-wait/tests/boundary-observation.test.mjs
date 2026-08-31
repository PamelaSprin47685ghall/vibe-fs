// requirements/causal-wait/tests/boundary-observation.test.mjs
// CAUSAL-003 — 观测不进 Journal / Fact codec / 决策路径（本包拥有的产品事实）。
// 静态 enforcement 与本测试共用 scripts/checks/causal-wait-boundary.mjs 的纯 analyzer；
// 本文件不重建 scanner。

import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { assertOpaque } from '../../verification-system/tests/support/js-contract.mjs'

const causal = await import('../../../dist/Execution/Session/Wait/Surface.js')
const boundaryGate = await import('../../../scripts/checks/causal-wait-boundary.mjs')

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '../../..')

const descriptor = causal.createWait({
  waitKind: 'boundary-observation',
  owner: causal.owner('workflow', { id: 'decision-decoy' }),
  subject: { journalFact: 'lexical-decoy' },
  producer: causal.externalProducer('provider', { id: 'producer-1' }),
  escapes: [causal.escape('processLifetime')],
  source: 'boundary-observation.test',
})

const CLEAN_FILES = [
  {
    rel: 'Execution/Session/Wait/Registry.fs',
    text: 'let reader: IWaitSnapshotReader = registry :> IWaitSnapshotReader\n',
  },
  {
    rel: 'Persistence/Journal/EventStoreJournalCodec.fs',
    text: 'let payload = "CausalWait WaitKind IWaitSnapshotReader CausalAwait"\n',
  },
  {
    rel: 'Change/Fact.fs',
    text: 'let payload = @"DiagnosticWait DiagnosticWaitSnapshot"\n',
  },
  {
    rel: 'Interaction/Dispatch/NewDecision.fs',
    text: [
      '// IWaitSnapshotReader and CausalWaitHub.snapshot are lexical decoys',
      'let ordinary = "IWaitSnapshotReader CausalWaitHub.snapshot"',
      'let verbatim = @"DiagnosticWaitSnapshot CausalWaitSurface"',
      'let triple = """CausalWaitBridge.toPlainObject"""',
      '',
    ].join('\n'),
  },
]

const mutate = (relativePath, addition) => {
  const mutated = CLEAN_FILES.map((file) =>
    file.rel === relativePath ? { ...file, text: file.text + addition } : file,
  )
  assert.notDeepEqual(mutated, CLEAN_FILES, 'target mutation must change the legal fixture')
  return mutated
}

test('WHAT[CAUSAL-003] business observer cannot read the diagnostic snapshot', () => {
  const registry = causal.createRegistry()
  const observer = causal.observerCapability(registry)
  const reader = causal.snapshotReaderCapability(registry)
  assertOpaque(observer, 'wait observer capability')
  assertOpaque(reader, 'wait snapshot reader capability')

  const lease = causal.observerEnter(observer, descriptor)
  assert.equal(causal.readerSnapshot(reader).active.length, 1)
  assert.throws(() => causal.readerSnapshot(observer), /snapshot reader capability required/)

  causal.dispose(lease)
})

test('WHAT[CAUSAL-003] shared analyzer accepts the real production tree', () => {
  const files = boundaryGate.collectCausalWaitBoundaryFiles(ROOT)
  assert.ok(files.length > 0, 'production scan must contain F# files')
  assert.deepEqual(boundaryGate.analyzeObservationBoundary(files), [])
})

test('WHAT[CAUSAL-003] analyzer accepts comments and string literals as lexical decoys', () => {
  assert.deepEqual(boundaryGate.analyzeObservationBoundary(CLEAN_FILES), [])
})

test('WHAT[CAUSAL-003] analyzer rejects causal-wait vocabulary in any Journal codec', () => {
  const violations = boundaryGate.analyzeObservationBoundary(
    mutate('Persistence/Journal/EventStoreJournalCodec.fs', 'let reader: IWaitSnapshotReader = source\n'),
  )
  assert.deepEqual(violations, [
    'Persistence/Journal/EventStoreJournalCodec.fs: causal-wait vocabulary "IWaitSnapshotReader" must not enter Fact/Journal',
  ])
})

test('WHAT[CAUSAL-003] analyzer rejects causal-wait vocabulary in every Fact carrier', () => {
  const violations = boundaryGate.analyzeObservationBoundary(
    mutate('Change/Fact.fs', 'let wait: DiagnosticWait = source\n'),
  )
  assert.deepEqual(violations, [
    'Change/Fact.fs: causal-wait vocabulary "DiagnosticWait" must not enter Fact/Journal',
  ])
})

test('WHAT[CAUSAL-003] analyzer rejects snapshot reads in an unlisted future decision path', () => {
  const violations = boundaryGate.analyzeObservationBoundary(
    mutate('Interaction/Dispatch/NewDecision.fs', 'let reader: IWaitSnapshotReader = source\n'),
  )
  assert.deepEqual(violations, [
    'Interaction/Dispatch/NewDecision.fs: diagnostics read capability "IWaitSnapshotReader" is confined to Execution/Session/Wait',
  ])
})

test('WHAT[CAUSAL-003] analyzer rejects the diagnostic bridge locator outside its owner', () => {
  const violations = boundaryGate.analyzeObservationBoundary(
    mutate(
      'Interaction/Dispatch/NewDecision.fs',
      'let path = ".wanxiangshu/diagnostics/causal-waits.json"\n',
    ),
  )
  assert.deepEqual(violations, [
    'Interaction/Dispatch/NewDecision.fs: diagnostics read capability "causal-waits.json" is confined to Execution/Session/Wait',
  ])
})

test('WHAT[CAUSAL-003] collector fails closed when the production scan root is missing', () => {
  const root = mkdtempSync(join(tmpdir(), 'causal-wait-boundary-'))
  try {
    assert.throws(
      () => boundaryGate.collectCausalWaitBoundaryFiles(root),
      /causal-wait-boundary: required scan root missing: src\/Wanxiangshu/,
    )
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
})
