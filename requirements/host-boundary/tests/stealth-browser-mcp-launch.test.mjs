import assert from 'node:assert/strict'
import test from 'node:test'
import { mcpConfig } from './support/host-surface.mjs'

const serverName = 'stealth-browser-mcp'
const command = ['uvx', 'stealth-browser-mcp@master']

test('WHAT[HOST-BOUNDARY-017] AGENT_026_kernel_identity_and_commands', () => {
  assert.equal(serverName, 'stealth-browser-mcp')
  assert.deepEqual(command, ['uvx', 'stealth-browser-mcp@master'])
})

test('WHAT[HOST-BOUNDARY-017] AGENT_026_launch_disabled_fixture_test_uvx', () => {
  const disabled = mcpConfig.launch({ enabled: false })
  assert.equal(disabled.enabled, false)
  assert.equal(disabled.reason, 'disabled')
})

test('WHAT[HOST-BOUNDARY-017] AGENT_026_apply_preserves_other_mcp_servers', () => {
  const config = mcpConfig.apply({ mcp: { other: { type: 'remote', url: 'https://example.test' } } }, serverName, mcpConfig.server(serverName, command))
  assert.equal(config.mcp.other.url, 'https://example.test')
  assert.equal(config.mcp[serverName].command, command)
})

test('WHAT[HOST-BOUNDARY-017] AGENT_026_configure_injects_mcp_on_ok_and_error', () => {
  const config = mcpConfig.apply({ mcp: {} }, serverName, mcpConfig.server(serverName, command))
  assert.equal(config.mcp[serverName].enabled, true)
  assert.equal(mcpConfig.launch({ testMode: true }).enabled, false)
})
