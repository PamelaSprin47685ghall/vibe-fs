import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const merge = await import("../../../dist/Persistence/EventStore/MergeSurface.js");

const make = (id, parents = [], stream = 'merge/main', payload = {}) => ({
  id,
  stream,
  type: 'JobRequested',
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

test('WHAT[durable-convergence-001] set union never drops distinct events', () => {
  const result = merge.merge([
    ['writer-a', [make(A)]],
    ['writer-b', [make(B)]],
  ])
  assert.deepEqual(ids(result).sort(), [A, B].sort())
})
}

{
const { default: assert } = await import("node:assert/strict");
const { mkdirSync, mkdtempSync, rmSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const { default: test } = await import("node:test");
const eventStore = await import("../../../dist/Persistence/EventStore/Surface.js");
const merge = await import("../../../dist/Persistence/EventStore/MergeSurface.js");

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

test('WHAT[durable-convergence-001] set union never drops concurrent events', () => {
  const merged = merge.merge([
    ['writer-a', [make(A)]],
    ['writer-b', [make(B)]],
  ])
  assert.deepEqual(new Set(ids(merged)), new Set([A, B]))
})
}
