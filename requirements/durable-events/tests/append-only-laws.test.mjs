// FROZEN — 2026-08-14. Shock-cut append-only laws for process NDJSON.
// Intentionally NOT executed before implementation.

import assert from 'node:assert/strict'
import { readFile, readdir } from 'node:fs/promises'
import test from 'node:test'
import path from 'node:path'
import { eventId, resultOf, toList } from '../../verification-system/tests/support/domain.mjs'
import { createLocalEventStore } from '../../verification-system/tests/support/local-event-store.mjs'

const Domain = await import('../../../dist/Persistence/EventStore/Model.js')
const streamId = (v) => Domain.EventStreamIdModule_create(v)
const event = (id, parents = []) => new Domain.EventEnvelope(eventId(id), streamId('append/law'), 'JobRequested', toList(parents.map(eventId)), { id }, toList([]))

const id = (n) => n.toString(16).padStart(40, '0')

test('WHAT[DURABLE-EVENTS-001] append_only_prior_writer_bytes_are_a_strict_prefix_after_new_fact', async () => {
  const local = createLocalEventStore({ writerId: 'append-law' })
  try {
    assert.equal(resultOf(await local.store.Append(toList([event(id(1))]))).ok, true)
    const file = path.join(local.commonDir, 'wanxiang', 'events', 'append-law.ndjson')
    const before = await readFile(file)
    assert.equal(resultOf(await local.store.Append(toList([event(id(2), [id(1)])]))).ok, true)
    const after = await readFile(file)
    assert.equal(after.subarray(0, before.length).equals(before), true)
    assert.ok(after.length > before.length)
  } finally {
    local.close()
  }
})

test('WHAT[DURABLE-EVENTS-005] one_writer_is_one_file_regardless_of_history_size', async () => {
  const local = createLocalEventStore({ writerId: 'one-file-law' })
  try {
    let parent = null
    for (let n = 1; n <= 128; n += 1) {
      const next = id(n)
      assert.equal(resultOf(await local.store.Append(toList([event(next, parent ? [parent] : [])]))).ok, true)
      parent = next
    }
    const files = await readdir(path.join(local.commonDir, 'wanxiang', 'events'))
    assert.deepEqual(files, ['one-file-law.ndjson'])
  } finally {
    local.close()
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
  const local = createLocalEventStore()
  try {
    const same = event(id(1))
    assert.equal(resultOf(await local.store.Append(toList([same]))).ok, true)
    assert.equal(resultOf(await local.store.Append(toList([same]))).ok, true)
    const conflict = new Domain.EventEnvelope(eventId(id(1)), streamId('append/law'), 'JobRequested', toList([]), { id: 'different' }, toList([]))
    assert.equal(resultOf(await local.store.Append(toList([conflict]))).ok, false)
  } finally {
    local.close()
  }
})
