import assert from 'node:assert/strict'
import test from 'node:test'

import { paretoFrontier, createStore, start, resume, state, assessWhy, fsharpList } from './support.mjs'

test('WHAT[EPI-011] wire_equivalence_hint_cannot_force_kernel_merge', () => {
  const store = createStore()
  const started = start(store, '为什么会这样？')
  assessWhy(store, started.handle)

  resume(store, started.handle, {
    type: 'Candidates',
    items: [
      {
        method: 'Abduction',
        question: '较贵表示',
        semanticKey: 'candidate:expensive',
        equivalenceKey: 'same-future-decision',
        expectedRootGain: 0.9,
        cost: 0.4,
      },
      {
        method: 'Abduction',
        question: '较便宜表示',
        semanticKey: 'candidate:cheap',
        equivalenceKey: 'same-future-decision',
        expectedRootGain: 0.9,
        cost: 0.2,
      },
    ],
  })

  const actions = [...state(store, started.handle).Actions].map(([, action]) => action)
  assert.equal(actions.length, 2)
  assert.ok(actions.every((action) => action.EquivalenceKey === undefined))
})

test('WHAT[EPI-011] same_kernel_identity_merges_candidate_provenance_instead_of_erasing_it', () => {
  const store = createStore()
  const started = start(store, '为什么会这样？')
  assessWhy(store, started.handle)

  resume(store, started.handle, {
    type: 'Candidates',
    items: [
      {
        method: 'Abduction',
        question: '同一个规范问题',
        semanticKey: 'question:shared',
        dependencyKey: 'source:shared',
        expectedRootGain: 0.7,
        cost: 0.2,
        provenance: ['generator:abduction'],
      },
      {
        method: 'Counterexample',
        question: '同一个规范问题',
        semanticKey: 'question:shared',
        dependencyKey: 'source:shared',
        expectedRootGain: 0.8,
        cost: 0.2,
        provenance: ['generator:counterexample'],
      },
    ],
  })

  const actions = [...state(store, started.handle).Actions].map(([, action]) => action)
  assert.equal(actions.length, 1)
  assert.deepEqual([...actions[0].Provenance].sort(), ['generator:abduction', 'generator:counterexample'])
})

test('WHAT[EPI-011] same_question_from_independent_dependency_groups_is_not_false_deduplicated', () => {
  const store = createStore()
  const started = start(store, '为什么会这样？')
  assessWhy(store, started.handle)

  resume(store, started.handle, {
    type: 'Candidates',
    items: [
      {
        method: 'SourceTriangulation',
        question: '独立来源是否支持该命题？',
        semanticKey: 'question:triangulate',
        dependencyKey: 'source:a',
        expectedRootGain: 0.8,
        cost: 0.2,
      },
      {
        method: 'SourceTriangulation',
        question: '独立来源是否支持该命题？',
        semanticKey: 'question:triangulate',
        dependencyKey: 'source:b',
        expectedRootGain: 0.8,
        cost: 0.2,
      },
    ],
  })

  assert.equal(state(store, started.handle).Actions.size, 2)
})

const kernelAction = ({ id, gain, gateway = 0, cost, value, provenance = [] }) => ({
  Id: id,
  ExpectedRootGain: gain,
  GatewayGain: gateway,
  Cost: cost,
  Value: value,
  Provenance: fsharpList(provenance),
})

test('WHAT[EPI-011] kernel_owned_equivalence_class_removes_only_truly_dominated_representation', () => {
  const frontier = paretoFrontier(
    fsharpList([
      kernelAction({ id: 'expensive', gain: 0.9, cost: 0.4, value: 0.5 }),
      kernelAction({ id: 'cheap', gain: 0.9, cost: 0.2, value: 0.7 }),
    ]),
  )

  assert.deepEqual([...frontier].map((action) => action.Id), ['cheap'])
})

test('WHAT[EPI-011] pareto_incomparable_equivalent_representations_both_survive', () => {
  const frontier = paretoFrontier(
    fsharpList([
      kernelAction({ id: 'high', gain: 1, cost: 0.4, value: 0.6 }),
      kernelAction({ id: 'low', gain: 0.7, cost: 0.1, value: 0.6 }),
    ]),
  )

  assert.equal([...frontier].length, 2)
})
