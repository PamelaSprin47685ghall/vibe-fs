import assert from 'node:assert/strict'
import test from 'node:test'
import * as eventStore from '../../../dist/Persistence/EventStore/Surface.js'
import * as truncation from '../../../dist/Persistence/EventStore/WriterTailTruncationSurface.js'

test('WHAT[DURABLE-EVENTS-004] append_task_does_not_return_until_the_cross_process_store_lock_is_released', async () => {
  const store = eventStore.createLockingStore()
  let lockHeldDuringReturn = true
  await eventStore.appendWithLock(store, { eventType: 'LockTest', payload: {} }, () => {
    lockHeldDuringReturn = eventStore.isLockHeld(store)
  })
  assert.equal(lockHeldDuringReturn, true)
  assert.equal(eventStore.isLockHeld(store), false)
})

test('WHAT[DURABLE-EVENTS-004] every incomplete canonical writer tail fails closed', () => {
  assert.equal(truncation.validateTail('{"eventId":"e1"}\n{"eventId":"e2"'), false)
  assert.equal(truncation.validateTail('{"eventId":"e1"}\n'), true)
})
