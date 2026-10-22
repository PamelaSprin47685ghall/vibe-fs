import assert from 'node:assert/strict'
import test from 'node:test'
import * as Loop from '../../../dist/Sphinx/V2/Runtime/Surface.js'

const estimate = (PlanId, Rank) => ({PlanId, ScopeId: 'scope-1', Location: Rank === 1 ? 0.8 : 0.2, Rank, Kind: 'model-estimate'})

test('WHAT[sphinx-v2-013] the actual selector follows declared comparison rather than plan identity or input order', () => {
  const first = [estimate('check-A', 1), estimate('find-new', 2)]
  assert.equal(Loop.decisionSelect('scope-1', first).SelectedPlanId, 'check-A')
  assert.equal(Loop.decisionSelect('scope-1', [...first].reverse()).SelectedPlanId, 'check-A')
  assert.equal(Loop.decisionSelect('scope-1', [estimate('check-A', 2), estimate('find-new', 1)]).SelectedPlanId, 'find-new')
})

test('WHAT[sphinx-v2-013] a swap of ranks changes the selection, not the id order', () => {
  assert.equal(Loop.decisionSelect('scope-1', [estimate('check-A', 2), estimate('find-new', 1)]).SelectedPlanId, 'find-new')
})

test('WHAT[sphinx-v2-013] a real tie is settled by the stated stable rule and recorded only when it happened', () => {
  const tied = [estimate('check-A', 1), estimate('find-new', 1)]
  const receipt = Loop.decisionSelect('scope-1', tied)
  // A stable operation key, not a seed, settles an equal rank.
  assert.equal(receipt.SelectedPlanId, 'check-A')
  assert.match(receipt.SelectionRule, /plan id ascending/)
  assert.equal(receipt.TieBreak, 'plan id ascending')

  // Swapping the input order must not change the selection.
  assert.equal(Loop.decisionSelect('scope-1', [...tied].reverse()).SelectedPlanId, 'check-A')
})

test('WHAT[sphinx-v2-013] a decisive rank records no tie handling', () => {
  const receipt = Loop.decisionSelect('scope-1', [estimate('check-A', 1), estimate('find-new', 2)])
  assert.equal(receipt.SelectedPlanId, 'check-A')
  assert.equal(receipt.TieBreak, null)
})
