// Split from tests/unit/agent/sphinx-mcp.test.mjs (cutover Wave 2a); owner: host-boundary
//
// Sphinx MCP Host adapter 机制：launch 判定（disabled / fixture / test-mode / local）、
// apply 保留其它 mcp server、configure 在 ok/error 两条路径都注入 mcp。
// （kernel identity/commands → epistemic-reasoning；Inquiry-only wildcard → capability-enforcement。）

import assert from 'node:assert/strict'
import test from 'node:test'
import { join } from 'node:path'
import { existsSync } from 'node:fs'

import {
  serverName,
  localCommand,
} from '../../../dist/Sphinx/Mcp.js'
import {
  apply as applyMcp,
  launchFromVars,
  defaultServerEntry,
} from '../../../dist/OpenCode/Host/SphinxMcpConfig.js'
import { managedAgentConfig, runtimeResources } from '../../verification-system/tests/support/domain.mjs'

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

const injected = (vars) => {
  const config = {}
  applyMcp(config, launchFromVars(vars))
  return config.mcp[serverName]
}

test.before(() => {
  runtimeResources.installFromPackage()
})

test('AGENT_030_launch_disabled_fixture_test_local', () => {
  const entry = defaultServerEntry()
  assert.ok(entry.endsWith(join('dist', 'Sphinx', 'McpServer.js')))
  assert.equal(existsSync(entry), true, 'defaultServerEntry must resolve to a file that exists on disk')

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

test('AGENT_030_apply_preserves_other_mcp_servers', () => {
  const config = { mcp: { other: { type: 'remote', url: 'https://example.test' } } }
  applyMcp(config, launchFromVars({}))
  assert.equal(config.mcp.other.url, 'https://example.test')
  assert.equal(config.mcp[serverName].type, 'local')
})

test('AGENT_030_configure_injects_mcp_on_ok_and_error', () => {
  const okConfig = buildConfig()
  assert.equal(managedAgentConfig.configure(okConfig).ok, true)
  assert.equal(okConfig.mcp[serverName].type, 'local')
  assert.equal(typeof okConfig.mcp[serverName].enabled, 'boolean')

  const bad = buildConfig()
  bad.agent['fast-inquiry'].model = 'shared'
  bad.agent['deep-inquiry'].model = 'shared'
  assert.equal(managedAgentConfig.configure(bad).ok, false)
  assert.equal(bad.mcp[serverName].type, 'local')
})
