import assert from 'node:assert/strict'
import { readFile, readdir } from 'node:fs/promises'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import * as eventStore from '../../../dist/Persistence/EventStore/Surface.js'

const id = (n) => n.toString(16).padStart(40, '0')

const event = (id, parents = []) => ({
  id,
  stream: 'append/law',
  type: 'JobRequested',
  parents,
  payload: { id },
  payloadRefs: [],
})

const withTemp = (fn) => {
  const base = mkdtempSync(join(tmpdir(), 'wxs-append-law-'))
  return fn(base)
}

test('WHAT[DURABLE-EVENTS-001] append_only_prior_writer_bytes_are_a_strict_prefix_after_new_fact', async () => {
  const dir = withTemp((base) => base)
  const store = eventStore.create(dir, 'append-law')
  try {
    const file = join(dir, 'wanxiang', 'events', 'append-law.ndjson')
    await eventStore.append(store, [event(id(1))])
    const before = await readFile(file)
    await eventStore.append(store, [event(id(2), [id(1)])])
    const after = await readFile(file)
    assert.equal(after.subarray(0, before.length).equals(before), true)
    assert.ok(after.length > before.length)
  } finally {
    eventStore.dispose(store)
    rmSync(dir, { recursive: true, force: true })
  }
})

test('WHAT[DURABLE-EVENTS-005] one_writer_is_one_file_regardless_of_history_size', async () => {
  const dir = withTemp((base) => base)
  const store = eventStore.create(dir, 'one-file-law')
  try {
    let parent = null
    for (let n = 1; n <= 128; n += 1) {
      const next = id(n)
      await eventStore.append(store, [event(next, parent ? [parent] : [])])
      parent = next
    }
    const files = await readdir(join(dir, 'wanxiang', 'events'))
    assert.deepEqual(files, ['one-file-law.ndjson'])
  } finally {
    eventStore.dispose(store)
    rmSync(dir, { recursive: true, force: true })
  }
})

test('WHAT[DURABLE-EVENTS-017] append_path_has_no_Git_object_or_ref_capability', async () => {
  const source = await readFile(new URL('../../../src/Wanxiangshu/Persistence/EventStore/Store.fs', import.meta.url), 'utf8')
  const log = await readFile(new URL('../../../src/Wanxiangshu/Persistence/EventStore/ProcessEventLog.fs', import.meta.url), 'utf8')
  for (const token of ['WriteBlob', 'WriteTree', 'ReadRef', 'CompareAndSwapRef', 'RootOid', 'ProcessGitRawStore']) {
    assert.equal(source.includes(token), false)
    assert.equal(log.includes(token), false)
  }
})

test('WHAT[DURABLE-EVENTS-006] duplicate_same_identity_is_idempotent_but_collision_is_rejected', async () => {
  const dir = withTemp((base) => base)
  const store = eventStore.create(dir, 'collision-law')
  try {
    const same = event(id(1))
    assert.equal((await eventStore.append(store, [same])).ok, true)
    assert.equal((await eventStore.append(store, [same])).ok, true)
    const conflict = { ...same, payload: { id: 'different' } }
    assert.equal((await eventStore.append(store, [conflict])).ok, false)
  } finally {
    eventStore.dispose(store)
    rmSync(dir, { recursive: true, force: true })
  }
})
