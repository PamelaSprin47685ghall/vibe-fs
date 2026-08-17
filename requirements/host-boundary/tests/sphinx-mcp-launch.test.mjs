// HOST-BOUNDARY-017: Sphinx MCP Host adapter launch decision and config apply.
//
// Production owner: dist/OpenCode/Host/SphinxMcpConfigSurface.js
// — a registered JS-native surface that translates env vars → plain JS
//   `{ kind, path, enabled, reason }` and applies the launch to a Host config.
// No Fable DU cases cross the edge; no support fixture is used.

import assert from 'node:assert/strict'
import test from 'node:test'

import * as SphinxMcpConfigSurface from '../../../dist/OpenCode/Host/SphinxMcpConfigSurface.js'

const serverName = SphinxMcpConfigSurface.serverIdentity()
const read = (env) => (name) => env[name] ?? undefined

test('WHAT[HOST-BOUNDARY-017] AGENT_030_launch_default_is_local_node_entry', () => {
  const decision = SphinxMcpConfigSurface.launchDecision(read({}))
  assert.equal(decision.kind, 'local')
  assert.equal(decision.enabled, true)
  assert.equal(decision.reason, 'enabled')
  assert.ok(decision.path, 'local launch must carry a server entry path')
})

test('WHAT[HOST-BOUNDARY-017] AGENT_030_launch_disabled_when_env_set', () => {
  const decision = SphinxMcpConfigSurface.launchDecision(read({ SPHINX_MCP_DISABLED: '1' }))
  assert.equal(decision.kind, 'disabled')
  assert.equal(decision.enabled, false)
  assert.equal(decision.reason, 'disabled')
})

test('WHAT[HOST-BOUNDARY-017] AGENT_030_launch_fixture_when_fixture_env_set', () => {
  const decision = SphinxMcpConfigSurface.launchDecision(read({ SPHINX_MCP_FIXTURE: '/path/to/fixture.js' }))
  assert.equal(decision.kind, 'fixture')
  assert.equal(decision.enabled, true)
  assert.equal(decision.path, '/path/to/fixture.js')
  assert.equal(decision.reason, 'fixture')
})

test('WHAT[HOST-BOUNDARY-017] AGENT_030_launch_disabled_in_test_mode', () => {
  const decision = SphinxMcpConfigSurface.launchDecision(read({ WANXIANGSHU_TEST: '1' }))
  assert.equal(decision.kind, 'disabled')
  assert.equal(decision.enabled, false)
})

test('WHAT[HOST-BOUNDARY-017] AGENT_030_apply_preserves_other_mcp_servers', () => {
  const config = { mcp: { other: { type: 'remote', url: 'https://example.test' } } }
  SphinxMcpConfigSurface.applyToConfig(config, read({}))
  assert.equal(config.mcp.other.url, 'https://example.test')
  assert.equal(config.mcp[serverName].type, 'local')
  assert.equal(config.mcp[serverName].enabled, true)
  assert.deepEqual(config.mcp[serverName].command, ['node', SphinxMcpConfigSurface.launchDecision(read({})).path])
})

test('WHAT[HOST-BOUNDARY-017] AGENT_030_apply_injects_disabled_entry_when_test_mode', () => {
  const config = { mcp: {} }
  SphinxMcpConfigSurface.applyToConfig(config, read({ WANXIANGSHU_TEST: '1' }))
  assert.equal(config.mcp[serverName].enabled, false)
  assert.equal(config.mcp[serverName].type, 'local')
})

test('WHAT[HOST-BOUNDARY-017] AGENT_030_entry_for_disabled_launch_is_not_enabled', () => {
  const entry = SphinxMcpConfigSurface.entryFor(read({ SPHINX_MCP_DISABLED: '1' }))
  assert.equal(entry.enabled, false)
  assert.equal(entry.type, 'local')
  assert.ok(Array.isArray(entry.command), 'disabled entry still carries a command for structural consistency')
})

test('WHAT[HOST-BOUNDARY-017] AGENT_030_entry_for_local_launch_carries_node_command', () => {
  const entry = SphinxMcpConfigSurface.entryFor(read({}))
  assert.equal(entry.enabled, true)
  assert.equal(entry.type, 'local')
  assert.equal(entry.command[0], 'node')
})
