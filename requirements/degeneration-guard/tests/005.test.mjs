import assert from 'node:assert/strict'
import test from 'node:test'
import { encode } from 'gpt-tokenizer/encoding/o200k_base'
import * as detector from '../../../dist/Execution/Session/LoopDetectorSurface.js'

test('WHAT[degeneration-guard-005] observed token storage grows with distinct vocabulary and stops growing for repeated input', () => {
  const handle = detector.create()
  const text = 'const order = await repository.load("订单");\nreturn { ok: true, revision: 17 };'
  const distinct = new Set(encode(text)).size
  detector.pushText(handle, text)
  assert.equal(detector.trackedTokenCount(handle), distinct)
  for (let index = 0; index < 100; index++) detector.pushText(handle, text)
  assert.equal(detector.trackedTokenCount(handle), distinct)
  assert.ok(detector.trackedTokenCount(handle) <= detector.vocabularySize)
})

test('WHAT[degeneration-guard-005] an empty push leaves score step and token storage unchanged', () => {
  const handle = detector.create()
  const before = detector.evaluate(handle)
  assert.deepEqual(detector.pushText(handle, ''), before)
  assert.equal(detector.trackedTokenCount(handle), 0)
})
