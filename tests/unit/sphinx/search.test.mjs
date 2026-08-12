import assert from 'node:assert/strict'
import test from 'node:test'
import {
  PriorityQueue,
  createEpistemicState,
  createSearchState,
  graphAstarExpandOrder,
  graphAstarScore,
  reopenOnBeliefShift,
  syncSearchFrontier,
  topFrontierAction,
  stopValue,
  closure,
  anytimeAnswer,
  resumeInquiry,
  continueInquiry,
  rootInformationGain,
} from '../../../src/sphinx/kernel/index.js'

test('priority_queue_orders_max_first', () => {
  const queue = new PriorityQueue((left, right) => left - right)
  for (const value of [3, 9, 1, 7, 5]) queue.push(value)
  assert.deepEqual(queue.toSortedArray(), [9, 7, 5, 3, 1])
})

test('sync_search_frontier_tracks_best_g_and_reopen', () => {
  let state = createEpistemicState('graph path')
  state = {
    ...state,
    R: { primaryForm: 'Why', primaryContract: 'Explanation', formBelief: { Why: 1 }, facets: {} },
    A: [
      {
        id: 'a1',
        kind: 'candidate',
        method: 'Abduction',
        label: 'alpha',
        semanticKey: 'node:alpha',
        cost: 3,
        value: 0.6,
        novelty: 1,
      },
      {
        id: 'a2',
        kind: 'candidate',
        method: 'Abduction',
        label: 'alpha',
        semanticKey: 'node:alpha',
        cost: 1,
        value: 0.55,
        novelty: 1,
      },
    ],
    search: { ...createSearchState(), closed: { 'node:alpha': true } },
  }
  state = syncSearchFrontier(state)
  assert.equal(state.search.bestG['node:alpha'], 1)
  assert.equal(state.search.reopenCount, 1)
  assert.equal(state.search.closed['node:alpha'], undefined)
  assert.ok(state.search.frontier.length >= 1)
})

test('reopen_on_belief_shift_clears_closed', () => {
  let state = createEpistemicState('belief shift')
  state = {
    ...state,
    B: { ...state.B, evidenceMass: 0.4 },
    search: { ...createSearchState(), closed: { 'node:x': true }, reopenCount: 0 },
  }
  state = reopenOnBeliefShift(state, 0.1)
  assert.deepEqual(state.search.closed, {})
  assert.equal(state.search.reopenCount, 1)
})

test('graph_astar_embedding_orders_by_g_plus_h', () => {
  const actions = [
    { id: 'a', semanticKey: 'n1', cost: 2, heuristic: 3 },
    { id: 'b', semanticKey: 'n2', cost: 1, heuristic: 4 },
    { id: 'c', semanticKey: 'n3', cost: 1, heuristic: 2 },
  ]
  const ordered = graphAstarExpandOrder(
    actions,
    (action) => action.cost,
    (action) => action.heuristic,
  )
  assert.equal(ordered[0].id, 'c')
  assert.deepEqual(new Set(ordered.map((action) => action.id)), new Set(['a', 'b', 'c']))
  assert.equal(graphAstarScore(actions[2], createEpistemicState('q'), 2), 3)
})

test('yield_includes_anytime_answer_after_candidates', () => {
  let { state, result } = resumeInquiry(createEpistemicState('why blue sky?'), {
    type: 'SemanticAssessment',
    forms: { Why: 1 },
    facets: { explanatory: 1 },
  })
  ;({ state, result } = resumeInquiry(state, {
    type: 'Candidates',
    items: [{ method: 'Abduction', text: 'Rayleigh', semanticKey: 'abd:rayleigh' }],
  }))
  assert.equal(result.status, 'yield')
  assert.ok(result.bestAnswer)
  assert.equal(result.bestAnswer.stopReason, 'anytime')
  assert.ok(result.bestAnswer.strands.length >= 1)
})

test('closure_reopen_after_semantic_assessment_mass_shift', () => {
  let state = createEpistemicState('mass shift')
  state = {
    ...state,
    B: { ...state.B, evidenceMass: 0.5 },
    search: { ...createSearchState(), closed: { 'k:1': true }, reopenCount: 0 },
  }
  state = closure(
    state,
    {
      type: 'SemanticAssessment',
      forms: { Why: 1 },
      facets: { explanatory: 1 },
    },
    { exogenous: true },
  )
  assert.equal(state.search.closed['k:1'], undefined)
  assert.ok(state.search.reopenCount >= 1)
})

test('root_information_gain_prefers_why_abduction', () => {
  const state = {
    ...createEpistemicState('why?'),
    R: { primaryForm: 'Why', primaryContract: 'Explanation', formBelief: { Why: 1 }, facets: {} },
    B: { evidenceMass: 0, hypotheses: [], formBelief: null, facets: null },
  }
  const gain = rootInformationGain(
    { method: 'Abduction', kind: 'candidate', novelty: 1, cost: 1 },
    state,
  )
  assert.ok(gain > 0.5)
})

test('expand_frontier_when_head_beats_stop', () => {
  let state = createEpistemicState('expand?')
  state = {
    ...state,
    R: {
      primaryForm: 'Why',
      primaryContract: 'Explanation',
      formBelief: { Why: 1 },
      facets: { explanatory: 1 },
    },
    B: { ...state.B, evidenceMass: 0.05 },
    activatedMethods: ['Abduction', 'Analogy', 'Synthesis'],
    A: [
      {
        id: 'valued',
        kind: 'candidate',
        method: 'Abduction',
        label: 'low',
        semanticKey: 'abd:low',
        cost: 1,
        llmValue: 0.15,
        value: 0.15,
        novelty: 1,
      },
      {
        id: 'expand',
        kind: 'candidate',
        method: 'Analogy',
        label: 'high',
        semanticKey: 'ana:high',
        cost: 1,
        value: 0.85,
        novelty: 1,
      },
    ],
    E: [
      { type: 'SemanticAssessment', semanticKey: 'sa', exogenous: true },
      { type: 'Candidates', semanticKey: 'c', exogenous: true },
      { type: 'ValueEstimates', semanticKey: 'v', exogenous: true },
    ],
  }
  state = syncSearchFrontier(state)
  assert.ok(topFrontierAction(state).f > stopValue(state))
  const { result } = continueInquiry(state)
  assert.equal(result.status, 'yield')
  assert.equal(result.request.type, 'ExpandFrontierRequest')
})
