import assert from 'node:assert/strict'
import test from 'node:test'
import { parse } from 'smol-toml'
import { clear, clearAll, delivered, plan, record } from '../../../dist/Execution/Delegation/Advisor/Surface.js'

const material = (...anchors) => anchors.map((anchor) => ({ anchor, text: 'material-' + anchor }))
const texts = (result) => (parse(result.viewPrompt).new_work_to_record ?? []).map((row) => row.record)
test.beforeEach(() => clearAll())

test('WHAT[advisor-003] follow-ups filter only successfully delivered material', () => {
  const first = plan('en', [], material(1, 2, 3))
  record('caller', first.deliveredMaterial)
  const second = plan('en', delivered('caller'), material(3, 4, 1, 5))
  assert.deepEqual(texts(second), ['material-4', 'material-5'])
  record('caller', second.deliveredMaterial)
  const third = plan('en', delivered('caller'), material(1, 2, 3, 4, 5))
  assert.deepEqual(texts(third), [])
  assert.equal(third.hasNewMaterial, false)
})

test('WHAT[advisor-003] changed content and literals retain their exact material identities', () => {
  const initial = plan('en', [], [{ anchor: 1, text: 'same' }, { anchor: null, text: 'old\nreport' }])
  record('caller', initial.deliveredMaterial)
  const result = plan('en', delivered('caller'), [
    { anchor: 1, text: 'same' },
    { anchor: null, text: 'same' },
    { anchor: 1, text: 'changed' },
    { anchor: null, text: 'old\nreport ' },
  ])
  assert.deepEqual(texts(result), ['same', 'changed', 'old\nreport '])
})

test('WHAT[advisor-003] failed or unrecorded consultation does not consume its proposed delta', () => {
  const pending = material(2, 1, 2)
  const first = plan('en', delivered('caller'), pending)
  const retry = plan('en', delivered('caller'), pending)
  assert.deepEqual(texts(retry), ['material-2', 'material-1', 'material-2'])
  assert.deepEqual(texts(retry), texts(first))
  assert.deepEqual(delivered('caller'), [])
})

test('WHAT[advisor-003] cursors are isolated, monotone, and cleared per caller', () => {
  const first = plan('en', [], material(1))
  const second = plan('en', [], material(2))
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

