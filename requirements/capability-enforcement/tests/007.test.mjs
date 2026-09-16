// ENF-007: Unified semantic capability policy for Native/MCP/Plugin tools
import assert from 'node:assert/strict'
import test from 'node:test'
import { configure as configureManagedAgents, installDefaultResources } from '../../../dist/OpenCode/Host/ManagedAgentConfigSurface.js'

const ROLES = [
  'Manager',
  'Orchestrator',
  'Coder',
  'Inspector',
  'Browser',
  'Inquiry',
  'DevOps',
  'Distiller',
  'Blogger',
  'Bookkeeper',
]
const agentName = (role) => `${role.toLowerCase()}`

const buildConfig = () => {
  const agent = {}
  for (const role of ROLES) {
    agent[agentName(role)] = { model: `${role.toLowerCase()}-model` };
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

installDefaultResources()

test('WHAT[ENF-007] AGENT_030_inquiry_only_wildcard_permission', () => {
  const permissionKey = 'sphinx_*'
  const config = buildConfig()
  assert.equal(configureManagedAgents(config).ok, true)

  for (const role of ROLES) {
    const name = agentName(role);
    const permission = config.agent[name].permission
    assert.equal(
      permission[permissionKey],
      role === 'Inquiry' ? 'allow' : 'deny',
      `${name} sphinx_*`,
    )
    const concrete = evaluate(permission, 'sphinx_start').action
    assert.equal(concrete, role === 'Inquiry' ? 'allow' : 'deny', `${name} concrete MCP tool`)
  }
})

test('WHAT[ENF-007] AGENT_026_wildcard_matrix_mechanism', () => {
  const permissionKey = 'stealth-browser-mcp_*'
  const config = buildConfig()
  assert.equal(configureManagedAgents(config).ok, true)

  for (const role of ROLES) {
    const name = agentName(role);
    const permission = config.agent[name].permission

    assert.equal(typeof permission[permissionKey], 'string', `${name} must pin ${permissionKey}`)
    assert.equal(permission.network, undefined, `${name} must not emit fictional network`)

    const concrete = evaluate(permission, 'stealth-browser-mcp_get_debug_view').action
    assert.equal(concrete, permission[permissionKey], `${name} concrete MCP tool must follow the wildcard key`)
  }
})
