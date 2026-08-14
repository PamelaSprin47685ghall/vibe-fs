import assert from 'node:assert/strict'
import test from 'node:test'
import { caseOf, listItems, payloadOf, toList } from '../../verification-system/tests/support/domain.mjs'

import {
  FissionPrompt_parse as parsePrompt,
  FissionCompletionAffinity,
  FissionCompletionAffinityModule_lane as affinityLane,
  FissionCompletionRouting_targets as completionTargets,
  FissionDeliveryModule_empty as deliveryEmpty,
  FissionDeliveryModule_mark as deliveryMark,
  FissionDeliveryModule_pendingTargets as deliveryPendingTargets,
  FissionWorkBundleModule_empty as workBundleEmpty,
  FissionWorkBundleModule_add as workBundleAdd,
  FissionWorkBundleModule_merge as workBundleMerge,
  FissionWorkBundleModule_keys as workBundleKeys,
  FissionWorkBundleModule_entries as workBundleEntries,
  FissionConvergence_ready as convergenceReady,
} from '../../../dist/Execution/Fission/Model.js'

const parse = (text) => parsePrompt(text)
const lanePrompts = (parsed) => listItems(parsed.Lanes).map((lane) => [lane.Index, lane.Prompt])

const mustOk = (result) => {
  assert.equal(caseOf(result), 'Ok')
  return payloadOf(result)
}

test('canonical parser normalizes only newline shape and preserves lane text', () => {
  const parsed = mustOk(parse('  A  \r\nB\r\n'))
  assert.deepEqual(lanePrompts(parsed), [[0, '  A  '], [1, 'B']])
  assert.equal(parsed.Count, 2)

  const internalBlank = parse('A\n   \nC')
  assert.equal(caseOf(internalBlank), 'Error')
  const reason = payloadOf(internalBlank)
  assert.equal(caseOf(reason), 'EmptyLanePrompt')
  assert.equal(payloadOf(reason), 1)

  assert.equal(caseOf(parse('A')), 'Error')
  assert.equal(caseOf(payloadOf(parse('A'))), 'TooFewLanes')
})

test('pre-fission completion broadcasts to every lane; post-fission completion has one affinity target', () => {
  const preFissionAffinity = new FissionCompletionAffinity(0, [])
  assert.deepEqual(listItems(completionTargets(4, preFissionAffinity)), [0, 1, 2, 3])
  assert.deepEqual(listItems(completionTargets(4, affinityLane(2))), [2])

  let delivery = deliveryEmpty(3)
  delivery = mustOk(deliveryMark('child-A', 0, delivery))
  delivery = mustOk(deliveryMark('child-A', 0, delivery)) // idempotent
  delivery = mustOk(deliveryMark('child-A', 2, delivery))
  assert.deepEqual(listItems(deliveryPendingTargets('child-A', delivery)), [1])
})

test('keyed work bundle is idempotent and rejects conflicting records for one lane', () => {
  const empty = workBundleEmpty
  const a = mustOk(workBundleAdd(2, 'ref-c', empty))
  const b = mustOk(workBundleAdd(0, 'ref-a', a))
  const same = mustOk(workBundleAdd(0, 'ref-a', b))
  assert.deepEqual(listItems(workBundleKeys(same)), [0, 2])

  const conflict = workBundleAdd(0, 'ref-other', same)
  assert.equal(caseOf(conflict), 'Error')
  assert.equal(caseOf(payloadOf(conflict)), 'ConflictingLaneRecord')

  const left = mustOk(workBundleAdd(1, 'ref-b', b))
  const right = mustOk(workBundleAdd(1, 'ref-b', mustOk(workBundleAdd(0, 'ref-a', empty))))
  const merged1 = mustOk(workBundleMerge(left, right))
  const merged2 = mustOk(workBundleMerge(right, left))
  assert.deepEqual(listItems(workBundleEntries(merged1)), listItems(workBundleEntries(merged2)))
})

test('convergence requires all lane records and all completion deliveries', () => {
  const bundle = [0, 1, 2].reduce((state, lane) => mustOk(workBundleAdd(lane, `ref-${lane}`, state)), workBundleEmpty)
  let delivery = deliveryEmpty(3)
  for (const lane of [0, 1, 2]) delivery = mustOk(deliveryMark('pre-child', lane, delivery))

  assert.equal(convergenceReady(3, toList(['pre-child']), bundle, delivery), true)
  const incomplete = mustOk(workBundleAdd(0, 'ref-0', workBundleEmpty))
  assert.equal(convergenceReady(3, toList(['pre-child']), incomplete, delivery), false)
})
