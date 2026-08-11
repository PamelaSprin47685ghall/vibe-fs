// tests/unit/Verify/host001-fragment-events.test.mjs — HOST-001 event layering.
//
// Fragment events must be dropped at the codec boundary. Coarse session
// lifecycle signals, including typed attempt abort, cross into the reconciler.

import assert from 'node:assert/strict'
import test from 'node:test'
import { hostSignals, caseOf, idValue, payloadOf } from '../support/domain.mjs'

const SESSION = 'ses_frag'

// ── HOST-001: fragment events die at the earliest boundary ────────────────────

test('HOST_001_fragment_events_die_at_earliest_boundary', () => {
  const fragments = [
    { type: 'message.updated', properties: { sessionID: SESSION, message: { id: 'msg_1' } } },
    { type: 'part.delta', properties: { sessionID: SESSION, delta: { text: 'x' } } },
    { type: 'session.updated', properties: { sessionID: SESSION } },
    { type: 'chat.message', properties: { sessionID: SESSION, message: { id: 'msg_2' } } },
  ]

  for (const raw of fragments) {
    assert.equal(hostSignals.isHostSignalEvent(raw), false, `${raw.type} must not look like a host signal`)
    assert.equal(hostSignals.tryDecode(raw), undefined, `${raw.type} must be dropped at the codec`)
  }
})

test('HOST_001_only_coarse_session_lifecycle_signals_cross_the_boundary', () => {
  const idle = hostSignals.tryDecode({
    type: 'session.status',
    properties: { sessionID: SESSION, status: { type: 'idle' } },
  })
  assert.equal(caseOf(idle), 'SessionIdle')
  assert.equal(idValue.session(payloadOf(idle)), SESSION)

  const retry = hostSignals.tryDecode({
    type: 'session.status',
    properties: { sessionID: SESSION, status: { type: 'retry', attempt: 3, message: 'rate limited' } },
  })
  assert.equal(caseOf(retry), 'ProviderRetry')
  const retrySignal = payloadOf(retry)
  assert.equal(idValue.session(retrySignal.SessionId), SESSION)
  assert.equal(retrySignal.Attempt, '3')
  assert.equal(retrySignal.Reason, 'rate limited')

  const deleted = hostSignals.tryDecode({
    type: 'session.deleted',
    properties: { sessionID: SESSION },
  })
  assert.equal(caseOf(deleted), 'SessionDeleted')
  const deletedSignal = payloadOf(deleted)
  assert.equal(idValue.session(deletedSignal[0]), SESSION)
  assert.equal(deletedSignal[1], undefined, 'missing Host parentID must remain None')

  const deletedChild = hostSignals.tryDecode({
    type: 'session.deleted',
    properties: { sessionID: SESSION, info: { parentID: 'ses_parent' } },
  })
  assert.equal(caseOf(deletedChild), 'SessionDeleted')
  const deletedChildSignal = payloadOf(deletedChild)
  assert.equal(idValue.session(deletedChildSignal[0]), SESSION)
  assert.equal(idValue.session(deletedChildSignal[1]), 'ses_parent')

  const error = hostSignals.tryDecode({
    type: 'session.error',
    properties: { sessionID: SESSION, error: { name: 'ProviderError', message: 'broken' } },
  })
  assert.equal(caseOf(error), 'ProviderFailure')
  assert.equal(idValue.session(payloadOf(error)[0]), SESSION)
  assert.equal(payloadOf(error)[1], 'broken')

  // Abort is a physical attempt fact, never a provider failure and never dropped.
  const abort = hostSignals.tryDecode({
    type: 'session.error',
    properties: { sessionID: SESSION, error: { name: 'MessageAbortedError' } },
  })
  assert.equal(caseOf(abort), 'AttemptAborted')
  assert.equal(idValue.session(payloadOf(abort)), SESSION)
})
