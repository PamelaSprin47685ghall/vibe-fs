import assert from 'node:assert/strict'
import test from 'node:test'
import * as enforcer from '../../../dist/Enforcer/Surface.js'

const rule = (name, lexicalOrder) => ({
  name, ruleId: name, fieldName: name, lexicalOrder,
  enforcerText: 'Shared wording can describe different contexts.',
  mainText: 'Shared guidance is not a structural conflict.',
})

test('WHAT[behavior-diagnosis-020] identical bodies under distinct identities pass structural validation', () => {
  const rules = [rule('first-context', 1), rule('second-context', 2)]
  const result = enforcer.validate(1, rules)
  assert.equal(result.ok, true)
  assert.deepEqual(result.value, rules)
})
