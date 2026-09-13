// DURABLE-EVENTS-004/005/010/011/017:
// local truth is one unbounded .git/wanxiang/events/<WriterId>.ndjson per process;
// local append performs no Git object/tree/ref work; blobification belongs only to remote sync.

import assert from 'node:assert/strict'
import { mkdtemp, readFile, readdir, rm } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import path from 'node:path'
import test from 'node:test'

import * as eventStore from '../../../dist/Persistence/EventStore/Surface.js'

const hexId = (n) => n.toString(16).padStart(40, '0')
const event = (id, n, parents = []) => ({
  id,
  stream: 'proof/local',
  type: 'JobRequested',
  parents,
  payload: { n },
  payloadRefs: [],
})
const commonDir = async () => path.join(await mkdtemp(path.join(tmpdir(), 'wanxiang-local-log-')), '.git')

const remove = async (dir) => rm(dir, { recursive: true, force: true })

test('WHAT[DURABLE-EVENTS-005] DURABLE_EVENTS_005_one_process_is_one_unbounded_writer_file_with_no_segments', async () => {
  const gitCommonDir = await commonDir()
  const store = eventStore.create(gitCommonDir, 'writer-proof-a')
  try {
    const first = Array.from({ length: 4 }, (_, i) => event(hexId(i + 1), i + 1))
    assert.equal((await eventStore.append(store, first)).ok, true)
    const file = path.join(gitCommonDir, 'wanxiang', 'events', 'writer-proof-a.ndjson')
    const prefix = await readFile(file)

    const many = Array.from({ length: 160 }, (_, i) => event(hexId(i + 100), i + 100))
    assert.equal((await eventStore.append(store, many)).ok, true)
    const after = await readFile(file)

    assert.equal(after.subarray(0, prefix.length).equals(prefix), true, 'append must preserve every prior byte')
    assert.equal(path.basename(file), 'writer-proof-a.ndjson')

    const files = await readdir(path.join(gitCommonDir, 'wanxiang', 'events'))
    assert.deepEqual(files, ['writer-proof-a.ndjson'], 'history size must not create 000000/segment/chunk files')
    assert.equal(files.some((name) => /^\d+\.ndjson$/.test(name)), false)
  } finally {
    eventStore.dispose(store)
    await remove(path.dirname(gitCommonDir))
  }
})

test('WHAT[DURABLE-EVENTS-017] DURABLE_EVENTS_004_017_local_append_has_zero_Git_object_tree_ref_dependencies', async () => {
  const gitCommonDir = await commonDir()
  const store1 = eventStore.create(gitCommonDir, 'writer-bytes-1')
  try {
    const e1 = event(hexId(1), 1)
    const e2 = event(hexId(2), 2, [hexId(1)])

    // 1. Two appends preserve all previous bytes exactly
    assert.equal((await eventStore.append(store1, [e1])).ok, true)
    const file = path.join(gitCommonDir, 'wanxiang', 'events', 'writer-bytes-1.ndjson')
    const bytesAfterFirst = await readFile(file)

    assert.equal((await eventStore.append(store1, [e2])).ok, true)
    const bytesAfterSecond = await readFile(file)
    assert.equal(bytesAfterSecond.subarray(0, bytesAfterFirst.length).equals(bytesAfterFirst), true)

    eventStore.dispose(store1)

    // 2. Restart and readback confirms both events are durable and intact
    const store2 = eventStore.create(gitCommonDir, 'writer-bytes-2')
    try {
      const read1 = eventStore.read(store2, hexId(1))
      const read2 = eventStore.read(store2, hexId(2))
      assert.deepEqual(read1?.payload, { n: 1 })
      assert.deepEqual(read2?.payload, { n: 2 })
    } finally {
      eventStore.dispose(store2)
    }

    // 3. Tail truncation / incomplete trailing line fail-closed on restart
    const { appendFile } = await import('node:fs/promises')
    await appendFile(file, '{"event_id":"corrupt-half-line')
    assert.throws(() => {
      const store3 = eventStore.create(gitCommonDir, 'writer-bytes-3')
      eventStore.dispose(store3)
    }, /incomplete trailing line|NonCanonical/i, 'boot must fail-closed on corrupted tail')
  } finally {
    await remove(path.dirname(gitCommonDir))
  }
})

test('WHAT[DURABLE-EVENTS-005] DURABLE_EVENTS_005_each_process_writer_id_names_a_distinct_file_without_machine_identity', async () => {
  const gitCommonDir = await commonDir()
  const a = eventStore.create(gitCommonDir, 'writer-a')
  const b = eventStore.create(gitCommonDir, 'writer-b')
  try {
    assert.equal((await eventStore.append(a, [event(hexId(0xa1), 1)])).ok, true)
    assert.equal((await eventStore.append(b, [event(hexId(0xb1), 2)])).ok, true)

    const files = (await readdir(path.join(gitCommonDir, 'wanxiang', 'events'))).sort()
    assert.deepEqual(files, ['writer-a.ndjson', 'writer-b.ndjson'])
  } finally {
    eventStore.dispose(a)
    eventStore.dispose(b)
    await remove(path.dirname(gitCommonDir))
  }
})
