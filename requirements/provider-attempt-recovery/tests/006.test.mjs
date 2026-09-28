import assert from 'node:assert/strict'
import test from 'node:test'
import * as planner from '../../../dist/Participant/Provider/Attempt/PlannerSurface.js'

// These are independently constructed plans, not a physical retry lifecycle.
test('WHAT[provider-attempt-recovery-006] request kind changes preserve the selected plan identity and system prompt', () => {
  const main = planner.plan({ role: 'engineer', kind: 'work-main' })
  const repair = planner.plan({ role: 'engineer', kind: 'interaction-repair' })
  assert.equal(main.ok, true)
  assert.equal(repair.ok, true)
  assert.equal(main.requestKind, 'work-main')
  assert.equal(repair.requestKind, 'interaction-repair')
  assert.equal(main.participantIdentity.selectedAgent, 'engineer')
  assert.deepEqual(repair.participantIdentity, main.participantIdentity)
  assert.equal(repair.systemPromptId, main.systemPromptId)
  assert.deepEqual(repair.toolCapabilities, main.toolCapabilities)
})

test.todo('WHAT[provider-attempt-recovery-006] actual retry with changed target preserves durable identity and full system prompt through Host submission (GAP-139)')
