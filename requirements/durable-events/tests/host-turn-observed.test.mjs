// tests/unit/execution/host-turn-observed.test.mjs — HostTurnObserved fact.
//
// Durable observation that a Host turn reached a terminal snapshot.
// Idempotent identity = SessionId + ProviderRun (when present). Fold is a
// no-op on LinkageProjection this batch; serialize/deserialize must work.

import assert from 'node:assert/strict'
import test from 'node:test'
import {
  caseOf,
  agentFactCaseOf,
  clockAt,
  envelope,
  fact,
  fold,
  idValue,
  journal,
  payloadOf,
  providerRun,
  sessionId,
  stream,
  utcOffset,
} from '../../verification-system/tests/support/domain.mjs'

const SESSION = sessionId('ses_obs')
const AT = utcOffset('2026-04-01T08:00:00Z')

test('WHAT[DURABLE-EVENTS-002] EXEC_HostTurnObserved_serializes_round_trip_with_provider_run', () => {
  const value = fact('HostTurnObserved', {
    SessionId: SESSION,
    ProviderRun: providerRun('run_abc'),
    ObservedAt: AT,
  })
  const line = journal.serializeFact(value)
  assert.equal(line.includes('HostTurnObserved'), true)
  assert.equal(line.includes('run_abc'), true)

  const decoded = journal.deserializeFact(line)
  assert.equal(decoded.ok, true, decoded.ok ? '' : decoded.error)
  assert.equal(agentFactCaseOf(payloadOf(decoded.value)), 'HostTurnObserved')
  // Full structure via re-serialize: field rename would change bytes.
  assert.equal(journal.serializeFact(decoded.value), line)

  const payload = payloadOf(payloadOf(payloadOf(decoded.value)))
  assert.equal(idValue.session(payload.SessionId), 'ses_obs')
  assert.equal(idValue.providerRun(payload.ProviderRun), 'run_abc')
})

test('WHAT[DURABLE-EVENTS-002] EXEC_HostTurnObserved_serializes_round_trip_without_provider_run', () => {
  const value = fact('HostTurnObserved', {
    SessionId: SESSION,
    ProviderRun: undefined,
    ObservedAt: AT,
  })
  const line = journal.serializeFact(value)
  const decoded = journal.deserializeFact(line)
  assert.equal(decoded.ok, true, decoded.ok ? '' : decoded.error)
  assert.equal(agentFactCaseOf(payloadOf(decoded.value)), 'HostTurnObserved')
  assert.equal(journal.serializeFact(decoded.value), line)
})

test('WHAT[DURABLE-EVENTS-002] EXEC_HostTurnObserved_fold_is_noop_on_agent_projection', () => {
  const observed = fact('HostTurnObserved', {
    SessionId: SESSION,
    ProviderRun: providerRun('run_1'),
    ObservedAt: AT,
  })
  const env = envelope({ seq: 1, stream: stream.session(SESSION), fact: observed, run: 'run_1' })
  const folded = fold.one(fold.empty, env)
  assert.equal(folded.ok, true, folded.ok ? '' : JSON.stringify(folded.error))
  // No session handle map created by HostTurnObserved alone.
  assert.equal(fold.session(folded.value, 'ses_obs'), undefined)
})

test('WHAT[DURABLE-EVENTS-002] EXEC_HostTurnObserved_identity_key_is_session_plus_provider_run', () => {
  // Dedupe lives in the future CompletionReactor; this batch asserts both
  // halves are present on the payload so the reactor can key on them.
  const withRun = fact('HostTurnObserved', {
    SessionId: sessionId('ses_a'),
    ProviderRun: providerRun('run_x'),
    ObservedAt: AT,
  })
  const sameKeyLater = fact('HostTurnObserved', {
    SessionId: sessionId('ses_a'),
    ProviderRun: providerRun('run_x'),
    ObservedAt: clockAt('2026-04-01T08:00:01Z'),
  })
  const differentRun = fact('HostTurnObserved', {
    SessionId: sessionId('ses_a'),
    ProviderRun: providerRun('run_y'),
    ObservedAt: AT,
  })

  const keyOf = (f) => {
    const p = payloadOf(payloadOf(payloadOf(f)))
    return `${idValue.session(p.SessionId)}|${idValue.providerRun(p.ProviderRun)}`
  }

  assert.equal(keyOf(withRun), 'ses_a|run_x')
  assert.equal(keyOf(sameKeyLater), keyOf(withRun))
  assert.equal(keyOf(differentRun), 'ses_a|run_y')
  assert.notEqual(keyOf(withRun), keyOf(differentRun))
})
