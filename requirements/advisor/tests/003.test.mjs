import assert from 'node:assert/strict'
import test from 'node:test'
import { clear, clearAll, delivered, plan, record } from '../../../dist/Execution/Delegation/Advisor/Surface.js'

const request = (anchors, thenMap = {}) => ({
  messages: anchors.map((anchor) => ({ anchor, text: 'material-' + anchor })),
  declaration: { prefixUntil: 0, thenMap },
})

test('WHAT[advisor-003] follow-up delivery filters exact material after generating the full view', () => {
  const first = plan('en', [], request([1, 2, 3]))
  const second = plan('en', first.deliveredMaterial, request([1, 2, 3, 4]))
  assert.equal(second.hasNewMaterial, true)
  assert.match(second.viewPrompt, /material-4/)
  assert.doesNotMatch(second.viewPrompt, /material-1|material-2|material-3/)
  const cursor = [...first.deliveredMaterial, ...second.deliveredMaterial]
  assert.equal(plan('en', cursor, request([1, 2, 3, 4])).hasNewMaterial, false)
})

test('WHAT[advisor-003] a new summary for previously delivered material is delivered once', () => {
  const first = plan('en', [], request([1, 2, 3]))
  const summary = plan('en', first.deliveredMaterial, request([1, 2, 3], { 1: 'summary-one' }))
  assert.match(summary.viewPrompt, /summary-one/)
  assert.doesNotMatch(summary.viewPrompt, /material-1|material-2|material-3/)
  const cursor = [...first.deliveredMaterial, ...summary.deliveredMaterial]
  assert.equal(plan('en', cursor, request([1, 2, 3], { 1: 'summary-one' })).hasNewMaterial, false)
  assert.equal(plan('en', cursor, request([1, 2, 3], { 1: 'summary-one-changed' })).hasNewMaterial, true)
})

test('WHAT[advisor-003] material cursors grow per caller and clear independently', () => {
  try {
    record('caller', ['3:one', '1:two'])
    record('caller', ['3:one'])
    record('other', ['different'])
    assert.deepEqual(delivered('caller'), ['1:two', '3:one'])
    clear('caller')
    assert.deepEqual(delivered('caller'), [])
    assert.deepEqual(delivered('other'), ['different'])
  } finally {
    clearAll()
  }
})
