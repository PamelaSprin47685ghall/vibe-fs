import assert from 'node:assert/strict'
import test from 'node:test'
import * as store from '../../../dist/Persistence/EventStore/Surface.js'
import * as timing from '../../../dist/Persistence/PortObservationTimingSurface.js'
import * as sub from '../../../dist/Persistence/JournalSubscriptionSurface.js'
import * as revisionSurface from '../../../dist/Persistence/Journal/RevisionSurface.js'

test('WHAT[DURABLE-EVENTS-013] physical append failure leaves event and structural Current unchanged', async () => {
  const s = store.createFailingAppendStore()
  const initialCurrent = store.currentIntegrated(s)
  await assert.rejects(() => store.appendEvent(s, { eventType: 'Fact', payload: {} }), /physical disk error/)
  assert.deepEqual(store.currentIntegrated(s), initialCurrent)
})

test('WHAT[DURABLE-EVENTS-013] port_members_read_journal_at_call_time', () => {
  const p = timing.createPort()
  assert.equal(timing.readsCurrentOnCall(p), true)
})

test('WHAT[DURABLE-EVENTS-013] journal_subscription_notifies_after_commit_only', () => {
  const s = sub.createSubscription()
  let notified = false
  sub.onCommit(s, () => { notified = true })
  assert.equal(notified, false)
  sub.simulateCommit(s)
  assert.equal(notified, true)
})
