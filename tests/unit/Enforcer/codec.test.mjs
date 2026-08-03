// tests/unit/Enforcer/codec.test.mjs — spec/15 ENFORCER-020…025.
//
// The blog-argument codec. ENFORCER-190 pure tests 2-4:
//   2. any omitted field is zero;
//   3. any field order does not change the canonical result;
//   4. Damerau–Levenshtein mapping is deterministic.

import assert from 'node:assert/strict'
import test from 'node:test'
import { enforcer } from '../domain.mjs'

// ── value tolerance (ENFORCER-023) ──────────────────────────────────────────

test('ENFORCER_023_numeric_spellings_parse', () => {
  assert.equal(enforcer.parseScore(7), 7)
  assert.equal(enforcer.parseScore(7.0), 7)
  assert.equal(enforcer.parseScore('7'), 7)
  assert.equal(enforcer.parseScore(' 7 '), 7)
})

test('ENFORCER_023_invalid_values_are_zero_not_failed', () => {
  for (const bad of [true, false, 'high', 'likely', Number.NaN, Infinity, -1, 10, 7.5, {}, []]) {
    assert.equal(enforcer.parseScore(bad), undefined, `value ${JSON.stringify(bad)} should parse to zero`)
  }
})

// ── field normalization (ENFORCER-024) ──────────────────────────────────────

test('ENFORCER_024_normalization_collapses_spelling_variants', () => {
  assert.equal(enforcer.normalizeFieldName('ignored_tdd'), 'ignored-tdd')
  assert.equal(enforcer.normalizeFieldName('ignored.tdd'), 'ignored-tdd')
  assert.equal(enforcer.normalizeFieldName('ignored--tdd'), 'ignored-tdd')
  assert.equal(enforcer.normalizeFieldName('IGNORED TDD'), 'ignored-tdd')
  assert.equal(enforcer.normalizeFieldName('_ignored_tdd_'), 'ignored-tdd')
})

test('ENFORCER_024_normalization_is_idempotent', () => {
  for (const input of ['ignored_tdd', 'serial-when-parallel', 'PRIMITIVE Obsession']) {
    const once = enforcer.normalizeFieldName(input)
    const twice = enforcer.normalizeFieldName(once)
    assert.equal(once, twice)
  }
})

test('ENFORCER_024_damerau_levenshtein_is_symmetric', () => {
  assert.equal(enforcer.damerauLevenshtein('abc', 'acb'), 1) // transposition
  assert.equal(enforcer.damerauLevenshtein('acb', 'abc'), 1)
  assert.equal(enforcer.damerauLevenshtein('abc', 'abc'), 0)
  assert.equal(enforcer.damerauLevenshtein('abc', 'abd'), 1)
})

// ── decode (ENFORCER-020/022/025) ───────────────────────────────────────────

test('ENFORCER_022_missing_fields_decode_to_zero', () => {
  const call = enforcer.decodeCall({ text: 'work log entry' })
  assert.equal(call.Text, 'work log entry')
  assert.equal(call.Evidence, undefined)
  assert.deepEqual([...call.Scores], [])
})

test('ENFORCER_020_text_is_required_and_trimmed', () => {
  const empty = enforcer.decodeCall({ text: '   ' })
  assert.equal(empty.Text, undefined)
  const ok = enforcer.decodeCall({ text: '  hello  ' })
  assert.equal(ok.Text, 'hello')
})

test('ENFORCER_025_same_rule_multiple_fields_take_max', () => {
  const call = enforcer.decodeCall({
    text: 'entry',
    ignored_tdd: 5,
    'ignored-tdd': 8,
  })
  assert.equal(call.Scores.get('enforcement-g01'), 8)
})

test('ENFORCER_024_misspelled_field_maps_to_nearest_rule', () => {
  // 'ignored-tdd' is a real field; one typo away is 'enf_ingored_tdd'
  // (transposition). ENFORCER-024: nearest-neighbour mapping only applies to
  // unknown keys under the enf_ namespace.
  const call = enforcer.decodeCall({
    text: 'entry',
    enf_ingored_tdd: 6,
  })
  assert.equal(call.Scores.get('enforcement-g01'), 6)
})

test('ENFORCER_024_unknown_non_enf_field_is_ignored', () => {
  // A numeric metadata field without the enf_ prefix must NOT be mapped to a
  // rule (ENFORCER-024 namespace rule).
  const call = enforcer.decodeCall({
    text: 'entry',
    some_other_number: 7,
  })
  assert.deepEqual([...call.Scores], [])
})

test('ENFORCER_024_text_and_evidence_are_reserved_and_never_scored', () => {
  const call = enforcer.decodeCall({
    text: 'entry',
    evidence: 'evidence here',
    // These collide with nothing; ensure they are not treated as rule scores.
  })
  assert.equal(call.Text, 'entry')
  assert.equal(call.Evidence, 'evidence here')
  assert.deepEqual([...call.Scores], [])
})

test('ENFORCER_023_out_of_range_scores_are_zeroed_not_clamped', () => {
  const call = enforcer.decodeCall({
    text: 'entry',
    ignored_tdd: 10,
  })
  // 10 is invalid → zero → the rule gets no score.
  assert.deepEqual([...call.Scores], [])
})
