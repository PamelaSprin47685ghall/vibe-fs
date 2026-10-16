import assert from 'node:assert/strict'
import test from 'node:test'
import * as enforcer from '../../../dist/Enforcer/Surface.js'

// Execution-phase invalid-tip prose is not reachable from the decode surface;
// this file anchors decode-time identity behavior only.
const call = (tipName) => ({
  charge: 'Resolve one concrete uncertainty.',
  occurrence: 'The decisive fact was established.',
  settlement: 'The uncertainty is now settled.',
  consequence: 'The road can proceed on that fact.',
  tipName,
  whatToDoNow: 'Take the next concrete step on that fact.',
  whyThisMatters: 'Later work rests on the settled uncertainty.',
  repairStrategy: 'Restore the rule at its owner rather than downstream.',
  decisionBranches: 'If the evidence holds, proceed; otherwise stop.',
  commonWrongFixes: 'Do not paper over the symptom downstream.',
  verification: 'Re-run the failing check after the repair.',
  doneWhen: 'The check is green and the boundary holds again.',
})

const decodeName = (tipName) => {
  const decoded = enforcer.decodeCall(call(tipName))
  assert.equal(decoded.ok, true)
  return decoded.value.tipName
}

test('WHAT[behavior-diagnosis-007] trimmed free name is kept verbatim instead of resolving to a catalog rule', () => {
  assert.equal(decodeName(' cat '), 'cat')
})

test('WHAT[behavior-diagnosis-007] edit-distance neighbors stay themselves instead of snapping to a catalog name', () => {
  for (const input of ['cot', 'ct', 'caut', 'coat']) {
    assert.equal(decodeName(input), input)
  }
})

test('WHAT[behavior-diagnosis-007] unknown free name decodes without any catalog binding', () => {
  const name = 'completely-unknown-name'
  assert.equal(enforcer.tryFindByField(name), null)
  assert.equal(decodeName(name), name)
})

test('WHAT[behavior-diagnosis-007] overlong and multiline free names are stably rejected', () => {
  const atLimit = enforcer.decodeCall(call('x'.repeat(200)))
  assert.equal(atLimit.ok, true)
  assert.equal(atLimit.value.tipName, 'x'.repeat(200))

  const tooLong = enforcer.decodeCall(call('x'.repeat(201)))
  assert.equal(tooLong.ok, false)
  assert.equal(tooLong.error, 'tip-name-too-long')

  const multiline = enforcer.decodeCall(call('free\nname'))
  assert.equal(multiline.ok, false)
  assert.equal(multiline.error, 'tip-name-multiline')
})
