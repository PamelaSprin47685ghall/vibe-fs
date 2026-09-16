import assert from 'node:assert/strict'
import { mkdirSync, mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import * as eventStore from '../../../dist/Persistence/EventStore/Surface.js'
import * as merge from '../../../dist/Persistence/EventStore/MergeSurface.js'

const make = (id, parents = [], stream = 'merge/main', type = 'JobRequested', payload = {}) => ({
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

test('WHAT[DURABLE-CONVERGENCE-001] set union never drops distinct events', () => {
  const result = merge.merge([
    ['writer-a', [make(A)]],
    ['writer-b', [make(B)]],
  ])
  assert.deepEqual(ids(result).sort(), [A, B].sort())
})

test('WHAT[DURABLE-CONVERGENCE-001] set union never drops concurrent events', () => {
  const merged = merge.merge([
    ['writer-a', [make(A, [], 'replica/law')]],
    ['writer-b', [make(B, [], 'replica/law')]],
  ])
  assert.deepEqual(new Set(ids(merged)), new Set([A, B]))
})
