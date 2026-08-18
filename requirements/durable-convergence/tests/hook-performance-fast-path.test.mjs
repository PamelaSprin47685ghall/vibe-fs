import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import test from 'node:test'

const read = (relative) => readFile(new URL(`../../../${relative}`, import.meta.url), 'utf8')

test('WHAT[DURABLE-CONVERGENCE-010] no-op sync reuses stat-fingerprint materialization instead of rereading durable bytes', async () => {
  const log = await read('src/Wanxiangshu/Persistence/EventStore/ProcessEventLog.fs')
  const sync = await read('src/Wanxiangshu/Persistence/EventStore/WriterStreamSync.fs')

  assert.match(log, /physicalFingerprint/)
  assert.match(log, /statSync/)
  assert.match(sync, /tryCachedLocal/)
  assert.match(sync, /physicalFingerprint/)
  assert.match(sync, /materializationCache/i)
})

test('WHAT[DURABLE-CONVERGENCE-010] near-equal worst path reads and blobifies only changed files', async () => {
  const log = await read('src/Wanxiangshu/Persistence/EventStore/ProcessEventLog.fs')
  const sync = await read('src/Wanxiangshu/Persistence/EventStore/WriterStreamSync.fs')

  assert.match(log, /writerPhysicalStats/)
  assert.match(log, /payloadPhysicalStats/)
  assert.match(log, /payloadExists[\s\S]*existsSync/)
  assert.match(sync, /CachedFile/)
  assert.match(sync, /cachedOid/)
  assert.match(sync, /remoteEntryNeeded/)
  assert.match(sync, /changedRemoteEntries/)
  assert.match(sync, /cached\.Oid = entry\.Oid/)
  assert.doesNotMatch(sync, /readRemoteTrees[\s\S]*readBlobList raw writerEntries[\s\S]*readBlobList raw payloadEntries/)
})

test('WHAT[DURABLE-CONVERGENCE-010] pre-push starts from tracking ref and only discovers remote after lease rejection', async () => {
  const gateway = await read('src/Wanxiangshu/Git/Gateway.fs')

  assert.match(gateway, /readTrackedRemote|trackingRef/)
  assert.match(gateway, /pushSnapshot/)
  assert.match(gateway, /Error _ when retriesLeft > 0[\s\S]*discoverRemote/s)
  assert.doesNotMatch(gateway, /\| None ->\s*let! snapshot, expected = discoverRemote run remote/)
})

test('WHAT[DURABLE-CONVERGENCE-010] confirmed same-root convergence does not publish an empty snapshot', async () => {
  const gateway = await read('src/Wanxiangshu/Git/Gateway.fs')

  assert.match(gateway, /remoteKnownCurrent|confirmedRemote/i)
  assert.match(gateway, /RootOid\.value merged\.RootOid/)
  assert.match(gateway, /expectedRemote/)
  assert.match(gateway, /return Ok\(\)/)
})
