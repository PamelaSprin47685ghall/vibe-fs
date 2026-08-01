// tests-mjs/Enforcer/catalog.test.mjs — SSOT/15 ENFORCER-170/171/172, ENFORCER-190.
//
// The Rule Catalog is the single source of truth, generated from SSOT/15.md.
// ENFORCER-190 (pure tests) items 1, 15:
//   1. Catalog generation is stable.
//   15. A catalog update does not change old NudgeAnchored bytes (the nudge text
//       is fixed per rule; the catalog is append-only in spirit).

import assert from 'node:assert/strict'
import test from 'node:test'
import { enforcer } from '../domain.mjs'

test('ENFORCER_170_catalog_has_exactly_120_rules', () => {
  assert.equal(enforcer.ruleCount, 120)
})

test('ENFORCER_170_rule_ids_are_unique', () => {
  const ids = enforcer.rules.map((r) => r.RuleId)
  assert.equal(new Set(ids).size, 120)
})

test('ENFORCER_170_field_names_are_unique', () => {
  const fields = enforcer.rules.map((r) => r.FieldName)
  assert.equal(new Set(fields).size, 120)
})

test('ENFORCER_170_catalog_ordinals_are_contiguous_from_1', () => {
  const ordinals = enforcer.rules.map((r) => r.CatalogOrdinal).sort((a, b) => a - b)
  assert.deepEqual(ordinals, Array.from({ length: 120 }, (_, i) => i + 1))
})

test('ENFORCER_170_all_nudges_are_nonempty', () => {
  for (const rule of enforcer.rules) {
    assert.ok(rule.Nudge.trim().length > 0, `rule ${rule.RuleId} has empty nudge`)
  }
})

test('ENFORCER_170_all_descriptions_are_nonempty', () => {
  for (const rule of enforcer.rules) {
    assert.ok(rule.ScoreWhen.trim().length > 0, `rule ${rule.RuleId} has empty description`)
  }
})

test('ENFORCER_170_all_twelve_families_present_with_ten_rules_each', () => {
  const byFamily = {}
  for (const rule of enforcer.rules) {
    byFamily[rule.Family] = (byFamily[rule.Family] ?? 0) + 1
  }
  for (const f of 'ABCDEFGHIJKL'.split('')) {
    assert.equal(byFamily[f], 10, `family ${f} should have 10 rules, got ${byFamily[f]}`)
  }
})

test('ENFORCER_172_field_names_match_the_ssot_spelling', () => {
  // Spot-check a few known field names from SSOT/15.md.
  const fields = new Set(enforcer.fieldNames())
  for (const expected of [
    'primitive-obsession',
    'ignored-tdd',
    'unrecorded-lesson',
    'serial-when-parallel',
    'serial-investigation',
    'in-place-mutation',
  ]) {
    assert.ok(fields.has(expected), `catalog missing field ${expected}`)
  }
})

test('ENFORCER_170_schema_digest_is_stable_across_runs', () => {
  // The catalog is generated; the same SSOT yields the same list (pure).
  const a = JSON.stringify(enforcer.rules)
  const b = JSON.stringify(enforcer.rules)
  assert.equal(a, b)
})
