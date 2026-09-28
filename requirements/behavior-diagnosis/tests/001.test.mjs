import assert from 'node:assert/strict'
import test from 'node:test'
import { readdirSync } from 'node:fs'
import * as enforcer from '../../../dist/Enforcer/Surface.js'

const directory = new URL('../../../resources/enforcer/', import.meta.url)

test('WHAT[behavior-diagnosis-001] packaged identities come from directory basenames', () => {
  const names = readdirSync(directory, { withFileTypes: true })
    .filter((entry) => entry.isDirectory()).map((entry) => entry.name).sort()
  assert.ok(names.length > 0)
  const rules = enforcer.rules()
  assert.deepEqual(rules.map((rule) => rule.name), names)
  assert.deepEqual(enforcer.fieldNames(), names)
  for (const rule of rules) {
    assert.equal(rule.ruleId, rule.name)
    assert.equal(rule.fieldName, rule.name)
  }
})

test.todo('WHAT[behavior-diagnosis-001] GAP-112 actual live union shares one provider enum and Main index and rejects collisions')
