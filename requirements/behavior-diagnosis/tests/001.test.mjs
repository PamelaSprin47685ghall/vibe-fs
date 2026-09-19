import test from 'node:test'
import { integrationTest } from '../../verification-system/tests/support/tier-gate.mjs'

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

{
const { default: assert } = await import("node:assert/strict");
const fs = await import("node:fs");
const path = await import("node:path");
const { fileURLToPath } = await import("node:url");
const enforcer = await import("../../../dist/Enforcer/Surface.js");

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../')
const enforcerRoot = path.join(repoRoot, 'resources', 'enforcer')

integrationTest('WHAT[behavior-diagnosis-001] ENFORCER_resource_folder_rulebook_loads_with_contiguous_ordinals', () => {
  const rules = enforcer.rules()
  assert.ok(Array.isArray(rules))
  assert.equal(rules.length, 120)
  assert.deepEqual(rules.map((r) => r.lexicalOrder), Array.from({ length: rules.length }, (_, i) => i + 1))
  assert.equal(enforcer.validate(1, rules).ok, true)
  assert.equal(new Set(rules.map((r) => r.name)).size, rules.length)
  for (const rule of rules) {
    assert.equal(rule.name, rule.ruleId)
    assert.equal(rule.name, rule.fieldName)
    assert.ok(rule.enforcerText.trim().length > 0)
    assert.ok(rule.mainText.trim().length > 0)
    assert.equal(rule.scoreWhen, undefined)
    assert.equal(rule.nudge, undefined)
    assert.equal(rule.family, undefined)
    assert.equal(rule.catalogOrdinal, undefined)
  }

  const dirs = fs.readdirSync(enforcerRoot, { withFileTypes: true }).filter((d) => d.isDirectory()).map((d) => d.name).sort()
  assert.deepEqual(rules.map((r) => r.name), dirs)
})
}
