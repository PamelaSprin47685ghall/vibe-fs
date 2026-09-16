// BD-001: live Rulebook SSOT and TipName identity space
import assert from 'node:assert/strict'
import test from 'node:test'
import * as enforcer from '../../../dist/Enforcer/Surface.js'
import * as blog from '../../../dist/Enforcer/BlogSurface.js'

const catalogRules = enforcer.rules()
const catalogFields = enforcer.fieldNames()

test('WHAT[BD-001] ENFORCER_170_catalog_has_exactly_120_rules', () => {
  assert.equal(enforcer.ruleCount(), 120)
})

test('WHAT[BD-001] ENFORCER_170_tip_name_equals_rule_id_and_field', () => {
  for (const rule of catalogRules) {
    assert.equal(rule.name, rule.ruleId, `Name/RuleId mismatch for ${rule.name}`)
    assert.equal(rule.name, rule.fieldName, `Name/FieldName mismatch for ${rule.name}`)
  }
})

test('WHAT[BD-001] ENFORCER_172_field_names_match_the_rfc_spelling', () => {
  const fields = new Set(catalogFields)
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

test('WHAT[BD-001] CHRONICLE_tip_enum_equals_catalog_field_names', () => {
  const fields = blog.tipFieldNames()
  assert.equal(fields.length, 120)
  assert.ok(fields.includes('primitive-obsession'))
})
