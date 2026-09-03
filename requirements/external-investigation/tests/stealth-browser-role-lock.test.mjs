// Split from tests/unit/agent/stealth-browser-mcp.test.mjs (cutover Wave 2a); owner: external-investigation
//
// EXTERNAL-INVESTIGATION-010（外部/本地证据分离）：Browser 是唯一网络能力 office。
// role-lock 事实半边——`stealth-browser-mcp_*` 只有 Browser 得 allow，其它 role 一律
// deny（wildcard 键与具体 MCP 工具两层）。
// （wildcard 矩阵机制半边 → capability-enforcement；kernel identity/launch/env/apply → host-boundary。）

import assert from 'node:assert/strict'
import test from 'node:test'

import { configure as configureManagedAgents, installDefaultResources } from '../../../dist/OpenCode/Host/ManagedAgentConfigSurface.js'

const permissionKey = 'stealth-browser-mcp_*'
const CONCRETE_TOOL = 'stealth-browser-mcp_get_debug_view'

const ROLES = [
  'Manager',
  'Orchestrator',
  'Coder',
  'Inspector',
  'Browser',
  'Inquiry',
  'Reviewer',
  'DevOps',
  'Distiller',
  'Blogger',
  'Bookkeeper',
]
const agentName = (role) => `${role.toLowerCase()}`

const buildConfig = () => {
  const agent = {}
  for (const role of ROLES) {
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

test('WHAT[EXTERNAL-INVESTIGATION-010] browser_is_the_only_network_office', () => {
  const config = buildConfig()
  assert.equal(configureManagedAgents(config).ok, true)

  for (const role of ROLES) {
    const name = agentName(role)
    const permission = config.agent[name].permission

    // role-lock 事实：只有 Browser 能到达外部网络，其它 role 一律 deny。
    assert.equal(
      permission[permissionKey],
      role === 'Browser' ? 'allow' : 'deny',
      `${name} ${permissionKey}`,
    )
    const concrete = evaluate(permission, CONCRETE_TOOL).action
    assert.equal(concrete, role === 'Browser' ? 'allow' : 'deny', `${name} concrete MCP tool`)
  }
})
