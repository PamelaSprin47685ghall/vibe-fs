import assert from 'node:assert/strict'
import test from 'node:test'
import * as enforcer from '../../../dist/Enforcer/Surface.js'

test('WHAT[behavior-diagnosis-018] packaged enum decoding and Main lookup agree for the current resource view', () => {
  const rules = enforcer.rules()
  assert.deepEqual(enforcer.fieldNames(), rules.map((rule) => rule.fieldName))
  for (const rule of rules) {
    assert.deepEqual(enforcer.tryFindByField(rule.fieldName), rule)
    const decoded = enforcer.decodeCall({
      charge: 'Resolve one concrete uncertainty.',
      occurrence: 'A meaningful transition happened.',
      settlement: 'The new state is established.',
      consequence: 'The future path changed.',
      tip: rule.fieldName,
    })
    assert.equal(decoded.ok, true)
    assert.equal(decoded.value.tip.ruleId, rule.ruleId)
  }
})

test.todo('WHAT[behavior-diagnosis-018] GAP-112 a real BIRTH during an active life changes none of its four frozen views and appears in the next fresh life')
