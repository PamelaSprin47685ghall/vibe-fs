import assert from 'node:assert/strict'
import test from 'node:test'
import { contextAttachAbort, contextDecode } from '../../../dist/OpenCode/Codec/ToolHostSurface.js'

test('WHAT[HOST-BOUNDARY-009] HOST_abort_callback_fires_once_immediately_or_from_the_registered_unit_listener', () => {
  let immediate = 0
  contextAttachAbort(contextDecode({ sessionID: 'immediate', abort: { aborted: true, addEventListener() {}, removeEventListener() {} } }), () => { immediate += 1 })
  assert.equal(immediate, 1)

  let registration
  const removed = []
  const signal = {
    aborted: false,
    addEventListener: (name, listener, options) => { registration = { name, listener, options } },
    removeEventListener: (name, listener) => removed.push({ name, listener }),
  }
  let deferred = 0
  const unsubscribe = contextAttachAbort(contextDecode({ sessionID: 'deferred', abortSignal: signal }), () => { deferred += 1 })
  assert.deepEqual({ name: registration.name, once: registration.options.once }, { name: 'abort', once: true })
  registration.listener()
  assert.equal(deferred, 1)
  unsubscribe()
  assert.deepEqual(removed, [{ name: 'abort', listener: registration.listener }])
})
