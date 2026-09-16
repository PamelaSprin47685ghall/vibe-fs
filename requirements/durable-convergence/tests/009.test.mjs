import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { readFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import { retainedWriterIdsAt, syncAt, writerSyncAdapterScenario } from '../../../dist/Persistence/EventStore/RetentionSurface.js'

const read = (relative) => readFile(new URL(`../../../${relative}`, import.meta.url), 'utf8')

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

test('WHAT[DURABLE-CONVERGENCE-009] dumb remote fixture has no Wanxiang domain or server-side logic', async () => {
  const remote = await read('requirements/verification-system/tests/support/dumb-remote.mjs')
  assert.doesNotMatch(remote, /dist\/Domain|CanonicalIntegrator|Projection|WriterStreamSync|HookSync/,
    'remote fixture must stay a dumb Git remote: no Event/Projection/Wanxiang domain')
  assert.match(remote, /git/, 'remote fixture speaks plain Git')
  assert.doesNotMatch(remote, /pre-receive|post-receive|serverSide|receive\.hook/i,
    'no server-side merge, pre-receive reducer or post-receive projection')

  const gateway = await read('src/Wanxiangshu/Git/Gateway.fs')
  assert.match(gateway, /WriterStreamSync\.syncWriterStreams/,
    'all sync intelligence lives in the client gateway')
  assert.doesNotMatch(gateway, /pre-receive|post-receive/i,
    'client gateway must not install server-side receive hooks')
})

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

test('WHAT[DURABLE-CONVERGENCE-009] Adapter: writer sync with absent remote creates the local-only snapshot without error', async () => {
  const repo = mkdtempSync(join(tmpdir(), 'wxs-writer-sync-remote-absent-'))
  execFileSync('git', ['init', '-q', repo])
  const commonDir = join(repo, '.git')
  const nowMs = Date.now()

  try {
    const events = join(commonDir, 'wanxiang', 'events')
    mkdirSync(events, { recursive: true })
    writeFileSync(join(events, 'writer-local.ndjson'), canonicalLine('a'.repeat(40), '1'.repeat(40)))

    const result = await syncAt(repo, commonDir, null, nowMs)
    assert.equal(result.ok, true, JSON.stringify(result))

    const rootEntries = execFileSync('git', ['-C', repo, 'ls-tree', result.root], { encoding: 'utf8' })
    assert.match(rootEntries, /\twriters$/m)
    assert.match(rootEntries, /\tpayloads$/m)
    assert.match(rootEntries, /\twriter-manifest$/m)

    const writers = execFileSync('git', ['-C', repo, 'ls-tree', `${result.root}:writers`], { encoding: 'utf8' })
    assert.match(writers, /writer-local\.ndjson$/m)
    assert.deepEqual(retainedWriterIdsAt(commonDir, nowMs), ['writer-local'])
  } finally {
    rmSync(repo, { recursive: true, force: true })
  }
})
