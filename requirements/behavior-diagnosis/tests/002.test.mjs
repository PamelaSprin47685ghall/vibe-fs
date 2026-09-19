import assert from 'node:assert/strict'
import test from 'node:test'
import * as enforcer from '../../../dist/Enforcer/Surface.js'
import { integrationTest } from '../../verification-system/tests/support/tier-gate.mjs'

const catalogRules = enforcer.rules()

const catalogFields = enforcer.fieldNames()

test('WHAT[behavior-diagnosis-002] ENFORCER_170_rule_ids_are_unique', () => {
  const ids = catalogRules.map((r) => r.ruleId)
  assert.equal(new Set(ids).size, 120)
})

test('WHAT[behavior-diagnosis-002] ENFORCER_170_field_names_are_unique', () => {
  const fields = catalogRules.map((r) => r.fieldName)
  assert.equal(new Set(fields).size, 120)
})

test('WHAT[behavior-diagnosis-002] ENFORCER_170_catalog_ordinals_are_contiguous_from_1', () => {
  const orders = catalogRules.map((r) => r.lexicalOrder).sort((a, b) => a - b)
  assert.deepEqual(orders, Array.from({ length: 120 }, (_, i) => i + 1))
})

test('WHAT[behavior-diagnosis-002] ENFORCER_170_all_main_and_enforcer_texts_are_nonempty', () => {
  for (const rule of catalogRules) {
    assert.ok(rule.enforcerText.trim().length > 0, `rule ${rule.ruleId} has empty enforcer.md`)
    assert.ok(rule.mainText.trim().length > 0, `rule ${rule.ruleId} has empty main.md`)
  }
})

test('WHAT[behavior-diagnosis-002] ENFORCER_170_catalog_is_stable_and_not_corrupted', () => {
  // Regression: last tip main guidance must stay short and domain-specific.
  const l10 = catalogRules.find((r) => r.fieldName === 'incidental-complexity-dominates')
  assert.ok(l10, 'incidental-complexity-dominates must exist')
  assert.ok(l10.mainText.trim().length > 0, 'main.md must be non-empty')
  assert.ok(
    l10.mainText.includes('Incidental complexity') || l10.enforcerText.includes('Incidental complexity'),
    'tip substance about incidental complexity must remain in md texts',
  )

  // Field names are the contract surface (provider-visible args); their exact
  // list is part of the catalog contract. Order is lexical directory order.
  const fields = catalogRules.map((r) => r.fieldName)
  assert.equal(fields.length, new Set(fields).size)
  assert.equal(fields[0], 'abbreviation-anxiety')
  assert.equal(fields[119], 'wrong-rule-composition')
})

{
const fs = await import("node:fs");
const path = await import("node:path");
const { fileURLToPath } = await import("node:url");

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../')
const enforcerRoot = path.join(repoRoot, 'resources', 'enforcer')

integrationTest('WHAT[behavior-diagnosis-002] ENFORCER_resource_catalog_json_is_not_runtime_ssot', () => {
  assert.equal(fs.existsSync(path.join(enforcerRoot, 'catalog.json')), false)
})

integrationTest('WHAT[behavior-diagnosis-002] ENFORCER_resource_rulebook_load_is_independent_of_process_cwd', () => {
  const previous = process.cwd()
  try {
    process.chdir('/')
    const rules = enforcer.rules()
    assert.equal(rules.length, 120)
    assert.deepEqual(rules.map((r) => r.lexicalOrder), Array.from({ length: rules.length }, (_, i) => i + 1))
  } finally {
    process.chdir(previous)
  }
})
}
