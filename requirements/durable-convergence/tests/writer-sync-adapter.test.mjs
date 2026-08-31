import assert from 'node:assert/strict'
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import { writerSyncAdapterScenario } from '../../../dist/Persistence/EventStore/RetentionSurface.js'

const canonicalLine = (id, stream) => JSON.stringify({
  event_id: id,
  event_type: 'JobRequested',
  parents: [],
  payload: {},
  payload_refs: [],
  stream_id: stream,
}) + '\n'

const operations = (protocol) => protocol.map((call) => call.split(' ', 1)[0])
const writtenRoot = (protocol) => protocol.findLast((call) => call.startsWith('WriteTree ')).slice('WriteTree '.length)

test('WHAT[DURABLE-CONVERGENCE-009] Adapter: writer sync preserves exact identities and fails closed at the Git gateway', async () => {
  const root = mkdtempSync(join(tmpdir(), 'wxs-writer-sync-adapter-'))
  const commonDir = join(root, '.git')
  const localWriterId = 'writer-local'
  const remoteWriterId = 'writer-remote'
  const nowMs = Date.now()

  try {
    const events = join(commonDir, 'wanxiang', 'events')
    mkdirSync(events, { recursive: true })
    writeFileSync(join(events, `${localWriterId}.ndjson`), canonicalLine('a'.repeat(40), '1'.repeat(40)))

    const result = await writerSyncAdapterScenario({
      commonDir,
      nowMs,
      remoteWriterId,
      remoteWriterText: canonicalLine('b'.repeat(40), '2'.repeat(40)),
      remoteActivityMs: nowMs,
    })

    assert.equal(result.first.ok, true, JSON.stringify(result.first))
    assert.equal(result.repeat.ok, true, JSON.stringify(result.repeat))
    assert.deepEqual(result.localWriterIds, [localWriterId, remoteWriterId])
    assert.deepEqual(result.writerIdsAfterInvalid, [localWriterId, remoteWriterId])

    assert.equal(result.first.protocol.includes(`ReadTree ${result.validRemoteRoot}`), true,
      'the adapter must receipt the exact supplied remote root')
    assert.equal(result.first.root, writtenRoot(result.first.protocol),
      'the returned snapshot identity must be the root written at the gateway')
    assert.equal(result.repeat.root, result.first.root,
      'repeating the same remote import must preserve snapshot identity')
    assert.equal(result.repeat.root, writtenRoot(result.repeat.protocol))

    assert.deepEqual(operations(result.first.protocol), [
      'WriteBlob', 'WriteTree', 'WriteTree', 'WriteBlob', 'WriteTree',
      'ReadTree', 'ReadTree', 'ReadObject', 'ReadObject', 'ReadTree',
      'WriteBlob', 'WriteTree', 'WriteTree', 'WriteBlob', 'WriteTree',
    ])
    assert.deepEqual(operations(result.repeat.protocol), [
      'WriteTree', 'WriteTree', 'WriteBlob', 'WriteTree',
      'ReadTree', 'ReadTree', 'ReadObject', 'ReadTree',
      'WriteTree', 'WriteTree', 'WriteBlob', 'WriteTree',
    ])

    assert.equal(result.invalid.ok, false)
    assert.match(result.invalid.error, /sync root must contain writers\/ and payloads\//)
    assert.deepEqual(operations(result.invalid.protocol), [
      'WriteTree', 'WriteTree', 'WriteBlob', 'WriteTree', 'ReadTree',
    ])
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
})
