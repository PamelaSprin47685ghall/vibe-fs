// Split from tests/unit/agent/stealth-browser-mcp.test.mjs (cutover Wave 2a); owner: capability-enforcement
//
// ENF-007: MCP wildcard 机制 — 域能力 token 留在 Roles.permissions，wildcard 只是
// schema 键。本文件锁矩阵机制半边：`stealth-browser-mcp_*` 键被钉在每个 role 的
// schema（allow 或 deny，绝不缺席），具体 MCP 工具经 wildcard 求值得到与键一致的
// 动作；不发射虚构的 network 键。
// （「Browser 是唯一网络 office」role-lock 事实半边 → external-investigation；
//   kernel identity/launch/env/apply → host-boundary。）

import assert from 'node:assert/strict'
import test from 'node:test'

import { managedAgentConfig, runtimeResources } from '../../verification-system/tests/support/domain.mjs'

const permissionKey = 'stealth-browser-mcp_*'

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
const TIERS = ['fast', 'deep']
const agentName = (tier, role) => `${tier}-${role.toLowerCase()}`

const buildConfig = () => {
  const agent = {}
  for (const tier of TIERS) {
    for (const role of ROLES) {
      agent[agentName(tier, role)] = { model: `${tier}-${role.toLowerCase()}-model` }
    }
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
  for (const [key, value] of Object.entries(permissionObj)) {
    if (typeof value === 'string') rules.push({ permission: key, action: value })
  }
  return (
    [...rules].reverse().find((r) => wildcardMatch(tool, r.permission)) ?? { action: 'ask' }
  )
}

test.before(() => {
  runtimeResources.installFromPackage()
})

test('AGENT_026_wildcard_matrix_mechanism', () => {
  const config = buildConfig()
  assert.equal(managedAgentConfig.configure(config).ok, true)

  for (const tier of TIERS) {
    for (const role of ROLES) {
      const name = agentName(tier, role)
      const permission = config.agent[name].permission

      // 矩阵机制：wildcard 键必须钉在每个 role 的 schema（allow 或 deny），
      // 绝不缺席——缺席会让该 role 对具体 MCP 工具落到 'ask'/默认路径。
      assert.equal(typeof permission[permissionKey], 'string', `${name} must pin ${permissionKey}`)

      // 不发射虚构的 network 键（域能力 token 只经 Roles.permissions 进入 schema）。
      assert.equal(permission.network, undefined, `${name} must not emit fictional network`)

      // 具体 MCP 工具经 wildcard 求值 = 键值（wildcard 是驱动具体工具的 schema 键）。
      const concrete = evaluate(permission, 'stealth-browser-mcp_get_debug_view').action
      assert.equal(concrete, permission[permissionKey], `${name} concrete MCP tool must follow the wildcard key`)
    }
  }
})
