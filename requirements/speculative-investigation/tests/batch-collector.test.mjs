import assert from 'node:assert/strict'
import test from 'node:test'
import * as Strength from '../../../dist/Strength/Surface.js'

const call = (callId, name, args) => ({ kind: 'tool-call', callId, name, args })
const result = (callId, resultText) => ({ kind: 'tool-result', callId, result: resultText })
const text = (textValue) => ({ kind: 'text', text: textValue })
const msg = (role, parts) => ({ role, parts })

test('WHAT[SPEC-INV-003] STRENGTH_003_005_collector_preserves_provider_request_batches_and_concurrent_order', () => {
  const batches = Strength.collectCompleteBatches([
    msg('user', [text('root')]),
    msg('assistant', [call('c1', 'read', '{"a":1}'), call('c2', 'grep', '{"b":2}')]),
    msg('tool', [result('c2', 'two'), result('c1', 'one')]),
    msg('assistant', [call('c3', 'glob', '{}')]),
    msg('tool', [result('c3', 'three')]),
  ])
  assert.equal(batches.length, 2)
  assert.equal(batches[0].requestOrdinal, 1)
  assert.deepEqual(batches[0].exchanges.map((exchange) => exchange.toolName), ['read', 'grep'])
  assert.deepEqual(batches[0].exchanges.map((exchange) => exchange.canonicalResult), ['one', 'two'])
  assert.equal(batches[1].requestOrdinal, 2)
})

test('WHAT[SPEC-INV-003] STRENGTH_005_incomplete_batch_and_results_after_next_provider_message_are_not_collected', () => {
  assert.deepEqual(Strength.collectCompleteBatches([
    msg('assistant', [call('c1', 'read', '{}'), call('c2', 'grep', '{}')]),
    msg('tool', [result('c1', 'one')]),
  ]), [])
  assert.deepEqual(Strength.collectCompleteBatches([
    msg('assistant', [call('c1', 'read', '{}')]),
    msg('assistant', [text('next provider output')]),
    msg('tool', [result('c1', 'late')]),
  ]), [])
})
