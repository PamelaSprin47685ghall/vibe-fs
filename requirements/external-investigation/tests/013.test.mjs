import assert from 'node:assert/strict'
import test from 'node:test'
import { configure as configureManagedAgents, installDefaultResources } from '../../../dist/OpenCode/Host/ManagedAgentConfigSurface.js'

const permissionKey = 'stealth-browser-mcp_*'

const CONCRETE_TOOL = 'stealth-browser-mcp_get_debug_view'

const ACTIVE_ROLES = [
  'Manager',
  'Orchestrator',
  'Engineer',
  'DevOps',
  'Blogger',
  'Bookkeeper',
]

const agentName = (role) => role.toLowerCase()

const buildConfig = (roles = ACTIVE_ROLES) => {
  const agent = {}
  for (const role of roles) {
    agent[agentName(role)] = { model: `${agentName(role)}-model` }
  }
  return { agent }
}

const wildcardMatch = (input, pattern) => {
  const escaped = pattern
    .replaceAll('\\', '/')
    .replace(/[.+^${}()|[\]\\]/g, '\\$&')
    .replace(/\*/g, '.*')
    .replace(/\?/g, '.')
  return new RegExp('^' + escaped + '$', 's').test(input.replaceAll('\\', '/'))
}

const evaluate = (permissionObj, tool) => {
  const rules = []
  for (const key in permissionObj) {
    const value = permissionObj[key]
    if (typeof value === 'string') rules.push({ permission: key, action: value })
  }
  return (
    [...rules].reverse().find((r) => wildcardMatch(tool, r.permission)) ?? { action: 'ask' }
  )
}

test.before(() => {
  installDefaultResources()
})

test('WHAT[EXTERNAL-INVESTIGATION-013] external_investigation_duties_not_transferred_to_engineer_or_devops', () => {
  const config = buildConfig()
  assert.equal(configureManagedAgents(config).ok, true)

  for (const role of ['Engineer', 'DevOps']) {
    const name = agentName(role)
    const permission = config.agent[name]?.permission ?? {}
    assert.notEqual(permission['stealth-browser-mcp_*'], 'allow', `${name} must not have stealth-browser-mcp`)
    assert.notEqual(permission['js-browser'], 'allow', `${name} must not have js-browser`)
  }
})
