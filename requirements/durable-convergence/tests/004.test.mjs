import assert from 'node:assert/strict'
import test from 'node:test'
import * as eventStore from '../../../dist/Persistence/EventStore/Surface.js'
import { event, withStore } from './support/events.mjs'

const parent = event('f'.repeat(40))
const left = event('a'.repeat(40), [parent.id])
const right = event('b'.repeat(40), [parent.id])

test('WHAT[durable-convergence-004] a shared-parent fork retains both structural heads without storage rejection', async () => {
  await withStore(async store => {
    for (const value of [parent, left, right]) {
      const result = await eventStore.append(store, [value])
      assert.equal(result.ok, true, JSON.stringify(result.error))
    }
    assert.deepEqual(eventStore.heads(store, parent.stream).sort(), [left.id, right.id])
    assert.equal(eventStore.head(store, parent.stream), null)
    assert.deepEqual(eventStore.read(store, left.id), left)
    assert.deepEqual(eventStore.read(store, right.id), right)
  })
})

test.todo('WHAT[durable-convergence-004] registered business projections expose deterministic typed DomainConflict for each legal concurrent fork (GAP-151)')
