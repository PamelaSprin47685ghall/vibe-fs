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

test('WHAT[EXTERNAL-INVESTIGATION-012] all_active_roles_denied_browser_mcp_and_no_browser_role_configured', () => {
  const config = buildConfig()
  assert.equal(configureManagedAgents(config).ok, true)

  // EXTERNAL-INVESTIGATION-012: 撤销 Browser 角色，系统中所有活跃角色均无 Browser MCP 权限
  for (const role of ACTIVE_ROLES) {
    const name = agentName(role)
    const permission = config.agent[name]?.permission ?? {}

    assert.notEqual(
      permission[permissionKey],
      'allow',
      `${name} ${permissionKey} must never be allowed`,
    )
    const concrete = evaluate(permission, CONCRETE_TOOL).action
    assert.equal(concrete, 'deny', `${name} concrete MCP tool must be deny`)
  }

  // 验证 Browser 不在合法活跃角色配置中
  assert.equal(config.agent['browser'], undefined, 'browser agent must not be configured')
})
