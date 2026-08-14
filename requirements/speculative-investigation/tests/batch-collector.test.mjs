import assert from 'node:assert/strict'
import test from 'node:test'

import * as Collector from '../../../dist/Domain/StrengthBatchCollector.js'
import * as Provider from '../../../dist/Domain/ProviderProjection.js'
import * as Id from '../../../dist/Kernel/Identity.js'
import { toList, listItems } from '../../verification-system/tests/support/domain.mjs'

const call = (id, name, args) => new Provider.WirePart(2, [Id.ToolCallIdModule_create(id), name, args])
const result = (id, body) => new Provider.WirePart(3, [Id.ToolCallIdModule_create(id), body])
const text = (body) => new Provider.WirePart(0, [body])
const msg = (role, parts) => ({ Role: role, Parts: toList(parts) })

test('STRENGTH_003_005_collector_preserves_provider_request_batches_and_concurrent_order', () => {
  const messages = toList([
    msg('user', [text('root')]),
    msg('assistant', [call('c1', 'read', '{"a":1}'), call('c2', 'grep', '{"b":2}')]),
    msg('tool', [result('c2', 'two'), result('c1', 'one')]),
    msg('assistant', [call('c3', 'glob', '{}')]),
    msg('tool', [result('c3', 'three')]),
  ])

  const batches = listItems(Collector.collectCompleteBatches(messages))
  assert.equal(batches.length, 2)
  assert.equal(batches[0].RequestOrdinal, 1)
  assert.deepEqual(listItems(batches[0].Exchanges).map((x) => x.ToolName), ['read', 'grep'])
  assert.deepEqual(listItems(batches[0].Exchanges).map((x) => x.CanonicalResult), ['one', 'two'])
  assert.equal(batches[1].RequestOrdinal, 2)
})

test('STRENGTH_005_incomplete_batch_and_results_after_next_provider_message_are_not_collected', () => {
  const incomplete = toList([
    msg('assistant', [call('c1', 'read', '{}'), call('c2', 'grep', '{}')]),
    msg('tool', [result('c1', 'one')]),
  ])
  assert.equal(listItems(Collector.collectCompleteBatches(incomplete)).length, 0)

  const crossedBoundary = toList([
    msg('assistant', [call('c1', 'read', '{}')]),
    msg('assistant', [text('next provider output')]),
    msg('tool', [result('c1', 'late')]),
  ])
  assert.equal(listItems(Collector.collectCompleteBatches(crossedBoundary)).length, 0)
})
