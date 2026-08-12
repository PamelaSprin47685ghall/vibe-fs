import assert from 'node:assert/strict'
import test from 'node:test'

import { createStore, start, resume, state, assessWhy } from './support.mjs'

test('explicit_equivalence_allows_dominated_representative_to_be_removed', () => {
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
        provenance: ['generator:a'],
      },
      {
        method: 'Abduction',
        question: '较便宜表示',
        semanticKey: 'candidate:cheap',
        equivalenceKey: 'same-future-decision',
        expectedRootGain: 0.9,
        cost: 0.2,
        provenance: ['generator:b'],
      },
    ],
  })

  const current = state(store, started.handle)
  assert.equal(current.Actions.size, 1)
  assert.equal([...current.Actions][0][1].SemanticKey, 'candidate:cheap')
})

test('same_question_from_independent_dependency_groups_is_not_false_deduplicated', () => {
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

test('pareto_incomparable_equivalent_representations_both_survive', () => {
  const store = createStore()
  const started = start(store, '为什么会这样？')
  assessWhy(store, started.handle)

  resume(store, started.handle, {
    type: 'Candidates',
    items: [
      {
        method: 'Abduction',
        question: '高收益高成本',
        semanticKey: 'candidate:high',
        equivalenceKey: 'pareto-class',
        expectedRootGain: 1,
        cost: 0.4,
      },
      {
        method: 'Abduction',
        question: '低收益低成本',
        semanticKey: 'candidate:low',
        equivalenceKey: 'pareto-class',
        expectedRootGain: 0.7,
        cost: 0.1,
      },
    ],
  })

  assert.equal(state(store, started.handle).Actions.size, 2)
})
