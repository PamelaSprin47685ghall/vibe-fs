import assert from 'node:assert/strict'
import test from 'node:test'
import { plan } from '../../../dist/Participant/Provider/Attempt/PlannerSurface.js'



test('WHAT[ENF-001] PROMPT_008_the_profile_derives_role_prompt_and_tools_from_the_authority', () => {
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
    'Write',
  ])
})
