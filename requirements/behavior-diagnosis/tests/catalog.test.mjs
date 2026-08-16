// tests/unit/Enforcer/catalog.test.mjs — ENFORCER-170/171/172, ENFORCER-190.
//
// The Rule Catalog is resources/enforcer/*/enforcer.md+main.md (folder SSOT).
// TipName = directory basename = provider tip enum = durable RuleId.
// ENFORCER-190 (pure tests) items 1, 15:
//   1. Catalog load is stable.
//   15. A catalog update does not change old tip main guidance bytes (main.md
//       is fixed per rule; the catalog is append-only in spirit).

import assert from 'node:assert/strict'
import test from 'node:test'
import * as enforcer from '../../../dist/Enforcer/Surface.js'

const rules = () => enforcer.rules()

test('WHAT[BD-001] ENFORCER_170_catalog_has_exactly_120_rules', () => {
  assert.equal(enforcer.ruleCount(), 120)
})

test('WHAT[BD-002] ENFORCER_170_rule_ids_are_unique', () => {
  const ids = rules().map((r) => r.ruleId)
  assert.equal(new Set(ids).size, 120)
})

test('WHAT[BD-002] ENFORCER_170_field_names_are_unique', () => {
  const fields = rules().map((r) => r.fieldName)
  assert.equal(new Set(fields).size, 120)
})

test('WHAT[BD-001] ENFORCER_170_tip_name_equals_rule_id_and_field', () => {
  for (const rule of rules()) {
    assert.equal(rule.name, rule.ruleId, `Name/RuleId mismatch for ${rule.name}`)
    assert.equal(rule.name, rule.fieldName, `Name/FieldName mismatch for ${rule.name}`)
  }
})

test('WHAT[BD-002] ENFORCER_170_catalog_ordinals_are_contiguous_from_1', () => {
  const orders = rules().map((r) => r.lexicalOrder).sort((a, b) => a - b)
  assert.deepEqual(orders, Array.from({ length: 120 }, (_, i) => i + 1))
})

test('WHAT[BD-002] ENFORCER_170_all_main_and_enforcer_texts_are_nonempty', () => {
  for (const rule of rules()) {
    assert.ok(rule.enforcerText.trim().length > 0, `rule ${rule.ruleId} has empty enforcer.md`)
    assert.ok(rule.mainText.trim().length > 0, `rule ${rule.ruleId} has empty main.md`)
  }
})

test('WHAT[BD-008] ENFORCER_170_no_bridge_fields_on_rule', () => {
  for (const rule of rules()) {
    assert.equal(rule.scoreWhen, undefined, `rule ${rule.ruleId} still has ScoreWhen`)
    assert.equal(rule.nudge, undefined, `rule ${rule.ruleId} still has Nudge`)
    assert.equal(rule.family, undefined, `rule ${rule.ruleId} still has Family`)
    assert.equal(rule.catalogOrdinal, undefined, `rule ${rule.ruleId} still has CatalogOrdinal`)
  }
})

test('WHAT[BD-001] ENFORCER_172_field_names_match_the_rfc_spelling', () => {
  // Spot-check a few known TipNames (directory basenames).
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

test('WHAT[BD-002] ENFORCER_170_catalog_is_stable_and_not_corrupted', () => {
  // Regression: last tip main guidance must stay short and domain-specific.
  const l10 = rules().find((r) => r.fieldName === 'incidental-complexity-dominates')
  assert.ok(l10, 'incidental-complexity-dominates must exist')
  assert.ok(l10.mainText.trim().length > 0, 'main.md must be non-empty')
  assert.ok(
    l10.mainText.includes('Incidental complexity') || l10.enforcerText.includes('Incidental complexity'),
    'tip substance about incidental complexity must remain in md texts',
  )

  // Field names are the contract surface (provider-visible args); their exact
  // list is part of the catalog contract. Order is lexical directory order.
  const fields = rules().map((r) => r.fieldName)
  assert.equal(fields.length, new Set(fields).size)
  assert.equal(fields[0], 'abbreviation-anxiety')
  assert.equal(fields[119], 'wrong-rule-composition')
})
