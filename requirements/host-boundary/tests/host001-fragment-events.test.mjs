import assert from 'node:assert/strict'
import test from 'node:test'
import * as HostSignalSurface from '../../../dist/OpenCode/Host/HostSignalSurface.js'

const SESSION = 'ses_frag'
const decode = (raw) => HostSignalSurface.tryDecode(raw) ?? undefined

test('WHAT[HOST-BOUNDARY-001] HOST_001_fragment_events_die_at_earliest_boundary', () => {
  const fragments = [
    { type: 'message.updated', properties: { sessionID: SESSION } },
    { type: 'part.delta', properties: { sessionID: SESSION } },
    { type: 'session.updated', properties: { sessionID: SESSION } },
    { type: 'chat.message', properties: { sessionID: SESSION } },
  ]
  assert.deepEqual(fragments.map(decode), [undefined, undefined, undefined, undefined])
})

test('WHAT[HOST-BOUNDARY-002] HOST_001_only_coarse_session_lifecycle_signals_cross_the_boundary', () => {
  const idle = decode({ type: 'session.status', properties: { sessionID: SESSION, status: { type: 'idle' } } })
  const dedicatedIdle = decode({ type: 'session.idle', properties: { sessionID: SESSION } })
  const retry = decode({ type: 'session.status', properties: { sessionID: SESSION, status: { type: 'retry', attempt: 2 } } })
  const deleted = decode({ type: 'session.deleted', properties: { sessionID: SESSION, parentID: 'root' } })
  const aborted = decode({ type: 'session.error', properties: { sessionID: SESSION, error: { name: 'AbortError' } } })
  assert.equal(idle.kind, 'SessionIdle')
  assert.equal(dedicatedIdle.kind, 'SessionIdle')
  assert.equal(retry.kind, 'ProviderRetry')
  assert.equal(deleted.kind, 'SessionDeleted')
  assert.equal(deleted.parentSessionId, 'root')
  assert.equal(aborted.kind, 'AttemptAborted')
})
