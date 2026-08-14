// HOST-CTX: raw Host event → (sessionId, agent) extraction and role resolution.

import assert from 'node:assert/strict'
import test from 'node:test'

const { read, roleOf } = await import('../../../dist/OpenCode/Host/HostSessionContext.js')
const { Role } = await import('../../../dist/Foundation/Roles.js')

test('HOST_CTX_roleOf_rejects_absent_and_blank_agents', () => {
  assert.equal(roleOf(null), undefined)
  assert.equal(roleOf(undefined), undefined)
  assert.equal(roleOf(''), undefined)
  assert.equal(roleOf('   '), undefined)
})

test('HOST_CTX_roleOf_resolves_managed_identity_and_rejects_aliases', () => {
  assert.equal(roleOf('fast-coder'), Role.Coder, 'fast-coder must resolve to the Coder role')
  assert.equal(roleOf('deep-reviewer'), Role.Reviewer, 'deep-reviewer must resolve to the Reviewer role')
  assert.equal(roleOf('build'), undefined, 'build alias stays rejected')
  assert.equal(roleOf('plan'), undefined, 'plan alias stays rejected')
  assert.equal(roleOf('not-an-agent'), undefined)
})

test('HOST_CTX_read_tolerates_null_and_shapeless_events', () => {
  assert.deepEqual(read(null), ['', undefined])
  assert.deepEqual(read(undefined), ['', undefined])
  assert.deepEqual(read({}), ['', undefined])
  assert.deepEqual(read({ event: null }), ['', undefined])
  assert.deepEqual(read({ event: {} }), ['', undefined])
})

test('HOST_CTX_read_prefers_properties_session_id', () => {
  const raw = { event: { properties: { sessionID: 'ses_props' }, sessionID: 'ses_event' } }
  assert.deepEqual(read(raw), ['ses_props', undefined], 'properties.sessionID wins over event.sessionID')

  const fallback = { event: { sessionID: 'ses_event' } }
  assert.deepEqual(read(fallback), ['ses_event', undefined], 'event.sessionID is the fallback')
})

test('HOST_CTX_read_accepts_a_bare_message_without_event_wrapper', () => {
  assert.deepEqual(read({ sessionID: 'ses_bare' }), ['ses_bare', undefined])
  assert.deepEqual(read({ properties: { sessionID: 'ses_top' } }), ['ses_top', undefined])
})

test('HOST_CTX_read_extracts_the_agent_only_from_properties_info', () => {
  const withAgent = { event: { properties: { sessionID: 'ses_a', info: { agent: 'fast-manager' } } } }
  assert.deepEqual(read(withAgent), ['ses_a', 'fast-manager'])

  const infoNoAgent = { event: { properties: { sessionID: 'ses_b', info: {} } } }
  assert.deepEqual(read(infoNoAgent), ['ses_b', undefined], 'info without agent yields no role')

  const noInfo = { event: { properties: { sessionID: 'ses_c' } } }
  assert.deepEqual(read(noInfo), ['ses_c', undefined])
})
