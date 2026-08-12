// tests/unit/agent/stealth-browser-mcp.test.mjs — AGENT-026
//
// Kernel command + Host mcp injection + Browser-only schema wildcard.

import assert from 'node:assert/strict'
import test from 'node:test'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

import { managedAgentConfig, runtimeResources } from '../support/domain.mjs'

const here = dirname(fileURLToPath(import.meta.url))
const dist = join(here, '../../../dist')

const stealth = await import(join(dist, 'Kernel/StealthBrowserMcp.js'))
const {
  serverName,
  permissionKey,
  defaultRef,
  isTool,
  uvxCommand,
  fixtureCommand,
} = stealth

const configMod = await import(join(dist, 'Infrastructure/OpenCode/Host/StealthBrowserMcpConfig.js'))
const { apply: applyMcp, launchFromVars } = configMod

const ROLES = [
  'Manager',
  'Orchestrator',
  'Coder',
  'Inspector',
  'Browser',
  'Meditator',
  'Reviewer',
  'DevOps',
  'Executor',
  'Blogger',
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

const injected = (vars) => {
  const config = {}
  applyMcp(config, launchFromVars(vars))
  return config.mcp[serverName]
}

test.before(() => {
  runtimeResources.installFromPackage()
})

test('AGENT_026_kernel_identity_and_commands', () => {
  assert.equal(serverName, 'stealth-browser-mcp')
  assert.equal(permissionKey, 'stealth-browser-mcp_*')
  assert.equal(defaultRef, 'master')
  assert.equal(isTool('stealth-browser-mcp_get_debug_view'), true)
  assert.equal(isTool('network'), false)
  assert.equal(isTool('read'), false)
  assert.deepEqual(uvxCommand(''), [
    'uvx',
    '--python',
    '3.13',
    '--from',
    'git+https://github.com/vibheksoni/stealth-browser-mcp.git@master',
    'python',
    '-m',
    'server',
  ])
  assert.deepEqual(uvxCommand(' v1.2.3 '), [
    'uvx',
    '--python',
    '3.13',
    '--from',
    'git+https://github.com/vibheksoni/stealth-browser-mcp.git@v1.2.3',
    'python',
    '-m',
    'server',
  ])
  assert.deepEqual(fixtureCommand('/tmp/fixture.js'), ['node', '/tmp/fixture.js'])
})

test('AGENT_026_launch_disabled_fixture_test_uvx', () => {
  const disabled = injected({ STEALTH_BROWSER_MCP_DISABLED: '1' })
  assert.equal(disabled.enabled, false)
  assert.deepEqual(disabled.command, uvxCommand(defaultRef))

  const fixture = injected({
    STEALTH_BROWSER_MCP_FIXTURE: '/tmp/stealth-fixture.js',
    WANXIANGSHU_TEST: 'true',
  })
  assert.equal(fixture.enabled, true)
  assert.deepEqual(fixture.command, ['node', '/tmp/stealth-fixture.js'])

  const testMode = injected({ WANXIANGSHU_TEST: 'true' })
  assert.equal(testMode.enabled, false)

  const uvx = injected({ STEALTH_BROWSER_MCP_REF: 'release-1' })
  assert.equal(uvx.type, 'local')
  assert.equal(uvx.enabled, true)
  assert.deepEqual(uvx.command, uvxCommand('release-1'))
})

test('AGENT_026_apply_preserves_other_mcp_servers', () => {
  const config = { mcp: { other: { type: 'remote', url: 'https://example.test' } } }
  applyMcp(config, launchFromVars({ STEALTH_BROWSER_MCP_REF: 'master' }))
  assert.equal(config.mcp.other.url, 'https://example.test')
  assert.equal(config.mcp[serverName].type, 'local')
})

test('AGENT_026_configure_injects_mcp_on_ok_and_error', () => {
  const okConfig = buildConfig()
  assert.equal(managedAgentConfig.configure(okConfig).ok, true)
  assert.equal(okConfig.mcp[serverName].type, 'local')
  assert.equal(typeof okConfig.mcp[serverName].enabled, 'boolean')

  const bad = buildConfig()
  bad.agent['fast-browser'].model = 'shared'
  bad.agent['deep-browser'].model = 'shared'
  assert.equal(managedAgentConfig.configure(bad).ok, false)
  assert.equal(bad.mcp[serverName].type, 'local')
})

test('AGENT_026_browser_only_wildcard_permission', () => {
  const config = buildConfig()
  assert.equal(managedAgentConfig.configure(config).ok, true)

  for (const tier of TIERS) {
    for (const role of ROLES) {
      const name = agentName(tier, role)
      const permission = config.agent[name].permission
      assert.equal(permission.network, undefined, `${name} must not emit fictional network`)
      assert.equal(
        permission[permissionKey],
        role === 'Browser' ? 'allow' : 'deny',
        `${name} stealth-browser-mcp_*`,
      )
      const concrete = evaluate(permission, 'stealth-browser-mcp_get_debug_view').action
      assert.equal(concrete, role === 'Browser' ? 'allow' : 'deny', `${name} concrete MCP tool`)
    }
  }
})
