import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const enforcer = await import("../../../dist/Enforcer/Surface.js");
const blog = await import("../../../dist/Enforcer/BlogSurface.js");


test('WHAT[behavior-diagnosis-001] CHRONICLE_tip_enum_equals_catalog_field_names', () => {
  const fields = blog.tipFieldNames()
  assert.equal(fields.length, 120)
  assert.ok(fields.includes('primitive-obsession'))
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const enforcer = await import("../../../dist/Enforcer/Surface.js");

const catalogRules = enforcer.rules()
const catalogFields = enforcer.fieldNames()

test('WHAT[behavior-diagnosis-001] ENFORCER_170_catalog_has_exactly_120_rules', () => {
  assert.equal(enforcer.ruleCount(), 120)
})
test('WHAT[behavior-diagnosis-001] ENFORCER_170_tip_name_equals_rule_id_and_field', () => {
  for (const rule of catalogRules) {
    assert.equal(rule.name, rule.ruleId, `Name/RuleId mismatch for ${rule.name}`)
    assert.equal(rule.name, rule.fieldName, `Name/FieldName mismatch for ${rule.name}`)
  }
})
test('WHAT[behavior-diagnosis-001] ENFORCER_172_field_names_match_the_rfc_spelling', () => {
  // Spot-check a few known TipNames (directory basenames).
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
}
