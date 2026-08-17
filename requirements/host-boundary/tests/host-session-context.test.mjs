import assert from 'node:assert/strict'
import test from 'node:test'
import * as host from '../../../dist/OpenCode/Host/HostBoundarySurface.js'

test('WHAT[HOST-BOUNDARY-017] HOST_CTX_roleOf_rejects_absent_and_blank_agents', () => {
  assert.equal(host.roleOf(null), null)
  assert.equal(host.roleOf(undefined), null)
  assert.equal(host.roleOf(''), null)
  assert.equal(host.roleOf('  '), null)
})

test('WHAT[HOST-BOUNDARY-017] HOST_CTX_roleOf_resolves_managed_identity_and_rejects_aliases', () => {
  assert.equal(host.roleOf('fast-coder'), 'coder')
  assert.equal(host.roleOf('deep-reviewer'), 'reviewer')
  assert.equal(host.roleOf('build'), null)
  assert.equal(host.roleOf('plan'), null)
})

test('WHAT[HOST-BOUNDARY-017] HOST_CTX_read_tolerates_null_and_shapeless_events', () => {
  assert.deepEqual(host.sessionContext(null), { sessionId: '', agent: null })
  assert.deepEqual(host.sessionContext(undefined), { sessionId: '', agent: null })
  assert.deepEqual(host.sessionContext({}), { sessionId: '', agent: null })
})

test('WHAT[HOST-BOUNDARY-017] HOST_CTX_read_prefers_properties_session_id', () => {
  const raw = { event: { properties: { sessionID: 'ses_props' }, sessionID: 'ses_event' } }
  assert.deepEqual(host.sessionContext(raw), { sessionId: 'ses_props', agent: null })
})

test('WHAT[HOST-BOUNDARY-017] HOST_CTX_read_accepts_a_bare_message_without_event_wrapper', () => {
  assert.deepEqual(host.sessionContext({ sessionID: 'ses_bare' }), { sessionId: 'ses_bare', agent: null })
  assert.deepEqual(host.sessionContext({ properties: { sessionID: 'ses_top' } }), { sessionId: 'ses_top', agent: null })
})

test('WHAT[HOST-BOUNDARY-017] HOST_CTX_read_extracts_the_agent_only_from_properties_info', () => {
  assert.deepEqual(host.sessionContext({ event: { properties: { sessionID: 'ses_a', info: { agent: 'fast-manager' } } } }), { sessionId: 'ses_a', agent: 'fast-manager' })
  assert.deepEqual(host.sessionContext({ event: { sessionID: 'ses_a', agent: 'fast-manager' } }), { sessionId: 'ses_a', agent: null })
})
