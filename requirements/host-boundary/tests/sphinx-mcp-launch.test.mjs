import assert from 'node:assert/strict'
import test from 'node:test'
import { mcpConfig } from './support/host-surface.mjs'

const serverName = 'sphinx-mcp'
const buildConfig = () => ({ mcp: {} })

test('WHAT[HOST-BOUNDARY-017] AGENT_030_launch_disabled_fixture_test_local', () => {
  const entry = mcpConfig.server(serverName, 'node SphinxMcpServer.js')
  assert.equal(entry.type, 'local')
  assert.equal(entry.enabled, true)
})

test('WHAT[HOST-BOUNDARY-017] AGENT_030_apply_preserves_other_mcp_servers', () => {
  const config = mcpConfig.apply({ mcp: { other: { type: 'remote', url: 'https://example.test' } } }, serverName, mcpConfig.server(serverName, 'node SphinxMcpServer.js'))
  assert.equal(config.mcp.other.url, 'https://example.test')
  assert.equal(config.mcp[serverName].type, 'local')
})

test('WHAT[HOST-BOUNDARY-017] AGENT_030_configure_injects_mcp_on_ok_and_error', () => {
  const config = mcpConfig.apply(buildConfig(), serverName, mcpConfig.server(serverName, 'node SphinxMcpServer.js'))
  assert.equal(config.mcp[serverName].enabled, true)
  assert.equal(mcpConfig.launch({ fixture: true }).enabled, false)
})
