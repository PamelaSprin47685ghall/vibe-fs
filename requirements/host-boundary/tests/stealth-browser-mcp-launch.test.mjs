// HOST-BOUNDARY-017: stealth-browser MCP Host adapter launch decision and config apply.
//
// Production owner: dist/OpenCode/Host/StealthBrowserMcpConfigSurface.js
// — a registered JS-native surface that translates env vars → plain JS
//   `{ kind, ref, path, enabled, reason }` and applies the launch to a Host config.
// No Fable DU cases cross the edge; no support fixture is used.

import assert from 'node:assert/strict'
import test from 'node:test'

import * as StealthBrowserMcpConfigSurface from '../../../dist/OpenCode/Host/StealthBrowserMcpConfigSurface.js'

const serverName = StealthBrowserMcpConfigSurface.serverIdentity()
const read = (env) => (name) => env[name] ?? undefined

test('WHAT[HOST-BOUNDARY-017] AGENT_026_kernel_identity_and_commands', () => {
  assert.equal(serverName, 'stealth-browser-mcp')
  const uvx = StealthBrowserMcpConfigSurface.uvxCommandFor('master')
  assert.deepEqual(uvx, ['uvx', '--python', '3.13', '--from', 'git+https://github.com/vibheksoni/stealth-browser-mcp.git@master', 'python', '-m', 'server'])
  const fixture = StealthBrowserMcpConfigSurface.fixtureCommandFor('/path/to/fix.js')
  assert.deepEqual(fixture, ['node', '/path/to/fix.js'])
})

test('WHAT[HOST-BOUNDARY-017] AGENT_026_launch_default_is_uvx_master', () => {
  const decision = StealthBrowserMcpConfigSurface.launchDecision(read({}))
  assert.equal(decision.kind, 'uvx')
  assert.equal(decision.enabled, true)
  assert.equal(decision.reason, 'enabled')
  assert.equal(decision.ref, 'master')
})

test('WHAT[HOST-BOUNDARY-017] AGENT_026_launch_disabled_when_env_set', () => {
  const decision = StealthBrowserMcpConfigSurface.launchDecision(read({ STEALTH_BROWSER_MCP_DISABLED: '1' }))
  assert.equal(decision.kind, 'disabled')
  assert.equal(decision.enabled, false)
  assert.equal(decision.reason, 'disabled')
})

test('WHAT[HOST-BOUNDARY-017] AGENT_026_launch_fixture_when_fixture_env_set', () => {
  const decision = StealthBrowserMcpConfigSurface.launchDecision(read({ STEALTH_BROWSER_MCP_FIXTURE: '/path/to/fixture.js' }))
  assert.equal(decision.kind, 'fixture')
  assert.equal(decision.enabled, true)
  assert.equal(decision.path, '/path/to/fixture.js')
  assert.equal(decision.reason, 'fixture')
})

test('WHAT[HOST-BOUNDARY-017] AGENT_026_launch_disabled_in_test_mode', () => {
  const decision = StealthBrowserMcpConfigSurface.launchDecision(read({ WANXIANGSHU_TEST: '1' }))
  assert.equal(decision.kind, 'disabled')
  assert.equal(decision.enabled, false)
})

test('WHAT[HOST-BOUNDARY-017] AGENT_026_launch_uvx_with_custom_ref', () => {
  const decision = StealthBrowserMcpConfigSurface.launchDecision(read({ STEALTH_BROWSER_MCP_REF: 'v1.0' }))
  assert.equal(decision.kind, 'uvx')
  assert.equal(decision.enabled, true)
  assert.equal(decision.ref, 'v1.0')
})

test('WHAT[HOST-BOUNDARY-017] AGENT_026_apply_preserves_other_mcp_servers', () => {
  const config = { mcp: { other: { type: 'remote', url: 'https://example.test' } } }
  StealthBrowserMcpConfigSurface.applyToConfig(config, read({}))
  assert.equal(config.mcp.other.url, 'https://example.test')
  assert.equal(config.mcp[serverName].type, 'local')
  assert.equal(config.mcp[serverName].enabled, true)
  assert.deepEqual(config.mcp[serverName].command, StealthBrowserMcpConfigSurface.uvxCommandFor('master'))
})

test('WHAT[HOST-BOUNDARY-017] AGENT_026_apply_injects_disabled_entry_when_test_mode', () => {
  const config = { mcp: {} }
  StealthBrowserMcpConfigSurface.applyToConfig(config, read({ WANXIANGSHU_TEST: '1' }))
  assert.equal(config.mcp[serverName].enabled, false)
  assert.equal(config.mcp[serverName].type, 'local')
})

test('WHAT[HOST-BOUNDARY-017] AGENT_026_entry_for_disabled_launch_is_not_enabled', () => {
  const entry = StealthBrowserMcpConfigSurface.entryFor(read({ STEALTH_BROWSER_MCP_DISABLED: '1' }))
  assert.equal(entry.enabled, false)
  assert.equal(entry.type, 'local')
  assert.ok(Array.isArray(entry.command), 'disabled entry still carries a command for structural consistency')
})

test('WHAT[HOST-BOUNDARY-017] AGENT_026_entry_for_uvx_launch_carries_uvx_command', () => {
  const entry = StealthBrowserMcpConfigSurface.entryFor(read({}))
  assert.equal(entry.enabled, true)
  assert.equal(entry.type, 'local')
  assert.equal(entry.command[0], 'uvx')
})
