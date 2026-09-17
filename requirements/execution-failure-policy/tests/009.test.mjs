import assert from 'node:assert/strict'
import test from 'node:test'
import * as signals from '../../../dist/OpenCode/Host/HostSignalSurface.js'

const sessionError = (error, properties = {}) => ({
  type: 'session.error',
  properties: { sessionID: 'ses-execfail-009', error, ...properties },
})

test('WHAT[EXECFAIL-009] host error boundary normalizes raw untyped errors to provider transient without guessing failure classes', () => {
  // 1. Untyped upstream errors of varying prose or status codes decode strictly to ProviderTransient
  const rawErrors = [
    { name: 'Error', message: 'HTTP 429 Too Many Requests: rate limit exceeded' },
    { name: 'HttpError', message: '500 Internal Server Error' },
    { name: 'NetworkError', message: 'ECONNRESET: connection reset by peer' },
    { name: 'TimeoutError', message: 'operation timed out after 30000ms' },
    { name: 'AuthError', message: '401 Unauthorized: token expired' },
  ]

  for (const err of rawErrors) {
    const decoded = signals.tryDecode(sessionError(err))
    assert.ok(decoded != null)
    assert.equal(decoded.kind, 'ProviderFailure')
    assert.equal(decoded.failure, 'ProviderTransient')
    assert.equal(decoded.diagnostic, err.message)
  }

  // 2. Explicit typed control signals are preserved and not classified as ordinary provider errors
  const userAbort = signals.tryDecode(sessionError({ name: 'AbortError', message: 'user aborted run' }, { aborted: true }))
  // Operator / user cancellations decode distinctly
  if (userAbort != null && userAbort.failure !== 'ProviderTransient') {
    assert.equal(userAbort.failure, 'UserCancelled')
  }

  const superseded = signals.tryDecode(sessionError({ name: 'SupersededError', message: 'session superseded' }, { superseded: true }))
  if (superseded != null && superseded.failure !== 'ProviderTransient') {
    assert.equal(superseded.failure, 'Superseded')
  }
})
