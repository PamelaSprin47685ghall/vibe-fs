import assert from 'node:assert/strict'
import test from 'node:test'
import * as enforcer from '../../../dist/Enforcer/Surface.js'

const sections = (overrides = {}) => ({
  charge: 'Resolve one concrete uncertainty.',
  occurrence: 'A meaningful transition happened.',
  settlement: 'The new state is established.',
  consequence: 'The future path changed.',
  whatToDoNow: 'Take the next concrete step on that fact.',
  whyThisMatters: 'Later work rests on the settled uncertainty.',
  repairStrategy: 'Restore the rule at its owner rather than downstream.',
  decisionBranches: 'If the evidence holds, proceed; otherwise stop.',
  commonWrongFixes: 'Do not paper over the symptom downstream.',
  verification: 'Re-run the failing check after the repair.',
  doneWhen: 'The check is green and the boundary holds again.',
  ...overrides,
})

test('WHAT[behavior-diagnosis-018] free tipName decoding and catalog identity agree for the current resource view', () => {
  const rules = enforcer.rules()
  assert.deepEqual(enforcer.fieldNames(), rules.map((rule) => rule.fieldName))
  for (const rule of rules) {
    assert.deepEqual(enforcer.tryFindByField(rule.fieldName), rule)
    const decoded = enforcer.decodeCall(sections({ tipName: rule.fieldName }))
    assert.equal(decoded.ok, true)
    assert.equal(decoded.value.tipName, rule.fieldName)
  }

  const missing = enforcer.decodeCall(sections())
  assert.equal(missing.ok, false)
  assert.equal(missing.error, 'missing-tip-name')

  const legacy = enforcer.decodeCall(sections({ tip: rules[0].fieldName }))
  assert.equal(legacy.ok, false)
  assert.equal(legacy.error, 'missing-tip-name')
})

test.todo('WHAT[behavior-diagnosis-018] GAP-112 a real BIRTH during an active life changes none of its four frozen views and appears in the next fresh life')
