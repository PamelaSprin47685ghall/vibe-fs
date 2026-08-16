import assert from 'node:assert/strict'
import test from 'node:test'

import { createStore, start, resume, state, assessPolar } from './support.mjs'

const preparePolarInvestigation = (store) => {
  const started = start(store, '明天白银会涨吗？')
  assessPolar(store, started.handle)
  const candidate = resume(store, started.handle, {
    type: 'Candidates',
    items: [
      {
        method: 'BaseRate',
        question: '同类市场条件下的次日上涨基准率是多少？',
        semanticKey: 'question:base-rate',
        expectedRootGain: 0.95,
        cost: 0.1,
      },
    ],
  })
  return { handle: started.handle, actionKey: candidate.request.action.id }
}

test('WHAT[EPI-009] bayesian_posterior_requires_explicit_numeric_qualification', () => {
  const store = createStore()
  const { handle, actionKey } = preparePolarInvestigation(store)

  resume(store, handle, {
    type: 'Investigation',
    actionKey,
    hypotheses: [
      { semanticKey: 'up', label: '上涨', prior: 0.5 },
      { semanticKey: 'down', label: '不涨', prior: 0.5 },
    ],
    evidence: [
      {
        semanticKey: 'evidence:qualitative',
        proposition: '分析者认为偏多。',
        source: { id: 'analysis', kind: 'document' },
        dependencyKey: 'analysis',
        likelihoods: { up: 0.9, down: 0.1 },
        numericQualified: false,
      },
    ],
  })

  assert.equal(state(store, handle).Bayesian, undefined)
})

test('WHAT[EPI-009] qualified_independent_evidence_updates_posterior', () => {
  const store = createStore()
  const { handle, actionKey } = preparePolarInvestigation(store)

  const next = resume(store, handle, {
    type: 'Investigation',
    actionKey,
    findings: [
      {
        semanticKey: 'finding:base-rate',
        text: '合格基准证据偏向上涨。',
        evidenceKeys: ['evidence:history'],
      },
    ],
    hypotheses: [
      { semanticKey: 'up', label: '上涨', prior: 0.5 },
      { semanticKey: 'down', label: '不涨', prior: 0.5 },
    ],
    evidence: [
      {
        semanticKey: 'evidence:history',
        proposition: '历史参考类给出 0.7/0.3 的似然。',
        source: { id: 'history', kind: 'dataset' },
        dependencyKey: 'market-history',
        likelihoods: { up: 0.7, down: 0.3 },
        numericQualified: true,
      },
    ],
  })

  assert.equal(next.status, 'yield')
  assert.equal(next.request.type, 'GenerateCandidatesRequest')
  const posterior = Object.fromEntries(state(store, handle).Bayesian.Posterior)
  assert.ok(Math.abs(posterior.up - 0.7) < 1e-12)
  assert.ok(Math.abs(posterior.down - 0.3) < 1e-12)
})

test('WHAT[EPI-009] unqualified_item_cannot_mask_qualified_evidence_from_same_dependency_group', () => {
  const store = createStore()
  const { handle, actionKey } = preparePolarInvestigation(store)

  resume(store, handle, {
    type: 'Investigation',
    actionKey,
    hypotheses: [
      { semanticKey: 'up', label: '上涨', prior: 0.5 },
      { semanticKey: 'down', label: '不涨', prior: 0.5 },
    ],
    evidence: [
      {
        semanticKey: 'evidence:a-unqualified',
        proposition: '同源但没有数值资格。',
        source: { id: 'same-dataset', kind: 'dataset' },
        dependencyKey: 'same-dataset',
        likelihoods: { up: 0.6, down: 0.4 },
        numericQualified: false,
      },
      {
        semanticKey: 'evidence:b-qualified',
        proposition: '同源的合格数值观测。',
        source: { id: 'same-dataset', kind: 'dataset' },
        dependencyKey: 'same-dataset',
        likelihoods: { up: 0.9, down: 0.1 },
        numericQualified: true,
      },
    ],
  })

  const posterior = Object.fromEntries(state(store, handle).Bayesian.Posterior)
  assert.ok(Math.abs(posterior.up - 0.9) < 1e-12)
  assert.ok(Math.abs(posterior.down - 0.1) < 1e-12)
})

test('WHAT[EPI-006] same_semantic_evidence_from_independent_dependency_groups_is_preserved_twice', () => {
  const store = createStore()
  const { handle, actionKey } = preparePolarInvestigation(store)

  resume(store, handle, {
    type: 'Investigation',
    actionKey,
    hypotheses: [
      { semanticKey: 'up', label: '上涨', prior: 0.5 },
      { semanticKey: 'down', label: '不涨', prior: 0.5 },
    ],
    evidence: [
      {
        semanticKey: 'evidence:same-proposition',
        proposition: '独立来源 A 支持上涨。',
        source: { id: 'source-a', kind: 'dataset' },
        dependencyKey: 'source-a',
        likelihoods: { up: 0.8, down: 0.2 },
        numericQualified: true,
      },
      {
        semanticKey: 'evidence:same-proposition',
        proposition: '独立来源 B 支持上涨。',
        source: { id: 'source-b', kind: 'dataset' },
        dependencyKey: 'source-b',
        likelihoods: { up: 0.75, down: 0.25 },
        numericQualified: true,
      },
    ],
  })

  const current = state(store, handle)
  const posterior = Object.fromEntries(current.Bayesian.Posterior)
  assert.equal(current.Evidence.size, 2)
  assert.ok(Math.abs(posterior.up - 12 / 13) < 1e-12)
  assert.ok(Math.abs(posterior.down - 1 / 13) < 1e-12)
})

test('WHAT[EPI-006] same_dependency_group_is_not_counted_as_independent_evidence_twice', () => {
  const store = createStore()
  const { handle, actionKey } = preparePolarInvestigation(store)

  resume(store, handle, {
    type: 'Investigation',
    actionKey,
    hypotheses: [
      { semanticKey: 'up', label: '上涨', prior: 0.5 },
      { semanticKey: 'down', label: '不涨', prior: 0.5 },
    ],
    evidence: [
      {
        semanticKey: 'evidence:a',
        proposition: '同一数据源的第一种汇总。',
        source: { id: 'same-dataset', kind: 'dataset' },
        dependencyKey: 'same-dataset',
        likelihoods: { up: 0.8, down: 0.2 },
        numericQualified: true,
      },
      {
        semanticKey: 'evidence:b',
        proposition: '同一数据源的第二种重述。',
        source: { id: 'same-dataset', kind: 'dataset' },
        dependencyKey: 'same-dataset',
        likelihoods: { up: 0.9, down: 0.1 },
        numericQualified: true,
      },
    ],
  })

  const belief = state(store, handle).Bayesian
  const posterior = Object.fromEntries(belief.Posterior)
  assert.ok(Math.abs(posterior.up - 0.8) < 1e-12)
  assert.ok(Math.abs(posterior.down - 0.2) < 1e-12)
})
