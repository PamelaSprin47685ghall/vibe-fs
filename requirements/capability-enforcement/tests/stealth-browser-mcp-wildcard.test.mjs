// requirements/capability-enforcement/tests/stealth-browser-mcp-wildcard.test.mjs
//
// ENF-007 / OFF-013: Browser role and Network permission revocation.
// All canonical active roles have no Network or Browser MCP permissions.
// Browser role is completely deleted from the active catalog.

import assert from 'node:assert/strict'
import test from 'node:test'

import { configure as configureManagedAgents, installDefaultResources } from '../../../dist/OpenCode/Host/ManagedAgentConfigSurface.js'
import { allRoleLabels } from '../../../dist/Foundation/RolesSurface.js'
import { isAllowed } from '../../../dist/Participant/Persona/OfficeCapabilitySurface.js'

const ROLES = [
  'Manager',
  'Orchestrator',
  'Engineer',
  'DevOps',
  'Blogger',
]
const agentName = (role) => `${role.toLowerCase()}`

const buildConfig = () => {
  const agent = {}
  for (const role of ROLES) {
    agent[agentName(role)] = { model: `${role.toLowerCase()}-model` }
  }
  return { agent }
}

installDefaultResources()

test('WHAT[ENF-007] browser_role_is_revoked_from_canonical_active_roles', () => {
  assert.equal(allRoleLabels.includes('browser'), false, 'Browser role must be deleted from canonical active roles')
})

test('WHAT[ENF-007] network_and_stealth_browser_mcp_are_denied_for_all_roles', () => {
  const config = buildConfig()
  assert.equal(configureManagedAgents(config).ok, true)

  for (const role of ROLES) {
    const name = agentName(role)
    const permission = config.agent[name].permission
    assert.notEqual(permission['stealth-browser-mcp_*'], 'allow', `${name} must not allow stealth-browser-mcp`)
    assert.equal(isAllowed(name, 'Network'), false, `${name} must not have Network permission`)
  }
})
