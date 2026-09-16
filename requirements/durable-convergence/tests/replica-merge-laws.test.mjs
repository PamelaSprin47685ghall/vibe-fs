// FROZEN — 2026-08-14. Replica convergence laws over the public EventStore merge
// owner and EventStore structural Current. Intentionally NOT executed before implementation.

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

test('WHAT[DURABLE-CONVERGENCE-001] set union never drops concurrent events', () => {
  const merged = merge.merge([
    ['writer-a', [make(A)]],
    ['writer-b', [make(B)]],
  ])
  assert.deepEqual(new Set(ids(merged)), new Set([A, B]))
})

test('WHAT[DURABLE-CONVERGENCE-002] merge is commutative associative idempotent at writer stream level', () => {
  const sa = ['writer-a', [make(A)]]
  const sb = ['writer-b', [make(B)]]
  const sc = ['writer-c', [make(C)]]
  const abc = ids(merge.merge([sa, sb, sc]))
  const cba = ids(merge.merge([sc, sb, sa]))
  assert.deepEqual(abc, cba)
  assert.deepEqual(ids(merge.merge([sa, ['copy', [make(A)]]])), [A])
})

test('WHAT[DURABLE-CONVERGENCE-006] convergence is a function of event truth not arrival wall clock', () => {
  const streams1 = [
    ['writer-a', [make(A), make(C, [A])]],
    ['writer-b', [make(B)]],
  ]
  const streams2 = [
    ['writer-b', [make(B)]],
    ['writer-a', [make(A), make(C, [A])]],
  ]
  assert.deepEqual(ids(merge.merge(streams1)), ids(merge.merge(streams2)))
})

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

test('WHAT[DURABLE-CONVERGENCE-005] resolution with all competing heads collapses structural frontier', async () => {
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
