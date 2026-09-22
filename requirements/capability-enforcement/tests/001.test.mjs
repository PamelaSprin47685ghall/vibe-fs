import assert from 'node:assert/strict'
import test from 'node:test'
import { integrationTest } from '../../verification-system/tests/support/tier-gate.mjs'
import { permissions } from '../../../dist/Participant/Persona/OfficeCapabilitySurface.js'
import { plan } from '../../../dist/Participant/Provider/Attempt/PlannerSurface.js'



test('WHAT[capability-enforcement-001] PROMPT_008_the_profile_derives_role_prompt_and_tools_from_the_authority', () => {
  const planned = plan({ role: 'engineer', tier: 'fast', kind: 'work-main' })

  assert.equal(planned.ok, true, planned.error)
  assert.equal(planned.canonicalRole, 'engineer')
  assert.equal(planned.systemPromptId, 'engineer', 'AGENT-001: derived from the role alone')
  assert.deepEqual(planned.toolCapabilities, [
    'BashHoneypot',
    'Edit',
    'Fetch',
    'Fission',
    'Glob',
    'Grep',
    'Move',
    'Read',
    'Remove',
    'Sphinx',
    'Write',
  ])
})

const ROLE_NAMES = ['orchestrator', 'manager', 'engineer', 'devops', 'blogger']

integrationTest('WHAT[capability-enforcement-001] MANAGER_role_permission_matrix_is_owned_by_RolesSurface', () => {
  for (const role of ROLE_NAMES) {
    const labels = permissions(role)
    assert.ok(Array.isArray(labels), role)
    if (role === 'blogger') assert.deepEqual(labels, ['Chronicle'])
  }
})
