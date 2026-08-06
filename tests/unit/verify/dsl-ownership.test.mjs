/**
 * FLOW-006: dsl-ownership gate must go red on each forbidden pattern.
 * Synthetic source only — never mutates production trees.
 */
import assert from 'node:assert/strict'
import test from 'node:test'
import {
  FORBIDDEN,
  GATE_NAMES,
  evaluateThreshold,
  scanFiles,
  scanText,
} from '../../../scripts/checks/dsl-ownership.mjs'

const NEGATIVES = [
  {
    gate: 'mutable',
    source: ['module Sample', 'let mutable counter = 0'].join('\n'),
    line: 2,
  },
  {
    gate: 'flow-lift',
    source: ['module Sample', 'let escape t = Flow.create t'].join('\n'),
    line: 2,
  },
  {
    gate: 'second-runtime-protocol',
    source: ['module Sample', 'type WorkflowCommand =', '    | ReadSnapshot'].join('\n'),
    line: 2,
  },
  {
    gate: 'business-interpreter',
    source: ['module Sample', 'module WorkflowInterpreter =', '    let run program = program'].join('\n'),
    line: 2,
  },
  {
    gate: 'infrastructure-leak',
    source: ['module Sample', 'open Wanxiangshu.Infrastructure'].join('\n'),
    line: 2,
  },
  {
    gate: 'program-counter',
    source: ['module Sample', 'type Flags = { Dirty: bool }'].join('\n'),
    line: 2,
  },
  {
    gate: 'behaviour-bool',
    source: ['module Sample', 'type State = { HasPendingCompletion: bool }'].join('\n'),
    line: 2,
  },
]

const CLEAN = [
  'module Sample',
  'open Wanxiangshu.Domain',
  'let run operation = task {',
  '    let! result = operation ()',
  '    return result',
  '}',
].join('\n')

test('DSL_OWNERSHIP_exports_seven_named_gates', () => {
  assert.deepEqual(GATE_NAMES, [
    'mutable',
    'flow-lift',
    'second-runtime-protocol',
    'business-interpreter',
    'infrastructure-leak',
    'program-counter',
    'behaviour-bool',
  ])
  assert.equal(FORBIDDEN.length, 7)
})

for (const sample of NEGATIVES) {
  test(`DSL_OWNERSHIP_negative_${sample.gate}_goes_red`, () => {
    const hits = scanText(sample.source, `Domain/Negative_${sample.gate}.fs`)
    const ofGate = hits.filter((v) => v.gate === sample.gate)
    assert.ok(ofGate.length >= 1, `expected gate ${sample.gate} to fire`)
    assert.equal(ofGate[0].line, sample.line)
    assert.equal(ofGate[0].file, `Domain/Negative_${sample.gate}.fs`)
  })
}

test('DSL_OWNERSHIP_clean_source_stays_green', () => {
  const hits = scanText(CLEAN, 'Domain/Clean.fs')
  assert.deepEqual(hits, [])
})

test('DSL_OWNERSHIP_TddPhase_domain_type_is_not_behaviour_bool', () => {
  const source = ['module Sample', 'let payload = { TddPhase: TddPhase option }'].join('\n')
  const hits = scanText(source, 'Domain/Clean.fs')
  assert.deepEqual(hits, [])
})

test('DSL_OWNERSHIP_domain_pending_evidence_is_not_behaviour_bool', () => {
  const source = [
    'module Sample',
    'type Witness = | PerfectPending of unit | StillPending of bool | ConflictPending of unit',
    'let recoveryBudgetSpent claim = true',
    'let tryTakePending cell = None',
    'let isPerfectPending w = true',
  ].join('\n')
  const hits = scanText(source, 'Domain/Clean.fs')
  assert.deepEqual(hits, [])
})


test('DSL_OWNERSHIP_scanFiles_aggregates_entries', () => {
  const hits = scanFiles([
    { file: 'a.fs', text: NEGATIVES[0].source },
    { file: 'b.fs', text: CLEAN },
    { file: 'c.fs', text: NEGATIVES[1].source },
  ])
  const gates = hits.map((v) => v.gate)
  assert.ok(gates.includes('mutable'))
  assert.ok(gates.includes('flow-lift'))
  assert.equal(
    hits.filter((v) => v.file === 'b.fs').length,
    0,
  )
})


test('DSL_OWNERSHIP_Domain_pure_scratch_mutable_is_not_gate_red', () => {
  const source = ['module Sample', 'let scratch () =', '    let mutable acc = 0', '    acc'].join('\n')
  assert.deepEqual(scanText(source, 'src/Wanxiangshu/Domain/Sample.fs'), [])
  assert.ok(scanText(source, 'src/Wanxiangshu/Session/Sample.fs').some((h) => h.gate === 'mutable'))
})

test('DSL_OWNERSHIP_comment_only_line_is_ignored', () => {
  const source = ['module Sample', '// type HiddenCommand =', '// let mutable x = 1'].join('\n')
  assert.deepEqual(scanText(source), [])
})

test('DSL_OWNERSHIP_threshold_freeze_semantics', () => {
  assert.equal(evaluateThreshold(0, -1).ok, true)
  assert.equal(evaluateThreshold(1, -1).ok, false)
  assert.equal(evaluateThreshold(317, 317).ok, true)
  assert.equal(evaluateThreshold(318, 317).ok, false)
  assert.equal(evaluateThreshold(317, 317).reason, 'within-threshold')
  assert.equal(evaluateThreshold(318, 317).reason, 'exceeds-threshold')
})
