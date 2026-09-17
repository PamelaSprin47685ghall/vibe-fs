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

test('WHAT[DURABLE-CONVERGENCE-004] concurrent heads are preserved as structural DomainConflict frontier', async () => {
  await withStore('writer-conflict', async (store) => {
    const a = make(A, [], 'replica/conflict')
    const b = make(B, [], 'replica/conflict')
    assert.equal((await eventStore.append(store, [a])).ok, true)
    assert.equal((await eventStore.append(store, [b])).ok, true)
    assert.deepEqual(eventStore.heads(store, 'replica/conflict').sort(), [A, B].sort())
    assert.equal(eventStore.head(store, 'replica/conflict') == null, true, 'conflict must not masquerade as one linear head')
  })
})
