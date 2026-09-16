// requirements/capability-enforcement/tests/sphinx-mcp-wildcard.test.mjs
//
// ENF-007 / OFF-018: Sphinx MCP wildcard & programmatic workflow.
// Sphinx is driven as a programmatic epistemic workflow, not an interactive role tool.
// No active canonical role carries sphinx_* in its ordinary Host schema permission.

import assert from 'node:assert/strict'
import test from 'node:test'

import { configure as configureManagedAgents, installDefaultResources } from '../../../dist/OpenCode/Host/ManagedAgentConfigSurface.js'

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
