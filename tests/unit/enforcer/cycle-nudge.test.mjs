// tests/unit/Enforcer/cycle-nudge.test.mjs — spec/15 ENFORCER-041/042/043, 100/101/102.
//
// Multi-call cycle merge and nudge rendering. ENFORCER-190 pure tests 5-7:
//   5. any parallel completion order does not change the cycle merge;
//   6. score merge is commutative/associative/idempotent (max);
//   7. text merge depends only on PartOrdinal;
//  14. deterministic nudge bytes.

import assert from 'node:assert/strict'
import test from 'node:test'
import { enforcer } from '../support/domain.mjs'

const call = (text, scores = {}, evidence) => ({
  Text: text,
  Evidence: evidence,
  Scores: scores,
})

// ── cycle merge (ENFORCER-042) ──────────────────────────────────────────────

test('ENFORCER_042_text_merges_in_part_ordinal_order', () => {
  const merged = enforcer.mergeCalls([
    [1, call('second')],
    [0, call('first')],
  ])
  assert.equal(merged.MergedText, 'first\n\nsecond')
})

test('ENFORCER_042_scores_merge_by_max', () => {
  const merged = enforcer.mergeCalls([
    [0, call('a', { 'enforcement-g01': 3 })],
    [1, call('b', { 'enforcement-g01': 7 })],
  ])
  assert.equal(merged.MergedScores.get('enforcement-g01'), 7)
})

test('ENFORCER_042_merge_is_order_independent_for_scores', () => {
  const forward = enforcer.mergeCalls([
    [0, call('a', { 'enforcement-g01': 3, 'enforcement-a01': 5 })],
    [1, call('b', { 'enforcement-g01': 7 })],
  ])
  const backward = enforcer.mergeCalls([
    [1, call('b', { 'enforcement-g01': 7 })],
    [0, call('a', { 'enforcement-g01': 3, 'enforcement-a01': 5 })],
  ])
  assert.equal(JSON.stringify([...forward.MergedScores]), JSON.stringify([...backward.MergedScores]))
})

test('ENFORCER_042_evidence_dedupes_exact_duplicates', () => {
  const merged = enforcer.mergeCalls([
    [0, call('a', {}, 'same')],
    [1, call('b', {}, 'same')],
    [2, call('c', {}, 'other')],
  ])
  assert.equal(merged.MergedEvidence, 'same; other')
})

test('ENFORCER_043_valid_cycle_requires_nonempty_text', () => {
  const valid = enforcer.mergeCalls([[0, call('content')]])
  assert.equal(enforcer.isValidCycle(valid), true)
  const empty = enforcer.mergeCalls([[0, call('   ')]])
  assert.equal(enforcer.isValidCycle(empty), false)
})

// ── nudge rendering (ENFORCER-100/101/102) ──────────────────────────────────

test('ENFORCER_100_nudge_renders_hash_comment_lines', () => {
  const line = enforcer.renderLine('ignored-tdd', 'TDD order was skipped.')
  assert.equal(line, '# [ignored-tdd] TDD order was skipped.')
})

test('ENFORCER_100_evidence_is_a_single_trailing_line', () => {
  const batch = enforcer.renderBatch(
    [
      [1, 'ignored-tdd', 'TDD order was skipped.'],
      [2, 'unrecorded-lesson', 'A reusable lesson emerged.'],
    ],
    'implementation changed before any failing test',
  )
  assert.equal(
    batch,
    '# [ignored-tdd] TDD order was skipped.\n# [unrecorded-lesson] A reusable lesson emerged.\n# Evidence: implementation changed before any failing test',
  )
})

test('ENFORCER_101_rules_sort_by_catalog_ordinal_not_arbitrary_order', () => {
  const batch = enforcer.renderBatch(
    [
      [2, 'unrecorded-lesson', 'A reusable lesson emerged.'],
      [1, 'ignored-tdd', 'TDD order was skipped.'],
    ],
    null,
  )
  const lines = batch.split('\n')
  assert.equal(lines[0], '# [ignored-tdd] TDD order was skipped.')
  assert.equal(lines[1], '# [unrecorded-lesson] A reusable lesson emerged.')
})

test('ENFORCER_102_evidence_merge_dedupes_in_report_order', () => {
  assert.equal(enforcer.mergeEvidence(['one', 'two', 'one', 'three']), 'one; two; three')
  assert.equal(enforcer.mergeEvidence(['', 'x', '  ']), 'x')
})

test('ENFORCER_100_nudge_bytes_are_deterministic', () => {
  const a = enforcer.renderBatch([[1, 'ignored-tdd', 'TDD order was skipped.']], 'evidence')
  const b = enforcer.renderBatch([[1, 'ignored-tdd', 'TDD order was skipped.']], 'evidence')
  assert.equal(a, b)
})
