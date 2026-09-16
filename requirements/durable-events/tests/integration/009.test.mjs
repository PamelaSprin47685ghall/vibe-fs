import assert from 'node:assert/strict'
import test from 'node:test'
import * as store from '../../../../dist/Persistence/EventStore/Surface.js'

test('WHAT[DURABLE-EVENTS-009] local_EventStore_never_reads_or_rewrites_any_legacy_layout', () => {
  const s = store.createMemoryStore()
  assert.equal(store.hasLegacyMigrationLayer(s), false)
})
