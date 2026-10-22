import assert from 'node:assert/strict'
import test from 'node:test'
import { parse } from 'smol-toml'
import { plan } from '../../../dist/Execution/Delegation/Advisor/Surface.js'

const records = (result) => (parse(result.viewPrompt).new_work_to_record ?? []).map((row) => row.record)
const piece = (anchor, text) => ({ anchor, text })

test('WHAT[advisor-002] empty carry sends no history while the independent intent may still be consulted', () => {
  const result = plan('en', [], [])
  assert.deepEqual(records(result), [])
  assert.deepEqual(result.deliveredAnchors, [])
  assert.equal(result.hasNewMaterial, false)
  assert.equal(result.viewPrompt.startsWith('# '), true)
})

test('WHAT[advisor-002] resolved carry keeps exact order, duplicates and literal whitespace', () => {
  const material = [piece(3, 'third'), piece(null, ''), piece(null, '  '), piece(1, 'first'), piece(3, 'third')]
  const before = structuredClone(material)
  const result = plan('en', [], material)
  assert.deepEqual(records(result), ['third', '', '  ', 'first', 'third'])
  assert.deepEqual(result.deliveredAnchors, [3, 1])
  assert.deepEqual(material, before)
})

test('WHAT[advisor-002] multiline materials are real TOML fields with exact round trips', () => {
  const texts = ['first\nsecond', 'leading\n', '\ntrailing  ', 'quote """ and slash \\']
  const result = plan('zh-CN', [], texts.map((text) => piece(null, text)))
  assert.deepEqual(records(result), texts)
  assert.match(result.viewPrompt, /record = """\nfirst\nsecond/)
  assert.doesNotMatch(result.viewPrompt, /first\\nsecond/)
})

test('WHAT[advisor-002] malformed resolved material is refused rather than coerced', () => {
  for (const material of [[piece(-1, 'bad')], [piece(1.5, 'bad')], [piece('1', 'bad')], [piece(1, false)], [null]]) {
    assert.throws(() => plan('en', [], material), /malformed/)
  }
})

