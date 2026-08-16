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
  const store = eventStore.EventStoreSurface_create(gitCommonDir, 'writer-proof-a')
  try {
    const first = Array.from({ length: 4 }, (_, i) => event(hexId(i + 1), i + 1))
    assert.equal((await eventStore.EventStoreSurface_append(store, first)).ok, true)
    const file = path.join(gitCommonDir, 'wanxiang', 'events', 'writer-proof-a.ndjson')
    const prefix = await readFile(file)

    const many = Array.from({ length: 160 }, (_, i) => event(hexId(i + 100), i + 100))
    assert.equal((await eventStore.EventStoreSurface_append(store, many)).ok, true)
    const after = await readFile(file)

    assert.equal(after.subarray(0, prefix.length).equals(prefix), true, 'append must preserve every prior byte')
    assert.equal(path.basename(file), 'writer-proof-a.ndjson')

    const files = await readdir(path.join(gitCommonDir, 'wanxiang', 'events'))
    assert.deepEqual(files, ['writer-proof-a.ndjson'], 'history size must not create 000000/segment/chunk files')
    assert.equal(files.some((name) => /^\d+\.ndjson$/.test(name)), false)
  } finally {
    eventStore.EventStoreSurface_dispose(store)
    await remove(path.dirname(gitCommonDir))
  }
})

test('WHAT[DURABLE-EVENTS-017] DURABLE_EVENTS_004_017_local_append_has_zero_Git_object_tree_ref_dependencies', async () => {
  const source = await readFile(
    new URL('../../../src/Wanxiangshu/Persistence/EventStore/ProcessEventLog.fs', import.meta.url),
    'utf8',
  )

  for (const forbidden of [
    'IGitRawStore',
    'WriteBlob',
    'WriteTree',
    'ReadTree',
    'ReadRef',
    'CompareAndSwapRef',
    'RootOid',
    'StoreRef.canonical',
    'SegmentMaxBytes',
  ]) {
    assert.equal(source.includes(forbidden), false, `local append must not depend on ${forbidden}`)
  }

  assert.match(source, /AppendAllText|appendFileSync/)
  assert.match(source, /commonDir|wanxiangDirectory|eventsDirectory/)
})

test('WHAT[DURABLE-EVENTS-005] DURABLE_EVENTS_005_each_process_writer_id_names_a_distinct_file_without_machine_identity', async () => {
  const gitCommonDir = await commonDir()
  const a = eventStore.EventStoreSurface_create(gitCommonDir, 'writer-a')
  const b = eventStore.EventStoreSurface_create(gitCommonDir, 'writer-b')
  try {
    assert.equal((await eventStore.EventStoreSurface_append(a, [event(hexId(0xa1), 1)])).ok, true)
    assert.equal((await eventStore.EventStoreSurface_append(b, [event(hexId(0xb1), 2)])).ok, true)

    const files = (await readdir(path.join(gitCommonDir, 'wanxiang', 'events'))).sort()
    assert.deepEqual(files, ['writer-a.ndjson', 'writer-b.ndjson'])
  } finally {
    eventStore.EventStoreSurface_dispose(a)
    eventStore.EventStoreSurface_dispose(b)
    await remove(path.dirname(gitCommonDir))
  }
})

test('WHAT[DURABLE-EVENTS-011] one_complete_writer_file_is_one_blob_only_at_remote_sync_boundary', async () => {
  const { readFile } = await import('node:fs/promises')
  const sync = await readFile(new URL('../../../src/Wanxiangshu/Persistence/EventStore/WriterStreamSync.fs', import.meta.url), 'utf8')
  const store = await readFile(new URL('../../../src/Wanxiangshu/Persistence/EventStore/Store.fs', import.meta.url), 'utf8')
  const log = await readFile(new URL('../../../src/Wanxiangshu/Persistence/EventStore/ProcessEventLog.fs', import.meta.url), 'utf8')

  assert.match(sync, /WriteBlob/, 'remote sync encodes writer bytes as Git blobs')
  assert.match(sync, /writerId \+ "\.ndjson"/, 'one complete writer file maps to one blob entry')
  assert.doesNotMatch(sync, /delta|chunk|segment|EventIdShard|index/, 'no delta/chunk/segment/index protocol in sync encoding')
  assert.doesNotMatch(store + log, /\.Fetch|\.Pull|\.Push|WriterStreamSync|GitGateway|ProcessGitRawStore/, 'runtime append path performs no remote sync or Git CAS')
})
