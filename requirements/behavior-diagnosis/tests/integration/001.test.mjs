import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'
import test from 'node:test'
import { fileURLToPath } from 'node:url'
import * as enforcer from '../../../../dist/Enforcer/Surface.js'

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../../')
const enforcerRoot = path.join(repoRoot, 'resources', 'enforcer')

test('WHAT[BD-001] ENFORCER_resource_folder_rulebook_loads_with_contiguous_ordinals', () => {
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
