// tests/unit/Enforcer/codec.test.mjs — spec/15 ENFORCER-020…026 tip v2.
//
// Blog-argument codec: required tip (catalog field exact match), text, optional evidence.
// No score map, no fuzzy field mapping, no default tip.

import assert from 'node:assert/strict'
import test from 'node:test'
import { enforcer } from '../support/domain.mjs'

const firstField = () => enforcer.fieldNames()[0]
const firstRule = () => enforcer.tryFindByField(firstField())

// ── missing / unknown tip (ENFORCER-023) ────────────────────────────────────

test('ENFORCER_023_missing_tip_fails', () => {
  const result = enforcer.decodeCall({ text: 'work log entry' })
  assert.equal(result.ok, false)
  assert.equal(result.error, enforcer.MissingTipError)
  assert.equal(result.error, 'missing required argument: tip')
})

test('ENFORCER_023_empty_tip_fails', () => {
  for (const tip of ['', '   ', null]) {
    const result = enforcer.decodeCall({ text: 'entry', tip })
    assert.equal(result.ok, false, `tip=${JSON.stringify(tip)}`)
    assert.equal(result.error, enforcer.MissingTipError)
  }
})

test('ENFORCER_023_unknown_tip_fails', () => {
  const result = enforcer.decodeCall({ text: 'entry', tip: 'not-a-catalog-field' })
  assert.equal(result.ok, false)
  assert.equal(result.error, enforcer.unknownTipError('not-a-catalog-field'))
  assert.match(result.error, /^UnknownTip /)
})

test('ENFORCER_024_fuzzy_or_misspelled_tip_is_not_mapped', () => {
  // Old ENFORCER-024 fuzzy mapping is deleted; exact field only.
  const result = enforcer.decodeCall({ text: 'entry', tip: 'enf-primitive-obsessin' })
  assert.equal(result.ok, false)
  assert.match(result.error, /UnknownTip/)
})

// ── valid tip maps RuleId (ENFORCER-021/025) ────────────────────────────────

test('ENFORCER_021_valid_field_maps_exact_rule_id', () => {
  const field = firstField()
  const rule = firstRule()
  assert.ok(rule, `catalog must resolve ${field}`)

  const result = enforcer.decodeCall({ text: 'work log entry', tip: field })
  assert.equal(result.ok, true, result.ok ? '' : result.error)
  assert.deepEqual(result.value, {
    text: 'work log entry',
    evidence: undefined,
    tip: {
      ruleId: rule.ruleId,
      fieldName: rule.fieldName,
      catalogOrdinal: rule.catalogOrdinal,
    },
  })
})

test('ENFORCER_021_tip_trims_whitespace_before_lookup', () => {
  const field = firstField()
  const rule = firstRule()
  const result = enforcer.decodeCall({ text: 'entry', tip: `  ${field}  ` })
  assert.equal(result.ok, true)
  assert.equal(result.value.tip.ruleId, rule.ruleId)
  assert.equal(result.value.tip.fieldName, rule.fieldName)
})

// ── text / evidence reserved (ENFORCER-022) ─────────────────────────────────

test('ENFORCER_020_text_is_trimmed_empty_becomes_none', () => {
  const field = firstField()
  const empty = enforcer.decodeCall({ text: '   ', tip: field })
  assert.equal(empty.ok, true)
  assert.equal(empty.value.text, undefined)

  const ok = enforcer.decodeCall({ text: '  hello  ', tip: field })
  assert.equal(ok.ok, true)
  assert.equal(ok.value.text, 'hello')
})

test('ENFORCER_022_text_and_evidence_are_reserved_not_tips', () => {
  const field = firstField()
  const result = enforcer.decodeCall({
    text: 'entry',
    tip: field,
    evidence: 'evidence here',
  })
  assert.equal(result.ok, true)
  assert.equal(result.value.text, 'entry')
  assert.equal(result.value.evidence, 'evidence here')
  assert.equal(result.value.tip.fieldName, field)
})

test('ENFORCER_024_extra_numeric_properties_are_ignored', () => {
  const field = firstField()
  const result = enforcer.decodeCall({
    text: 'entry',
    tip: field,
    'primitive-obsession': 7,
    some_other_number: 3,
  })
  assert.equal(result.ok, true)
  assert.equal(result.value.tip.fieldName, field)
  assert.equal(result.value.evidence, undefined)
})

test('ENFORCER_022_has_valid_text_requires_nonempty_text', () => {
  const field = firstField()
  const empty = enforcer.decodeCall({ text: '   ', tip: field })
  assert.equal(enforcer.hasValidText(empty.value), false)
  const ok = enforcer.decodeCall({ text: 'body', tip: field })
  assert.equal(enforcer.hasValidText(ok.value), true)
})
