import assert from 'node:assert/strict'
import test from 'node:test'
import { permissions } from '../../../dist/Participant/Persona/OfficeCapabilitySurface.js'
import { plan } from '../../../dist/Participant/Provider/Attempt/PlannerSurface.js'

test('WHAT[capability-enforcement-001] ordinary execution profiles carry the role-owned capability projection', () => {
  for (const role of ['engineer', 'devops', 'manager', 'orchestrator']) {
    const planned = plan({ role, kind: 'work-main' })
    assert.equal(planned.ok, true, planned.error)
    assert.equal(planned.canonicalRole, role)
    assert.equal(planned.systemPromptId, role)
    assert.deepEqual(planned.toolCapabilities, permissions(role))
  }
})

test('WHAT[capability-enforcement-001] an unknown role or request kind cannot produce an executable profile', () => {
  assert.equal(plan({ role: 'unknown', kind: 'work-main' }).ok, false)
  assert.equal(plan({ role: 'engineer', kind: 'unknown' }).ok, false)
})
