// tests/unit/Enforcer/cycle-nudge.test.mjs — spec/15 ENFORCER-041/042/043/025 tip v2.
//
// Multi-call cycle merge: single CanonicalTip by PartOrdinal; text/evidence join.
// Score-batch nudge rendering (ENFORCER-100..102) deleted with tip v2.

import assert from 'node:assert/strict'
import test from 'node:test'
import { enforcer } from '../support/domain.mjs'

const fields = () => enforcer.fieldNames()
const tipA = () => fields()[0]
const tipB = () => fields()[1]

const call = (text, tipField, evidence) => ({
  text,
  tipField,
  ...(evidence !== undefined ? { evidence } : {}),
})

// ── cycle merge (ENFORCER-042 / 025) ────────────────────────────────────────

test('ENFORCER_042_text_merges_in_part_ordinal_order', () => {
  const merged = enforcer.mergeCalls([
    [1, call('second', tipA())],
    [0, call('first', tipB())],
  ])
  assert.equal(merged.mergedText, 'first\n\nsecond')
})

test('ENFORCER_025_canonical_tip_is_first_by_part_ordinal', () => {
  const a = tipA()
  const b = tipB()
  const ruleA = enforcer.tryFindByField(a)

  const forward = enforcer.mergeCalls([
    [0, call('a', a)],
    [1, call('b', b)],
  ])
  assert.deepEqual(forward.tip, {
    ruleId: ruleA.ruleId,
    fieldName: ruleA.fieldName,
    catalogOrdinal: ruleA.catalogOrdinal,
  })
  assert.equal(forward.multiCall, true)

  const backward = enforcer.mergeCalls([
    [1, call('b', b)],
    [0, call('a', a)],
  ])
  // Same ordinal-first tip regardless of input list order.
  assert.deepEqual(backward.tip, forward.tip)
  assert.equal(backward.tip.fieldName, a)
  assert.notEqual(backward.tip.fieldName, b)
})

test('ENFORCER_025_multi_call_does_not_merge_or_max_tips', () => {
  // Second tip is never selected even if "more severe" under old score model.
  const a = tipA()
  const b = tipB()
  const merged = enforcer.mergeCalls([
    [0, call('first', a)],
    [1, call('second', b)],
  ])
  assert.equal(merged.tip.fieldName, a)
  assert.equal(merged.multiCall, true)
})

test('ENFORCER_042_evidence_dedupes_exact_duplicates', () => {
  const t = tipA()
  const merged = enforcer.mergeCalls([
    [0, call('a', t, 'same')],
    [1, call('b', t, 'same')],
    [2, call('c', t, 'other')],
  ])
  assert.equal(merged.mergedEvidence, 'same; other')
})

test('ENFORCER_043_valid_cycle_requires_nonempty_text', () => {
  const t = tipA()
  const valid = enforcer.mergeCalls([[0, call('content', t)]])
  assert.equal(enforcer.isValidCycle(valid), true)
  const empty = enforcer.mergeCalls([[0, call('   ', t)]])
  assert.equal(enforcer.isValidCycle(empty), false)
})

test('ENFORCER_042_single_call_is_not_multi_call', () => {
  const t = tipA()
  const merged = enforcer.mergeCalls([[0, call('solo', t)]])
  assert.equal(merged.multiCall, false)
  assert.equal(merged.tip.fieldName, t)
})
