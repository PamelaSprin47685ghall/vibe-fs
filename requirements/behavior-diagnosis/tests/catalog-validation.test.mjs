// tests/unit/Enforcer/catalog-validation.test.mjs — ENFORCER-170 dynamic N validation.
//
// Domain EnforcerCatalog.validate: schemaVersion=1, non-empty rules,
// unique TipName/id/field (all equal), lexicalOrder 1..N, non-empty texts.
// N = rules.Length (no hardcoded 120).
//
// Run via unit runner: node tests/unit/runner.mjs

import assert from 'node:assert/strict'
import test from 'node:test'
import * as enforcer from '../../../dist/Enforcer/Surface.js'

const rule = (overrides = {}) => ({
  name: overrides.name ?? overrides.fieldName ?? overrides.ruleId ?? 'sample-field',
  ruleId: overrides.ruleId ?? overrides.name ?? overrides.fieldName ?? 'sample-field',
  fieldName: overrides.fieldName ?? overrides.name ?? overrides.ruleId ?? 'sample-field',
  enforcerText: overrides.enforcerText ?? 'enforcer body',
  mainText: overrides.mainText ?? 'main body',
  lexicalOrder: overrides.lexicalOrder ?? 1,
})

test('WHAT[BD-003] ENFORCER_170_validate_accepts_one_rule', () => {
  const result = enforcer.validate(1, [
    rule({ name: 'f1', lexicalOrder: 1 }),
  ])
  assert.equal(result.ok, true)
  assert.equal(result.value.length, 1)
  assert.equal(result.value[0].ruleId, 'f1')
  assert.equal(result.value[0].fieldName, 'f1')
  assert.equal(result.value[0].name, 'f1')
  assert.equal(result.value[0].lexicalOrder, 1)
})

test('WHAT[BD-003] ENFORCER_170_validate_accepts_two_rules', () => {
  const result = enforcer.validate(1, [
    rule({ name: 'f1', lexicalOrder: 1 }),
    rule({ name: 'f2', lexicalOrder: 2 }),
  ])
  assert.equal(result.ok, true)
  assert.equal(result.value.length, 2)
  assert.deepEqual(
    result.value.map((r) => r.lexicalOrder),
    [1, 2],
  )
})

test('WHAT[BD-003] ENFORCER_170_validate_accepts_packaged_catalog_n_rules', () => {
  // Real package rulebook (dynamic N; do not hardcode 120 in the validator).
  const packaged = enforcer.rules()
  assert.ok(packaged.length > 0, 'packaged catalog must load at least one rule')
  const result = enforcer.validate(1, packaged)
  assert.equal(result.ok, true)
  assert.equal(result.value.length, packaged.length)
  const orders = result.value.map((r) => r.lexicalOrder)
  assert.deepEqual(
    orders,
    Array.from({ length: packaged.length }, (_, i) => i + 1),
  )
})

test('WHAT[BD-003] ENFORCER_170_validate_rejects_empty_catalog', () => {
  const result = enforcer.validate(1, [])
  assert.equal(result.ok, false)
  assert.equal(result.error, 'enforcer catalog must contain at least one rule')
})

test('WHAT[BD-003] ENFORCER_170_validate_rejects_duplicate_rule_id', () => {
  const result = enforcer.validate(1, [
    rule({ name: 'dup', lexicalOrder: 1 }),
    rule({ name: 'dup', lexicalOrder: 2 }),
  ])
  assert.equal(result.ok, false)
  assert.match(result.error, /duplicate rule/)
  assert.match(result.error, /dup/)
})

test('WHAT[BD-003] ENFORCER_170_validate_rejects_duplicate_field', () => {
  // Same TipName twice is a duplicate field/name/id.
  const result = enforcer.validate(1, [
    rule({ name: 'same-field', lexicalOrder: 1 }),
    rule({ name: 'same-field', lexicalOrder: 2 }),
  ])
  assert.equal(result.ok, false)
  assert.match(result.error, /duplicate/)
  assert.match(result.error, /same-field/)
})

test('WHAT[BD-003] ENFORCER_170_validate_rejects_ordinal_gap', () => {
  // Only [1, 3] — gap at 2.
  const result = enforcer.validate(1, [
    rule({ name: 'f1', lexicalOrder: 1 }),
    rule({ name: 'f3', lexicalOrder: 3 }),
  ])
  assert.equal(result.ok, false)
  assert.match(result.error, /lexicalOrder must be contiguous 1\.\.2/)
})

test('WHAT[BD-003] ENFORCER_170_validate_rejects_unknown_schema_version', () => {
  const result = enforcer.validate(2, [
    rule({ name: 'f1', lexicalOrder: 1 }),
  ])
  assert.equal(result.ok, false)
  assert.equal(result.error, 'enforcer catalog schemaVersion must be 1, got 2')
})

test('WHAT[BD-003] ENFORCER_170_validate_rejects_empty_main_text', () => {
  const result = enforcer.validate(1, [
    rule({ name: 'f1', mainText: '   ', lexicalOrder: 1 }),
  ])
  assert.equal(result.ok, false)
  assert.match(result.error, /empty text or identity mismatch on rule ordinal 1/)
})

test('WHAT[BD-003] ENFORCER_170_validate_rejects_empty_enforcer_text', () => {
  const result = enforcer.validate(1, [
    rule({ name: 'f1', enforcerText: '', lexicalOrder: 1 }),
  ])
  assert.equal(result.ok, false)
  assert.match(result.error, /empty text or identity mismatch on rule ordinal 1/)
})

test('WHAT[BD-003] ENFORCER_170_validate_rejects_identity_mismatch', () => {
  const result = enforcer.validate(1, [
    rule({ name: 'tip-a', ruleId: 'other-id', fieldName: 'tip-a', lexicalOrder: 1 }),
  ])
  assert.equal(result.ok, false)
  assert.match(result.error, /empty text or identity mismatch/)
})
