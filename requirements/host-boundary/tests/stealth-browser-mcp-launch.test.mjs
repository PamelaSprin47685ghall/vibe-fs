// Split from tests/unit/agent/stealth-browser-mcp.test.mjs (cutover Wave 2a); owner: host-boundary
//
// Stealth-browser MCP Host adapter 机制：kernel identity（uvx command / ref / fixture）、
// launch 判定（disabled / fixture / test-mode / uvx）、apply 保留其它 mcp server、
// configure 在 ok/error 两条路径都注入 mcp。
// （「Browser 是唯一网络 office」role-lock 事实 → external-investigation；
//   wildcard 矩阵机制 → capability-enforcement。）

import assert from 'node:assert/strict'
import test from 'node:test'

import {
  serverName,
  permissionKey,
  defaultRef,
  isTool,
  uvxCommand,
  fixtureCommand,
} from '../../../dist/Kernel/StealthBrowserMcp.js'
import {
  apply as applyMcp,
  launchFromVars,
} from '../../../dist/Infrastructure/OpenCode/Host/StealthBrowserMcpConfig.js'
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
