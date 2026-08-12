// tests/unit/agent/sphinx-mcp.test.mjs — AGENT-028
//
// Kernel identity + Host mcp.sphinx injection + Meditator-only schema wildcard.
// Requires: npm run build (dist/Kernel/SphinxMcp.js + SphinxMcpConfig.js).

import assert from 'node:assert/strict'
import test from 'node:test'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

import { managedAgentConfig, runtimeResources } from '../support/domain.mjs'

const here = dirname(fileURLToPath(import.meta.url))
const dist = join(here, '../../../dist')

const sphinx = await import(join(dist, 'Kernel/SphinxMcp.js'))
const {
  serverName,
  permissionKey,
  relativeServerEntry,
  isTool,
  localCommand,
  fixtureCommand,
} = sphinx

const configMod = await import(join(dist, 'Infrastructure/OpenCode/Host/SphinxMcpConfig.js'))
const { apply: applyMcp, launchFromVars, defaultServerEntry } = configMod

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

test('AGENT_028_kernel_identity_and_commands', () => {
  assert.equal(serverName, 'sphinx')
  assert.equal(permissionKey, 'sphinx_*')
  assert.equal(relativeServerEntry, 'dist/sphinx/mcp-server.js')
  assert.equal(isTool('sphinx_start'), true)
  assert.equal(isTool('sphinx_resume'), true)
  assert.equal(isTool('stealth-browser-mcp_get_debug_view'), false)
  assert.equal(isTool('inspector'), false)
  assert.deepEqual(localCommand('/tmp/entry.js'), ['node', '/tmp/entry.js'])
  assert.deepEqual(fixtureCommand('/tmp/sphinx-fixture.js'), ['node', '/tmp/sphinx-fixture.js'])
})

test('AGENT_028_launch_disabled_fixture_test_local', () => {
  const entry = defaultServerEntry()
  assert.ok(entry.endsWith(join('dist', 'sphinx', 'mcp-server.js')))

  const disabled = injected({ SPHINX_MCP_DISABLED: '1' })
  assert.equal(disabled.enabled, false)
  assert.equal(disabled.type, 'local')
  assert.deepEqual(disabled.command, localCommand(entry))

  const fixture = injected({
    SPHINX_MCP_FIXTURE: '/tmp/sphinx-fixture.js',
    WANXIANGSHU_TEST: 'true',
  })
  assert.equal(fixture.enabled, true)
  assert.deepEqual(fixture.command, ['node', '/tmp/sphinx-fixture.js'])

  const testMode = injected({ WANXIANGSHU_TEST: 'true' })
  assert.equal(testMode.enabled, false)

  const local = injected({})
  assert.equal(local.type, 'local')
  assert.equal(local.enabled, true)
  assert.deepEqual(local.command, localCommand(entry))
})

test('AGENT_028_apply_preserves_other_mcp_servers', () => {
  const config = { mcp: { other: { type: 'remote', url: 'https://example.test' } } }
  applyMcp(config, launchFromVars({}))
  assert.equal(config.mcp.other.url, 'https://example.test')
  assert.equal(config.mcp[serverName].type, 'local')
})

test('AGENT_028_configure_injects_mcp_on_ok_and_error', () => {
  const okConfig = buildConfig()
  assert.equal(managedAgentConfig.configure(okConfig).ok, true)
  assert.equal(okConfig.mcp[serverName].type, 'local')
  assert.equal(typeof okConfig.mcp[serverName].enabled, 'boolean')

  const bad = buildConfig()
  bad.agent['fast-meditator'].model = 'shared'
  bad.agent['deep-meditator'].model = 'shared'
  assert.equal(managedAgentConfig.configure(bad).ok, false)
  assert.equal(bad.mcp[serverName].type, 'local')
})

test('AGENT_028_meditator_only_wildcard_permission', () => {
  const config = buildConfig()
  assert.equal(managedAgentConfig.configure(config).ok, true)

  for (const tier of TIERS) {
    for (const role of ROLES) {
      const name = agentName(tier, role)
      const permission = config.agent[name].permission
      assert.equal(
        permission[permissionKey],
        role === 'Meditator' ? 'allow' : 'deny',
        `${name} sphinx_*`,
      )
      const concrete = evaluate(permission, 'sphinx_start').action
      assert.equal(concrete, role === 'Meditator' ? 'allow' : 'deny', `${name} concrete MCP tool`)
    }
  }
})
