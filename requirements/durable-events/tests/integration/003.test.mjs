import assert from 'node:assert/strict'
import test from 'node:test'
import * as identityCollision from '../../../../dist/Persistence/EventStore/IdentityCollisionSurface.js'

test('WHAT[DURABLE-EVENTS-003] same_EventId_different_canonical_bytes_fail_closed', () => {
  assert.throws(
    () => identityCollision.checkCollision({ eventId: 'e1', bytes: 'bytes-a' }, { eventId: 'e1', bytes: 'bytes-b' }),
    /identity collision fail closed/,
  )
})

test('WHAT[DURABLE-EVENTS-003] same_EventId_same_canonical_bytes_dedupe_ok', () => {
  const res = identityCollision.checkCollision({ eventId: 'e1', bytes: 'bytes-same' }, { eventId: 'e1', bytes: 'bytes-same' })
  assert.equal(res.action, 'Dedupe')
})

test('WHAT[DURABLE-EVENTS-003] distinct_EventIds_are_both_retained', () => {
  const res = identityCollision.checkCollision({ eventId: 'e1', bytes: 'b1' }, { eventId: 'e2', bytes: 'b2' })
  assert.equal(res.action, 'RetainBoth')
})
