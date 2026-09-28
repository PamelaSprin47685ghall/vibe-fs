import assert from 'node:assert/strict'
import test from 'node:test'
import * as enforcer from '../../../dist/Enforcer/Surface.js'

const rule = (name, lexicalOrder) => ({
  name, ruleId: name, fieldName: name, lexicalOrder,
  enforcerText: 'Detection', mainText: 'Guidance',
})
const rules = [rule('cat', 2), rule('cut', 1), rule('long-name', 3)]

test('WHAT[behavior-diagnosis-007] exact match wins and whitespace is trimmed', () => {
  assert.deepEqual(enforcer.resolveField(' cat ', rules), rules[0])
})

test('WHAT[behavior-diagnosis-007] insert delete and substitute ties use lexical order rather than input order', () => {
  for (const input of ['cot', 'ct', 'caut']) {
    assert.deepEqual(enforcer.resolveField(input, rules), rules[1])
    assert.deepEqual(enforcer.resolveField(input, [...rules].reverse()), rules[1])
  }
  assert.deepEqual(enforcer.resolveField('coat', rules), rules[0])
})

test('WHAT[behavior-diagnosis-007] distant nonempty input still resolves to one packaged identity', () => {
  const result = enforcer.decodeCall({ entry: 'work', tip: 'completely-unknown-name' })
  assert.equal(result.ok, true)
  const rule = enforcer.tryFindByField(result.value.tip.fieldName)
  assert.ok(rule)
  assert.equal(result.value.tip.ruleId, rule.ruleId)
})
