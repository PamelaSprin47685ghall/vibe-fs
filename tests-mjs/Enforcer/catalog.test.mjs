// tests-mjs/Enforcer/catalog.test.mjs — ENFORCER-170/171/172, ENFORCER-190.
//
// The Rule Catalog is resources/enforcer/catalog.json (runtime data, ENFORCER-170).
// ENFORCER-190 (pure tests) items 1, 15:
//   1. Catalog load is stable.
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

test('ENFORCER_172_field_names_match_the_rfc_spelling', () => {
  // Spot-check a few known field names from docs/rfcs/enforcer-nudge.md / resources/enforcer/catalog.json.
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

test('ENFORCER_170_catalog_is_stable_and_not_corrupted', () => {
  // Regression for the L10 corruption: an earlier extract once swallowed the entire
  // implementation-order chapter into ENF-L10's Nudge (measured >8,000 chars).
  // The last rule's Nudge must stay short and exact.
  const l10 = enforcer.rules.find((r) => r.FieldName === 'incidental-complexity-dominates')
  assert.equal(
    l10.Nudge,
    'Incidental complexity is dominating the design. Remove ceremony until the essential domain concepts become the visible structure.',
  )
  assert.ok(l10.Nudge.length < 200, `ENF-L10 Nudge must be short, got ${l10.Nudge.length} chars`)

  // Field names are the contract surface (provider-visible args); their exact
  // list is part of the catalog contract.
  const fields = enforcer.rules.map((r) => r.FieldName)
  assert.equal(fields.length, new Set(fields).size)
  // Spot-check the first and last fields stay stable.
  assert.equal(fields[0], 'primitive-obsession')
  assert.equal(fields[119], 'incidental-complexity-dominates')
})
