import assert from 'node:assert/strict'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import * as eventStore from '../../../dist/Persistence/EventStore/Surface.js'
import * as eventMerge from '../../../dist/Persistence/EventStore/MergeSurface.js'

const A = 'a'.repeat(40)

const B = 'b'.repeat(40)

const C = 'c'.repeat(40)

const D = 'd'.repeat(40)

const make = ({
  id,
  stream = 'job/main',
  eventType = 'JobRequested',
  parents = [],
  payload = { status: 'open' },
  payloadRefs = [],
} = {}) => ({
  id,
  stream,
  type: eventType,
  parents,
  payload,
  payloadRefs,
})

const withTemp = (fn) => {
  const base = mkdtempSync(join(tmpdir(), 'wxs-event-store-fold-'))
  return fn(base)
}

test('WHAT[durable-events-008] DURABLE_EVENTS_008_concurrent_heads_remain_distinct_in_structural_Current', async () => {
  const dir = withTemp((base) => base)
  const store = eventStore.create(dir, 'conflict-proof')
  try {
    assert.equal((await eventStore.append(store, [make({ id: A, stream: 'job/conflict' })])).ok, true)
    assert.equal((await eventStore.append(store, [make({ id: B, stream: 'job/conflict' })])).ok, true)

    assert.deepEqual(eventStore.heads(store, 'job/conflict').sort(), [A, B])
    assert.equal(eventStore.head(store, 'job/conflict'), null, 'a fork must not masquerade as a unique head')
  } finally {
    eventStore.dispose(store)
    rmSync(dir, { recursive: true, force: true })
  }
})

test('WHAT[durable-events-008] DURABLE_EVENTS_008_resolution_naming_all_heads_collapses_structural_Current', async () => {
  const dir = withTemp((base) => base)
  const store = eventStore.create(dir, 'resolution-proof')
  try {
    assert.equal((await eventStore.append(store, [make({ id: A, stream: 'job/resolution' })])).ok, true)
    assert.equal((await eventStore.append(store, [make({ id: B, stream: 'job/resolution' })])).ok, true)
    assert.equal(
      (await eventStore.append(store, [make({ id: D, stream: 'job/resolution', eventType: 'JobConflictResolved', parents: [A, B] })])).ok,
      true,
    )

    assert.deepEqual(eventStore.heads(store, 'job/resolution'), [D])
    assert.equal(eventStore.head(store, 'job/resolution'), D)
  } finally {
    eventStore.dispose(store)
    rmSync(dir, { recursive: true, force: true })
  }
})
