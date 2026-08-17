import assert from 'node:assert/strict'
import test from 'node:test'
import * as HostSessionContext from '../../../dist/OpenCode/Host/HostSessionContext.js'
import { roleName } from '../../../dist/Participant/Persona/RoleIdentity.js'

const roleOf = HostSessionContext.roleOf
const read = HostSessionContext.read
const labelOf = (role) => (role ? roleName(role) : undefined)

test('WHAT[HOST-BOUNDARY-017] HOST_CTX_roleOf_rejects_absent_and_blank_agents', () => {
  assert.equal(roleOf(null), undefined)
  assert.equal(roleOf(undefined), undefined)
  assert.equal(roleOf(''), undefined)
  assert.equal(roleOf('  '), undefined)
})

test('WHAT[HOST-BOUNDARY-017] HOST_CTX_roleOf_resolves_managed_identity_and_rejects_aliases', () => {
  assert.equal(labelOf(roleOf('fast-coder')), 'coder')
  assert.equal(labelOf(roleOf('deep-reviewer')), 'reviewer')
  assert.equal(roleOf('build'), undefined)
  assert.equal(roleOf('plan'), undefined)
})

test('WHAT[HOST-BOUNDARY-017] HOST_CTX_read_tolerates_null_and_shapeless_events', () => {
  assert.deepEqual(read(null), ['', undefined])
  assert.deepEqual(read(undefined), ['', undefined])
  assert.deepEqual(read({}), ['', undefined])
})

test('WHAT[HOST-BOUNDARY-017] HOST_CTX_read_prefers_properties_session_id', () => {
  const raw = { event: { properties: { sessionID: 'ses_props' }, sessionID: 'ses_event' } }
  assert.deepEqual(read(raw), ['ses_props', undefined])
})

test('WHAT[HOST-BOUNDARY-017] HOST_CTX_read_accepts_a_bare_message_without_event_wrapper', () => {
  assert.deepEqual(read({ sessionID: 'ses_bare' }), ['ses_bare', undefined])
  assert.deepEqual(read({ properties: { sessionID: 'ses_top' } }), ['ses_top', undefined])
})

test('WHAT[HOST-BOUNDARY-017] HOST_CTX_read_extracts_the_agent_only_from_properties_info', () => {
  assert.deepEqual(read({ event: { properties: { sessionID: 'ses_a', info: { agent: 'fast-manager' } } } }), ['ses_a', 'fast-manager'])
  assert.deepEqual(read({ event: { sessionID: 'ses_a', agent: 'fast-manager' } }), ['ses_a', undefined])
})
