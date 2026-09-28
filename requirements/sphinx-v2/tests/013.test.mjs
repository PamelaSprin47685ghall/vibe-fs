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

test.todo('WHAT[sphinx-v2-013] real Agenda refuses unsatisfied or failed prerequisites including a parent merely selected in the same concurrent batch')
