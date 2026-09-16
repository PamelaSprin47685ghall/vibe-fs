// HostTurnObserved is a durable terminal observation. Its identity is the
// session plus provider run when present; folding it alone does not create a
// linkage projection entry.

import assert from 'node:assert/strict'
import test from 'node:test'

import * as hostTurn from '../../../dist/Execution/Delegation/HostTurnObservedSurface.js'

const observed = (overrides = {}) => ({
  SessionId: 'ses_obs',
  ProviderRun: 'run_abc',
  ObservedAt: '2026-04-01T08:00:00Z',
  ...overrides,
})

test('WHAT[DURABLE-EVENTS-002] EXEC_HostTurnObserved_serializes_round_trip_with_provider_run', () => {
  const line = hostTurn.serialize(observed())
  assert.equal(line.includes('HostTurnObserved'), true)
  assert.equal(line.includes('run_abc'), true)

  const decoded = hostTurn.deserialize(line)
  assert.equal(decoded.ok, true, decoded.ok ? '' : decoded.error)
  assert.equal(decoded.case, 'HostTurnObserved')
  assert.equal(decoded.sessionId, 'ses_obs')
  assert.equal(decoded.providerRun, 'run_abc')
  assert.equal(decoded.line, line)
})

test('WHAT[DURABLE-EVENTS-002] EXEC_HostTurnObserved_serializes_round_trip_without_provider_run', () => {
  const value = observed({ ProviderRun: null })
  const line = hostTurn.serialize(value)
  const decoded = hostTurn.deserialize(line)
  assert.equal(decoded.ok, true, decoded.ok ? '' : decoded.error)
  assert.equal(decoded.case, 'HostTurnObserved')
  assert.equal(decoded.providerRun == null, true)
  assert.equal(decoded.line, line)
})

test('WHAT[DURABLE-EVENTS-002] EXEC_HostTurnObserved_fold_is_noop_on_agent_projection', () => {
  const folded = hostTurn.foldNoop(observed({ ProviderRun: 'run_1' }))
  assert.equal(folded.ok, true, folded.ok ? '' : JSON.stringify(folded.error))
  assert.equal(folded.hasSession, false)
})

test('WHAT[DURABLE-EVENTS-002] EXEC_HostTurnObserved_identity_key_is_session_plus_provider_run', () => {
  const withRun = observed({ SessionId: 'ses_a', ProviderRun: 'run_x' })
  const sameKeyLater = observed({ SessionId: 'ses_a', ProviderRun: 'run_x', ObservedAt: '2026-04-01T08:00:01Z' })
  const differentRun = observed({ SessionId: 'ses_a', ProviderRun: 'run_y' })

  assert.equal(hostTurn.identityKey(withRun), 'ses_a|run_x')
  assert.equal(hostTurn.identityKey(sameKeyLater), hostTurn.identityKey(withRun))
  assert.equal(hostTurn.identityKey(differentRun), 'ses_a|run_y')
  assert.notEqual(hostTurn.identityKey(withRun), hostTurn.identityKey(differentRun))
})
