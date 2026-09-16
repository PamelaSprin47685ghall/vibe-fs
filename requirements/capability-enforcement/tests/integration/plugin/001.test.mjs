import assert from 'node:assert/strict'
import test from 'node:test'
import { plan } from '../../../../../dist/Participant/Provider/Attempt/PlannerSurface.js'
import { permissions } from '../../../../../dist/Participant/Persona/OfficeCapabilitySurface.js'

const ROLE_NAMES = ['orchestrator', 'manager', 'coder', 'inspector', 'devops', 'browser', 'inquiry', 'blogger', 'distiller']

test('WHAT[ENF-001] PROMPT_008_the_profile_derives_role_prompt_and_tools_from_the_authority', () => {
  const planned = plan({ role: 'coder', tier: 'fast', kind: 'work-main' })

  assert.equal(planned.ok, true, planned.error)
  assert.equal(planned.canonicalRole, 'coder')
  assert.equal(planned.systemPromptId, 'coder', 'AGENT-001: derived from the role alone')
  assert.deepEqual(planned.toolCapabilities, [
    'BashHoneypot',
    'Edit',
    'Fetch',
    'Fission',
    'Glob',
    'Grep',
    'Inspect',
    'Move',
    'Read',
    'Remove',
    'Write',
  ])
})

test('WHAT[ENF-001] MANAGER_role_permission_matrix_is_owned_by_RolesSurface', () => {
  for (const role of ROLE_NAMES) {
    const labels = permissions(role)
    assert.ok(Array.isArray(labels), role)
    if (role === 'distiller') assert.deepEqual(labels, [])
    if (role === 'blogger') assert.deepEqual(labels, ['Chronicle'])
  }
})
