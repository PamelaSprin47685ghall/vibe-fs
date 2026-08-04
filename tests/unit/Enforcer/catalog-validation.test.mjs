// tests/unit/Enforcer/catalog-validation.test.mjs — ENFORCER-170 dynamic N validation.
//
// Domain EnforcerCatalog.validate: schemaVersion=1, non-empty rules,
// unique id/field, ordinals 1..N, non-empty text. N = rules.Length (no hardcoded 120).
//
// Run via unit runner: node tests/unit/runner.mjs

import assert from 'node:assert/strict'
import test from 'node:test'
import { enforcerCatalog, enforcer } from '../domain.mjs'

const rule = (overrides) => enforcerCatalog.rule(overrides)

test('ENFORCER_170_validate_accepts_one_rule', () => {
  const result = enforcerCatalog.validate(1, [
    rule({ ruleId: 'r1', fieldName: 'f1', family: 'A', catalogOrdinal: 1 }),
  ])
  assert.equal(result.ok, true)
  assert.equal(result.value.length, 1)
  assert.equal(result.value[0].RuleId, 'r1')
  assert.equal(result.value[0].FieldName, 'f1')
  assert.equal(result.value[0].CatalogOrdinal, 1)
})

test('ENFORCER_170_validate_accepts_two_rules', () => {
  const result = enforcerCatalog.validate(1, [
    rule({ ruleId: 'r1', fieldName: 'f1', family: 'A', catalogOrdinal: 1 }),
    rule({ ruleId: 'r2', fieldName: 'f2', family: 'B', catalogOrdinal: 2 }),
  ])
  assert.equal(result.ok, true)
  assert.equal(result.value.length, 2)
  assert.deepEqual(
    result.value.map((r) => r.CatalogOrdinal),
    [1, 2],
  )
})

test('ENFORCER_170_validate_accepts_packaged_catalog_n_rules', () => {
  // Real package catalog (dynamic N; do not hardcode 120 in the validator).
  const packaged = enforcer.rules
  assert.ok(packaged.length > 0, 'packaged catalog must load at least one rule')
  const result = enforcerCatalog.validate(1, packaged)
  assert.equal(result.ok, true)
  assert.equal(result.value.length, packaged.length)
  const ordinals = result.value.map((r) => r.CatalogOrdinal)
  assert.deepEqual(
    ordinals,
    Array.from({ length: packaged.length }, (_, i) => i + 1),
  )
})

test('ENFORCER_170_validate_rejects_empty_catalog', () => {
  const result = enforcerCatalog.validate(1, [])
  assert.equal(result.ok, false)
  assert.equal(result.error, 'enforcer catalog must contain at least one rule')
})

test('ENFORCER_170_validate_rejects_duplicate_rule_id', () => {
  const result = enforcerCatalog.validate(1, [
    rule({ ruleId: 'dup', fieldName: 'f1', catalogOrdinal: 1 }),
    rule({ ruleId: 'dup', fieldName: 'f2', catalogOrdinal: 2 }),
  ])
  assert.equal(result.ok, false)
  assert.match(result.error, /duplicate rule id/)
  assert.match(result.error, /dup/)
})

test('ENFORCER_170_validate_rejects_duplicate_field', () => {
  const result = enforcerCatalog.validate(1, [
    rule({ ruleId: 'r1', fieldName: 'same-field', catalogOrdinal: 1 }),
    rule({ ruleId: 'r2', fieldName: 'same-field', catalogOrdinal: 2 }),
  ])
  assert.equal(result.ok, false)
  assert.match(result.error, /duplicate field/)
  assert.match(result.error, /same-field/)
})

test('ENFORCER_170_validate_rejects_ordinal_gap', () => {
  // Only [1, 3] — gap at 2.
  const result = enforcerCatalog.validate(1, [
    rule({ ruleId: 'r1', fieldName: 'f1', catalogOrdinal: 1 }),
    rule({ ruleId: 'r3', fieldName: 'f3', catalogOrdinal: 3 }),
  ])
  assert.equal(result.ok, false)
  assert.match(result.error, /catalogOrdinal must be contiguous 1\.\.2/)
})

test('ENFORCER_170_validate_rejects_unknown_schema_version', () => {
  const result = enforcerCatalog.validate(2, [
    rule({ ruleId: 'r1', fieldName: 'f1', catalogOrdinal: 1 }),
  ])
  assert.equal(result.ok, false)
  assert.equal(result.error, 'enforcer catalog schemaVersion must be 1, got 2')
})

test('ENFORCER_170_validate_rejects_empty_nudge_text', () => {
  const result = enforcerCatalog.validate(1, [
    rule({ ruleId: 'r1', fieldName: 'f1', nudge: '   ', catalogOrdinal: 1 }),
  ])
  assert.equal(result.ok, false)
  assert.match(result.error, /empty text on rule ordinal 1/)
})

test('ENFORCER_170_validate_rejects_empty_score_when', () => {
  const result = enforcerCatalog.validate(1, [
    rule({ ruleId: 'r1', fieldName: 'f1', scoreWhen: '', catalogOrdinal: 1 }),
  ])
  assert.equal(result.ok, false)
  assert.match(result.error, /empty text on rule ordinal 1/)
})
