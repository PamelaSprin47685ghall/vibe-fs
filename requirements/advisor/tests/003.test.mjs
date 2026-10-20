import assert from 'node:assert/strict'
import test from 'node:test'
import { parse } from 'smol-toml'
import { clear, clearAll, delivered, plan, record } from '../../../dist/Execution/Delegation/Advisor/Surface.js'

const request = (anchors, prefixUntil, thenMap = {}, history = []) => ({
  messages: anchors.map((anchor) => ({ anchor, text: 'material-' + anchor })),
  declaration: { prefixUntil, thenMap }, history,
})
const texts = (result) => (parse(result.viewPrompt).new_work_to_record ?? []).map((row) => row.record)
test.beforeEach(() => clearAll())

test('WHAT[advisor-003] follow-ups use successfully delivered material rather than a declaration boundary', () => {
  const first = plan('en', [], request([1, 2, 3], 0))
  record('caller', first.deliveredMaterial)
  const second = plan('en', delivered('caller'), request([1, 2, 3, 4, 5], 99))
  assert.deepEqual(texts(second), ['material-4', 'material-5'])
  record('caller', second.deliveredMaterial)
  const third = plan('en', delivered('caller'), request([1, 2, 3, 4, 5], 0))
  assert.deepEqual(texts(third), [])
  assert.equal(third.hasNewMaterial, false)
  assert.deepEqual(third.deliveredMaterial, [])
})

test('WHAT[advisor-003] view generation folds intervals before delta and delivers a new representative summary', () => {
  const initial = plan('en', [], request([1, 2, 3], 0))
  record('caller', initial.deliveredMaterial)
  const history = [{ anchor: 2, until: 3, summary: 'old-summary-two' }]
  const result = plan('en', delivered('caller'), request([1, 2, 3], 1, { 1: 'one-and-two', 3: null }, history))
  assert.deepEqual(texts(result), ['one-and-two'])
  assert.equal(result.viewPrompt.includes('old-summary-two'), false)
  record('caller', result.deliveredMaterial)
  assert.deepEqual(texts(plan('en', delivered('caller'), request([1, 2, 3], 1, { 1: 'one-and-two' }, history))), [])
})

test('WHAT[advisor-003] an unrecorded or failed consultation does not consume its proposed delta', () => {
  const pending = request([1, 2], 0)
  const first = plan('en', delivered('caller'), pending)
  const retry = plan('en', delivered('caller'), pending)
  assert.deepEqual(texts(retry), texts(first))
  assert.deepEqual(delivered('caller'), [])
})

test('WHAT[advisor-003] delivery cursors are isolated, monotone, and cleared per caller', () => {
  const first = plan('en', [], request([1], 0))
  const second = plan('en', [], request([2], 0))
  record('caller', first.deliveredMaterial)
  record('caller', first.deliveredMaterial)
  record('other', second.deliveredMaterial)
  assert.equal(delivered('caller').length, 1)
  assert.equal(delivered('other').length, 1)
  assert.notDeepEqual(delivered('caller'), delivered('other'))
  clear('caller')
  assert.deepEqual(delivered('caller'), [])
  assert.equal(delivered('other').length, 1)
  clearAll()
  assert.deepEqual(delivered('other'), [])
})

test('WHAT[advisor-003] permanent anchor zero delivers changed material once and leaves caller state untouched', () => {
  const firstRequest = request([1, 2], 0, { 0: 'first-opening', 1: null, 2: null })
  const original = JSON.stringify(firstRequest)
  const first = plan('en', [], firstRequest)
  record('caller', first.deliveredMaterial)
  assert.equal(JSON.stringify(firstRequest), original)
  const history = [{ anchor: 0, until: 1, summary: 'first-opening' }]
  const secondRequest = request([1, 2], 0, { 0: 'second-opening', 1: null, 2: null }, history)
  const second = plan('en', delivered('caller'), secondRequest)
  assert.deepEqual(texts(second), ['second-opening'])
  record('caller', second.deliveredMaterial)
  assert.deepEqual(texts(plan('en', delivered('caller'), secondRequest)), [])
})
