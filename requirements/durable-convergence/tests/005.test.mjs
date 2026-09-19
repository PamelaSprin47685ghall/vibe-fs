import assert from 'node:assert/strict'
import { mkdirSync, mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import * as eventStore from '../../../dist/Persistence/EventStore/Surface.js'
import * as merge from '../../../dist/Persistence/EventStore/MergeSurface.js'

const make = (id, parents = [], stream = 'replica/law', type = 'JobRequested', payload = {}) => ({
  id,
  stream,
  type,
  parents,
  payload,
  payloadRefs: [],
})

const ids = (result) => {
  assert.equal(result.ok, true, result.ok ? '' : JSON.stringify(result.error))
  return result.events.map((event) => event.id)
}

const A = 'a'.repeat(40)

const B = 'b'.repeat(40)

const C = 'c'.repeat(40)

const R = 'd'.repeat(40)

const withStore = async (writerId, fn) => {
  const root = mkdtempSync(join(tmpdir(), `wxs-replica-${writerId}-`))
  const commonDir = join(root, '.git')
  mkdirSync(commonDir, { recursive: true })
  const handle = eventStore.create(commonDir, writerId)
  try {
    await fn(handle)
  } finally {
    eventStore.dispose(handle)
    rmSync(root, { recursive: true, force: true })
  }
}

test('WHAT[durable-convergence-005] resolution with all competing heads collapses structural frontier', async () => {
  await withStore('writer-resolution', async (store) => {
    const a = make(A, [], 'replica/resolution')
    const b = make(B, [], 'replica/resolution')
    const resolution = make(R, [A, B], 'replica/resolution', 'JobConflictResolved', { winner: A })
    assert.equal((await eventStore.append(store, [a])).ok, true)
    assert.equal((await eventStore.append(store, [b])).ok, true)
    assert.equal((await eventStore.append(store, [resolution])).ok, true)
    assert.deepEqual(eventStore.heads(store, 'replica/resolution'), [R])
    assert.equal(eventStore.head(store, 'replica/resolution'), R)
  })
})
