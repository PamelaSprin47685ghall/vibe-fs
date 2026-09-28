import assert from 'node:assert/strict'
import test from 'node:test'
import { plan } from '../../../dist/Participant/Provider/Attempt/PlannerSurface.js'

test('WHAT[capability-enforcement-004] retired tier input does not alter the canonical role prompt or capability projection', () => {
  for (const role of ['engineer', 'devops', 'manager', 'orchestrator']) {
    const ordinary = plan({ role, kind: 'work-main' })
    assert.equal(ordinary.ok, true)
    for (const tier of ['fast', 'deep', 'unrecognized-retired-tier']) {
      const supplied = plan({ role, tier, kind: 'work-main' })
      assert.equal(supplied.ok, true)
      assert.equal(supplied.systemPromptId, ordinary.systemPromptId)
      assert.deepEqual(supplied.toolCapabilities, ordinary.toolCapabilities)
    }
  }
})
