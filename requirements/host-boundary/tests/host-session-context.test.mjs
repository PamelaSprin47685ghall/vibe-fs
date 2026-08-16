import assert from 'node:assert/strict'
import test from 'node:test'
import { hostContext } from './support/host-surface.mjs'

test('WHAT[HOST-BOUNDARY-017] HOST_CTX_roleOf_rejects_absent_and_blank_agents', () => {
  assert.equal(hostContext.roleOf(null), undefined)
  assert.equal(hostContext.roleOf(undefined), undefined)
  assert.equal(hostContext.roleOf(''), undefined)
  assert.equal(hostContext.roleOf('  '), undefined)
})

test('WHAT[HOST-BOUNDARY-017] HOST_CTX_roleOf_resolves_managed_identity_and_rejects_aliases', () => {
  assert.equal(hostContext.roleOf('fast-coder'), 'Coder')
  assert.equal(hostContext.roleOf('deep-reviewer'), 'Reviewer')
  assert.equal(hostContext.roleOf('build'), undefined)
  assert.equal(hostContext.roleOf('plan'), undefined)
})

test('WHAT[HOST-BOUNDARY-017] HOST_CTX_read_tolerates_null_and_shapeless_events', () => {
  assert.deepEqual(hostContext.read(null), ['', undefined])
  assert.deepEqual(hostContext.read(undefined), ['', undefined])
  assert.deepEqual(hostContext.read({}), ['', undefined])
})

test('WHAT[HOST-BOUNDARY-017] HOST_CTX_read_prefers_properties_session_id', () => {
  const raw = { event: { properties: { sessionID: 'ses_props' }, sessionID: 'ses_event' } }
  assert.deepEqual(hostContext.read(raw), ['ses_props', undefined])
})

test('WHAT[HOST-BOUNDARY-017] HOST_CTX_read_accepts_a_bare_message_without_event_wrapper', () => {
  assert.deepEqual(hostContext.read({ sessionID: 'ses_bare' }), ['ses_bare', undefined])
  assert.deepEqual(hostContext.read({ properties: { sessionID: 'ses_top' } }), ['ses_top', undefined])
})

test('WHAT[HOST-BOUNDARY-017] HOST_CTX_read_extracts_the_agent_only_from_properties_info', () => {
  assert.deepEqual(hostContext.read({ event: { properties: { sessionID: 'ses_a', info: { agent: 'fast-manager' } } } }), ['ses_a', 'fast-manager'])
  assert.deepEqual(hostContext.read({ event: { sessionID: 'ses_a', agent: 'fast-manager' } }), ['ses_a', undefined])
})
