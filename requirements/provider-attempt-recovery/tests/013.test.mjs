import assert from 'node:assert/strict'
import test from 'node:test'
import * as planner from '../../../dist/Participant/Provider/Attempt/PlannerSurface.js'

const identity = role => {
  const plan = planner.plan({ role, kind: 'work-main' })
  assert.equal(plan.ok, true)
  return plan
}

test('WHAT[provider-attempt-recovery-013] newly constructed plans expose the selected participant evidence instead of a fixed fixture answer', () => {
  const engineer = identity('engineer')
  const manager = identity('manager')
  for (const [plan, role, persona] of [[engineer, 'engineer', 'Engineer'], [manager, 'manager', 'Lead']]) {
    assert.equal(plan.participant, role)
    assert.equal(plan.canonicalRole, role)
    assert.equal(plan.systemPromptId, role)
    assert.equal(plan.participantIdentity.selectedAgent, role)
    assert.equal(plan.participantIdentity.persona, persona)
    assert.equal(plan.participantIdentity.origin, 'ResolvedAtRoot')
    assert.ok(Number.isInteger(plan.participantIdentity.personaCatalogVersion))
  }
  assert.notDeepEqual(engineer.participantIdentity, manager.participantIdentity)
})

test.todo('WHAT[provider-attempt-recovery-013] actual retries and restart preserve complete durable Persona provenance language authority and provider horizon (GAP-139)')
