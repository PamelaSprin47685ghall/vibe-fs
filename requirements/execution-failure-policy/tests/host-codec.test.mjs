import assert from 'node:assert/strict'
import test from 'node:test'

import * as signals from '../../../dist/OpenCode/Host/HostSignalSurface.js'

const sessionError = (error) => ({
  type: 'session.error',
  properties: { sessionID: 'session-1', error },
})

test('WHAT[EXECFAIL-001] Host adapter returns closed typed failures from structural evidence', () => {
  assert.equal(signals.tryDecode(sessionError({ name: 'TimeoutError', message: 'fatal wording' })).failure, 'ProviderTransient')
  assert.equal(signals.tryDecode(sessionError({ name: 'ProviderAuthError', message: 'retry wording' })).failure, 'ProviderPermanent')
  assert.equal(signals.tryDecode(sessionError({ name: 'PermissionDeniedError', message: 'retry wording' })).failure, 'AuthorizationDenied')
  assert.equal(signals.tryDecode(sessionError({ name: 'MessageAbortedError' })).failure, 'UserCancelled')
  assert.equal(signals.tryDecode(sessionError({ name: 'SupersededError' })).failure, 'Superseded')
})

test('WHAT[EXECFAIL-008] Host classification ignores diagnostic wording', () => {
  const transient = signals.tryDecode(sessionError({ name: 'TimeoutError', message: 'permission denied forever' }))
  const rewritten = signals.tryDecode(sessionError({ name: 'TimeoutError', message: 'please retry' }))
  assert.equal(transient.failure, 'ProviderTransient')
  assert.equal(rewritten.failure, transient.failure)
  assert.notEqual(rewritten.diagnostic, transient.diagnostic)
})
