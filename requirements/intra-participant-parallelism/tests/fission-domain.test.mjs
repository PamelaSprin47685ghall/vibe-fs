import assert from 'node:assert/strict'
import test from 'node:test'
import { caseOf, listItems, payloadOf, toList } from '../../verification-system/tests/support/domain.mjs'

const Fission = await import('../../../dist/Domain/Fission.js')

const parse = (text) => Fission.FissionPrompt_parse(text)
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
  assert.deepEqual(listItems(Fission.FissionCompletionRouting_targets(4, Fission.FissionCompletionAffinity_preFission)), [0, 1, 2, 3])
  assert.deepEqual(listItems(Fission.FissionCompletionRouting_targets(4, Fission.FissionCompletionAffinity_lane(2))), [2])

  let delivery = Fission.FissionDelivery_empty(3)
  delivery = mustOk(Fission.FissionDelivery_mark('child-A', 0, delivery))
  delivery = mustOk(Fission.FissionDelivery_mark('child-A', 0, delivery)) // idempotent
  delivery = mustOk(Fission.FissionDelivery_mark('child-A', 2, delivery))
  assert.deepEqual(listItems(Fission.FissionDelivery_pendingTargets('child-A', delivery)), [1])
})

test('keyed work bundle is idempotent and rejects conflicting records for one lane', () => {
  const empty = Fission.FissionWorkBundle_empty
  const a = mustOk(Fission.FissionWorkBundle_add(2, 'ref-c', empty))
  const b = mustOk(Fission.FissionWorkBundle_add(0, 'ref-a', a))
  const same = mustOk(Fission.FissionWorkBundle_add(0, 'ref-a', b))
  assert.deepEqual(listItems(Fission.FissionWorkBundle_keys(same)), [0, 2])

  const conflict = Fission.FissionWorkBundle_add(0, 'ref-other', same)
  assert.equal(caseOf(conflict), 'Error')
  assert.equal(caseOf(payloadOf(conflict)), 'ConflictingLaneRecord')

  const left = mustOk(Fission.FissionWorkBundle_add(1, 'ref-b', b))
  const right = mustOk(Fission.FissionWorkBundle_add(1, 'ref-b', mustOk(Fission.FissionWorkBundle_add(0, 'ref-a', empty))))
  const merged1 = mustOk(Fission.FissionWorkBundle_merge(left, right))
  const merged2 = mustOk(Fission.FissionWorkBundle_merge(right, left))
  assert.deepEqual(listItems(Fission.FissionWorkBundle_entries(merged1)), listItems(Fission.FissionWorkBundle_entries(merged2)))
})

test('convergence requires all lane records and all completion deliveries', () => {
  const bundle = [0, 1, 2].reduce((state, lane) => mustOk(Fission.FissionWorkBundle_add(lane, `ref-${lane}`, state)), Fission.FissionWorkBundle_empty)
  let delivery = Fission.FissionDelivery_empty(3)
  for (const lane of [0, 1, 2]) delivery = mustOk(Fission.FissionDelivery_mark('pre-child', lane, delivery))

  assert.equal(Fission.FissionConvergence_ready(3, toList(['pre-child']), bundle, delivery), true)
  const incomplete = mustOk(Fission.FissionWorkBundle_add(0, 'ref-0', Fission.FissionWorkBundle_empty))
  assert.equal(Fission.FissionConvergence_ready(3, toList(['pre-child']), incomplete, delivery), false)
})
