import assert from 'node:assert/strict'
import test from 'node:test'
import { createStore, start, resume, state, assessWhy } from './support.mjs'



test('WHAT[epistemic-reasoning-008] gateway_gain_can_make_low_immediate_gain_question_worth_asking', () => {
  const store = createStore()
  const started = start(store, '复杂问题为什么发生？')
  assessWhy(store, started.handle)

  const result = resume(store, started.handle, {
    type: 'Candidates',
    items: [
      {
        method: 'UnknownExpansion',
        question: '哪个隐藏变量会打开后续判别问题？',
        semanticKey: 'question:gateway',
        expectedRootGain: 0.05,
        gatewayGain: 1,
        cost: 0.2,
      },
    ],
  })

  assert.equal(result.status, 'yield')
  assert.equal(result.request.type, 'InvestigateRequest')
  assert.equal(result.request.action.semanticKey, 'question:gateway')
})

test('WHAT[epistemic-reasoning-008] bare_candidate_without_explicit_gains_is_evaluated_by_kernel_and_investigated', () => {
  const store = createStore()
  const started = start(store, '复杂系统为什么会崩溃？')
  assessWhy(store, started.handle)

  const result = resume(store, started.handle, {
    type: 'Candidates',
    items: [
      {
        method: 'CausalMechanism',
        question: '崩溃的核心因果链是什么？',
        semanticKey: 'question:causal-chain',
      },
    ],
  })

  assert.equal(result.status, 'yield')
  assert.equal(result.request.type, 'InvestigateRequest')
  assert.equal(result.request.action.semanticKey, 'question:causal-chain')
  assert.ok(result.request.action.expectedRootGain > 0.3, 'kernel should derive expected gain from method utility')
  assert.ok(result.request.action.cost > 0.05, 'kernel should derive cost from method base cost')
  assert.ok(result.request.action.value > 0.0, 'action delta should be positive')
})
