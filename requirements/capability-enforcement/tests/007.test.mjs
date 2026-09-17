import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { configure: configureManagedAgents, installDefaultResources } = await import("../../../dist/OpenCode/Host/ManagedAgentConfigSurface.js");

const permissionKey = 'sphinx_*'
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

test('WHAT[ENF-007] sphinx_wildcard_is_not_exposed_to_ordinary_interactive_roles', () => {
  const config = buildConfig()
  assert.equal(configureManagedAgents(config).ok, true)

  for (const role of ROLES) {
    const name = agentName(role)
    const permission = config.agent[name].permission
    assert.equal(
      permission[permissionKey],
      'deny',
      `${name} must deny sphinx_*`,
    )
  }
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { configure: configureManagedAgents, installDefaultResources } = await import("../../../dist/OpenCode/Host/ManagedAgentConfigSurface.js");
const { allRoleLabels } = await import("../../../dist/Foundation/RolesSurface.js");
const { isAllowed } = await import("../../../dist/Participant/Persona/OfficeCapabilitySurface.js");

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
}
