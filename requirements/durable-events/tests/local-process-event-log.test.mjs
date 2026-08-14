// FROZEN — 2026-08-14. Written before implementation by explicit user request.
// Intentionally NOT executed before implementation.
//
// DURABLE-EVENTS-004/005/010/011/017:
// local truth is one unbounded .git/wanxiang/events/<WriterId>.ndjson per process;
// local append performs no Git object/tree/ref work; blobification belongs only to remote sync.

import assert from 'node:assert/strict'
import { mkdtemp, readFile, readdir } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import path from 'node:path'
import test from 'node:test'

import { eventId, toList } from '../../verification-system/tests/support/domain.mjs'

const Domain = await import('../../../dist/Domain/EventStore.js')
const LocalLog = await import('../../../dist/Infrastructure/Persist/ProcessEventLog.js')

const streamId = (value) => Domain.EventStreamIdModule_create(value)
const envelope = (id, n) =>
  new Domain.EventEnvelope(
    eventId(id),
    streamId('proof/local'),
    'JobRequested',
    toList([]),
    { n },
    toList([]),
  )

const hexId = (n) => n.toString(16).padStart(40, '0')
const commonDir = async () => path.join(await mkdtemp(path.join(tmpdir(), 'wanxiang-local-log-')), '.git')

test('DURABLE_EVENTS_005_one_process_is_one_unbounded_writer_file_with_no_segments', async () => {
  const gitCommonDir = await commonDir()
  const writerId = 'writer-proof-a'
  const log = LocalLog.ProcessEventLogModule_create(gitCommonDir, writerId)

  const first = Array.from({ length: 4 }, (_, i) => envelope(hexId(i + 1), i + 1))
  LocalLog.ProcessEventLogModule_append(log, toList(first))
  const prefix = await readFile(LocalLog.ProcessEventLogModule_filePath(log))

  const many = Array.from({ length: 160 }, (_, i) => envelope(hexId(i + 100), i + 100))
  LocalLog.ProcessEventLogModule_append(log, toList(many))
  const after = await readFile(LocalLog.ProcessEventLogModule_filePath(log))

  assert.equal(after.subarray(0, prefix.length).equals(prefix), true, 'append must preserve every prior byte')
  assert.equal(path.basename(LocalLog.ProcessEventLogModule_filePath(log)), `${writerId}.ndjson`)

  const files = await readdir(path.join(gitCommonDir, 'wanxiang', 'events'))
  assert.deepEqual(files, [`${writerId}.ndjson`], 'history size must not create 000000/segment/chunk files')
  assert.equal(files.some((name) => /^\d+\.ndjson$/.test(name)), false)
})

test('DURABLE_EVENTS_004_017_local_append_has_zero_Git_object_tree_ref_dependencies', async () => {
  const source = await readFile(
    new URL('../../../src/Wanxiangshu/Infrastructure/Persist/ProcessEventLog.fs', import.meta.url),
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

test('DURABLE_EVENTS_005_each_process_writer_id_names_a_distinct_file_without_machine_identity', async () => {
  const gitCommonDir = await commonDir()
  const a = LocalLog.ProcessEventLogModule_create(gitCommonDir, 'writer-a')
  const b = LocalLog.ProcessEventLogModule_create(gitCommonDir, 'writer-b')

  LocalLog.ProcessEventLogModule_append(a, toList([envelope(hexId(0xa1), 1)]))
  LocalLog.ProcessEventLogModule_append(b, toList([envelope(hexId(0xb1), 2)]))

  assert.notEqual(LocalLog.ProcessEventLogModule_filePath(a), LocalLog.ProcessEventLogModule_filePath(b))
  const files = (await readdir(path.join(gitCommonDir, 'wanxiang', 'events'))).sort()
  assert.deepEqual(files, ['writer-a.ndjson', 'writer-b.ndjson'])
})
