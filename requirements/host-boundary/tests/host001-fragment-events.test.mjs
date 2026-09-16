import assert from 'node:assert/strict'
import test from 'node:test'
import * as HostSignalSurface from '../../../dist/OpenCode/Host/HostSignalSurface.js'

const SESSION = 'ses_frag'
const decode = (raw) => HostSignalSurface.tryDecode(raw) ?? undefined
const decodeExecutionEnd = (raw) => HostSignalSurface.tryDecodePhysicalExecutionEnd(raw) ?? undefined
const decodeStepEnd = (raw) => HostSignalSurface.tryDecodeProviderStepEnd(raw) ?? undefined

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

test('WHAT[HOST-BOUNDARY-001] HOST_001_terminal_message_identity_is_physical_capacity_evidence_not_a_business_signal', () => {
  const running = {
    type: 'message.updated',
    properties: {
      sessionID: SESSION,
      info: { role: 'assistant', parentID: 'msg-current', time: { created: 1 } },
    },
  }
  const toolCallStep = {
    type: 'message.updated',
    properties: {
      // Plugin Hooks.event is typed against the legacy SDK Event, where the
      // session identity lives on info. The decoder also accepts v2's outer
      // properties.sessionID shape.
      info: {
        sessionID: SESSION,
        id: 'run-tool-call',
        role: 'assistant',
        parentID: 'msg-current',
        time: { created: 1, completed: 2 },
        finish: 'tool-calls',
      },
    },
  }
  const completed = {
    type: 'message.updated',
    properties: {
      info: {
        sessionID: SESSION,
        id: 'run-final',
        role: 'assistant',
        parentID: 'msg-current',
        time: { created: 1, completed: 3 },
        finish: 'stop',
      },
    },
  }
  const failed = {
    type: 'message.updated',
    properties: {
      info: {
        sessionID: SESSION,
        id: 'run-failed',
        role: 'assistant',
        parentID: 'msg-current',
        time: { created: 1, completed: 2 },
        error: { name: 'ProviderError' },
      },
    },
  }

  assert.equal(decode(running), undefined)
  assert.equal(decode(toolCallStep), undefined)
  assert.equal(decode(completed), undefined, 'message.updated still never becomes a business HostSignal')
  assert.equal(decodeExecutionEnd(running), undefined)
  assert.deepEqual(decodeStepEnd(toolCallStep), {
    sessionId: SESSION,
    physicalUserMessageId: 'msg-current',
    providerRun: 'run-tool-call',
  }, 'tool-calls ends the provider step even though the physical execution continues')
  assert.equal(
    decodeExecutionEnd(toolCallStep),
    undefined,
    'a completed tool-call provider step is not the end of the physical user execution',
  )
  assert.deepEqual(decodeExecutionEnd(completed), {
    sessionId: SESSION,
    physicalUserMessageId: 'msg-current',
  })
  assert.deepEqual(decodeStepEnd(completed), {
    sessionId: SESSION,
    physicalUserMessageId: 'msg-current',
    providerRun: 'run-final',
  })
  assert.deepEqual(decodeStepEnd(failed), {
    sessionId: SESSION,
    physicalUserMessageId: 'msg-current',
    providerRun: 'run-failed',
  })
  assert.equal(decodeExecutionEnd(failed), undefined)
  assert.equal(
    decodeExecutionEnd({ type: 'session.idle', properties: { sessionID: SESSION } }),
    undefined,
    'coarse idle has no physical identity and therefore cannot own capacity release',
  )
})
