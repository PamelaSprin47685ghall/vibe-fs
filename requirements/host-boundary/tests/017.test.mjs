import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const EventsSurface = await import("../../../dist/OpenCode/Host/EventsSurface.js");

const notify = (port, sessionId, outcome) => EventsSurface.notify(port, sessionId, outcome.kind, outcome.providerRun ?? '', outcome.error ?? outcome.value ?? '')
const completed = (providerRun = '') => ({ kind: 'Completed', providerRun })
const failed = (error) => ({ kind: 'Failed', error })
const aborted = (reason) => ({ kind: 'Aborted', error: reason })

test('WHAT[host-boundary-017] HOST_CTX_notifyCompleted_rejects_unknown_or_blank_roles', () => {
  const port = EventsSurface.create()
  const received = []
  EventsSurface.subscribe(port, (_, outcome) => received.push(outcome.kind))

  assert.equal(EventsSurface.notifyCompleted(port, 'ses_role', 'done', 'done', 'coder'), true)
  assert.equal(EventsSurface.notifyCompleted(port, 'ses_role', 'ignored', 'ignored', 'reviewer'), false)
  assert.equal(EventsSurface.notifyCompleted(port, 'ses_role', 'ignored', 'ignored', 'unknown-role'), false)
  assert.equal(EventsSurface.notifyCompleted(port, 'ses_role', 'ignored', 'ignored', ''), false)
  assert.equal(EventsSurface.notifyCompleted(port, 'ses_role', 'ignored', 'ignored', null), false)
  assert.deepEqual(received, ['Completed'])
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const HostSessionContext = await import("../../../dist/OpenCode/Host/HostSessionContextSurface.js");

const roleOf = HostSessionContext.roleOf
const read = HostSessionContext.read
const labelOf = (role) => role ?? undefined

test('WHAT[host-boundary-017] HOST_CTX_roleOf_rejects_absent_and_blank_agents', () => {
  assert.equal(roleOf(null), undefined)
  assert.equal(roleOf(undefined), undefined)
  assert.equal(roleOf(''), undefined)
  assert.equal(roleOf('  '), undefined)
})
test('WHAT[host-boundary-017] HOST_CTX_roleOf_resolves_managed_identity_and_rejects_aliases', () => {
  assert.equal(labelOf(roleOf('coder')), 'coder')
  assert.equal(labelOf(roleOf('inspector')), 'inspector')
  assert.equal(roleOf('build'), undefined)
  assert.equal(roleOf('plan'), undefined)
  assert.equal(roleOf('reviewer'), undefined)
})
test('WHAT[host-boundary-017] HOST_CTX_read_tolerates_null_and_shapeless_events', () => {
  assert.deepEqual(read(null), ['', undefined])
  assert.deepEqual(read(undefined), ['', undefined])
  assert.deepEqual(read({}), ['', undefined])
})
test('WHAT[host-boundary-017] HOST_CTX_read_prefers_properties_session_id', () => {
  const raw = { event: { properties: { sessionID: 'ses_props' }, sessionID: 'ses_event' } }
  assert.deepEqual(read(raw), ['ses_props', undefined])
})
test('WHAT[host-boundary-017] HOST_CTX_read_accepts_a_bare_message_without_event_wrapper', () => {
  assert.deepEqual(read({ sessionID: 'ses_bare' }), ['ses_bare', undefined])
  assert.deepEqual(read({ properties: { sessionID: 'ses_top' } }), ['ses_top', undefined])
})
test('WHAT[host-boundary-017] HOST_CTX_read_extracts_the_agent_only_from_properties_info', () => {
  assert.deepEqual(read({ event: { properties: { sessionID: 'ses_a', info: { agent: 'manager' } } } }), ['ses_a', 'manager'])
  assert.deepEqual(read({ event: { sessionID: 'ses_a', agent: 'manager' } }), ['ses_a', undefined])
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const SphinxMcpConfigSurface = await import("../../../dist/OpenCode/Host/SphinxMcpConfigSurface.js");

const serverName = SphinxMcpConfigSurface.serverIdentity()
const read = (env) => (name) => env[name] ?? undefined

test('WHAT[host-boundary-017] AGENT_030_launch_default_is_local_node_entry', () => {
  const decision = SphinxMcpConfigSurface.launchDecision(read({}))
  assert.equal(decision.kind, 'local')
  assert.equal(decision.enabled, true)
  assert.equal(decision.reason, 'enabled')
  assert.ok(decision.path, 'local launch must carry a server entry path')
})
test('WHAT[host-boundary-017] AGENT_030_launch_disabled_when_env_set', () => {
  const decision = SphinxMcpConfigSurface.launchDecision(read({ SPHINX_MCP_DISABLED: '1' }))
  assert.equal(decision.kind, 'disabled')
  assert.equal(decision.enabled, false)
  assert.equal(decision.reason, 'disabled')
})
test('WHAT[host-boundary-017] AGENT_030_launch_fixture_when_fixture_env_set', () => {
  const decision = SphinxMcpConfigSurface.launchDecision(read({ SPHINX_MCP_FIXTURE: '/path/to/fixture.js' }))
  assert.equal(decision.kind, 'fixture')
  assert.equal(decision.enabled, true)
  assert.equal(decision.path, '/path/to/fixture.js')
  assert.equal(decision.reason, 'fixture')
})
test('WHAT[host-boundary-017] AGENT_030_launch_disabled_in_test_mode', () => {
  const decision = SphinxMcpConfigSurface.launchDecision(read({ WANXIANGSHU_TEST: '1' }))
  assert.equal(decision.kind, 'disabled')
  assert.equal(decision.enabled, false)
})
test('WHAT[host-boundary-017] AGENT_030_apply_preserves_other_mcp_servers', () => {
  const config = { mcp: { other: { type: 'remote', url: 'https://example.test' } } }
  SphinxMcpConfigSurface.applyToConfig(config, read({}))
  assert.equal(config.mcp.other.url, 'https://example.test')
  assert.equal(config.mcp[serverName].type, 'local')
  assert.equal(config.mcp[serverName].enabled, true)
  assert.deepEqual(config.mcp[serverName].command, ['node', SphinxMcpConfigSurface.launchDecision(read({})).path])
})
test('WHAT[host-boundary-017] AGENT_030_apply_injects_disabled_entry_when_test_mode', () => {
  const config = { mcp: {} }
  SphinxMcpConfigSurface.applyToConfig(config, read({ WANXIANGSHU_TEST: '1' }))
  assert.equal(config.mcp[serverName].enabled, false)
  assert.equal(config.mcp[serverName].type, 'local')
})
test('WHAT[host-boundary-017] AGENT_030_entry_for_disabled_launch_is_not_enabled', () => {
  const entry = SphinxMcpConfigSurface.entryFor(read({ SPHINX_MCP_DISABLED: '1' }))
  assert.equal(entry.enabled, false)
  assert.equal(entry.type, 'local')
  assert.ok(Array.isArray(entry.command), 'disabled entry still carries a command for structural consistency')
})
test('WHAT[host-boundary-017] AGENT_030_entry_for_local_launch_carries_node_command', () => {
  const entry = SphinxMcpConfigSurface.entryFor(read({}))
  assert.equal(entry.enabled, true)
  assert.equal(entry.type, 'local')
  assert.equal(entry.command[0], 'node')
})
}
