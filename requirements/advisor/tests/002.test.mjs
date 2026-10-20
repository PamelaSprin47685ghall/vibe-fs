import assert from 'node:assert/strict'
import test from 'node:test'
import { parse } from 'smol-toml'
import { plan } from '../../../dist/Execution/Delegation/Advisor/Surface.js'

const messages = [1, 2, 3].map((anchor) => ({ anchor, text: 'material-' + anchor }))
const request = (prefixUntil, thenMap, history = []) => ({ messages, declaration: { prefixUntil, thenMap }, history })
const records = (result) => (parse(result.viewPrompt).new_work_to_record ?? []).map((row) => row.record)

test('WHAT[advisor-002] first consult delivers the complete generated view, not a prefixUntil window', () => {
  for (const prefixUntil of [0, 2, 3, 99]) {
    const result = plan('en', [], request(prefixUntil, {}))
    assert.deepEqual(records(result), ['material-1', 'material-2', 'material-3'])
    assert.deepEqual(result.deliveredAnchors, [1, 2, 3])
    assert.equal(result.hasNewMaterial, true)
    assert.equal(result.viewPrompt.startsWith('# '), true)
    assert.equal(result.viewPrompt.endsWith('\n'), true)
  }
})

test('WHAT[advisor-002] shared view protects prefix, accepts null and missing keys, and summarizes at the boundary', () => {
  const result = plan('en', [], request(2, { 1: 'forbidden', 2: 'summary-two', 3: null }))
  assert.deepEqual(records(result), ['material-1', 'summary-two', 'material-3'])
  assert.deepEqual(result.deliveredAnchors, [1, 2, 3])
})

test('WHAT[advisor-002] prior compression remains and a later replacement covers the latest interval', () => {
  const history = [{ anchor: 2, until: 3, summary: 'old-summary-two' }]
  const preserved = plan('en', [], request(99, { 2: 'forbidden' }, history))
  assert.deepEqual(records(preserved), ['material-1', 'old-summary-two', 'material-3'])
  const replaced = plan('en', [], request(1, { 1: 'one-and-two', 3: null }, history))
  assert.deepEqual(records(replaced), ['one-and-two', 'material-3'])
  assert.deepEqual(replaced.deliveredAnchors, [1, 3])
})

test('WHAT[advisor-002] implicit zero remains the opening representative', () => {
  const history = [{ anchor: 1, until: 2, summary: 'old-summary-one' }]
  const result = plan('zh-CN', [], request(0, { 0: 'opening', 2: null, 3: null }, history))
  assert.deepEqual(records(result), ['opening', 'material-2', 'material-3'])
  assert.deepEqual(result.deliveredAnchors, [0, 2, 3])
})

test('WHAT[advisor-002] shape errors use the shared view validator', () => {
  assert.throws(() => plan('en', [], { messages, declaration: { prefixUntil: 1 } }), /VIEW_DECLARATION_INVALID/)
  assert.throws(() => plan('en', [], request(1, { 2: false })), /VIEW_DECLARATION_INVALID/)
})
