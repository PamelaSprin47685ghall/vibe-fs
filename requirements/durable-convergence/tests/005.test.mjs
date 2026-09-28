import assert from 'node:assert/strict'
import test from 'node:test'
import * as eventStore from '../../../dist/Persistence/EventStore/Surface.js'
import { event, withStore } from './support/events.mjs'

const left = event('a'.repeat(40))
const right = event('b'.repeat(40))
const partial = event('c'.repeat(40), [left.id])
const complete = { ...event('d'.repeat(40), [right.id, partial.id]), type: 'JobConflictResolved' }

test('WHAT[durable-convergence-005] partial parent coverage leaves multiple structural heads until all current heads are covered', async () => {
  await withStore(async store => {
    for (const value of [left, right, partial]) {
      const result = await eventStore.append(store, [value])
      assert.equal(result.ok, true, JSON.stringify(result.error))
    }
    assert.deepEqual(eventStore.heads(store, left.stream).sort(), [right.id, partial.id])
    assert.equal(eventStore.head(store, left.stream), null)
    const result = await eventStore.append(store, [complete])
    assert.equal(result.ok, true, JSON.stringify(result.error))
    assert.deepEqual(eventStore.heads(store, left.stream), [complete.id])
    assert.equal(eventStore.head(store, left.stream), complete.id)
  })
})

test.todo('WHAT[durable-convergence-005] business resolution leaves DomainConflict only after the resolution and all competing heads are folded (GAP-151)')
