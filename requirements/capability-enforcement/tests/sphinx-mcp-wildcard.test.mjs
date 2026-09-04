// Split from tests/unit/agent/sphinx-mcp.test.mjs (cutover Wave 2a); owner: capability-enforcement
//
// ENF-007: MCP wildcard 机制 — 域能力 token 留在 Roles.permissions，wildcard 只是
// schema 键。本文件锁 Sphinx 的 `sphinx_*` wildcard：仅 Inquiry role 允许，
// 其余 role deny，且具体 MCP 工具（sphinx_start）经 wildcard 求值一致。
// （kernel identity/commands → epistemic-reasoning；launch/env/apply → host-boundary。）

import assert from 'node:assert/strict'
import test from 'node:test'

import { configure as configureManagedAgents, installDefaultResources } from '../../../dist/OpenCode/Host/ManagedAgentConfigSurface.js'

const permissionKey = 'sphinx_*'

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