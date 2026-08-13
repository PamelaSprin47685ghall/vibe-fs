// tests/integration/persist/leave-unread.test.mjs
// P4U3 LEAVE-UNREAD-CONTRACT — Amendment G3.5-A / Phase 4 Active notes.
//
// Abandoned on-disk legacy Journal NDJSON + RuntimePath blobs must remain
// unread when EventStore paths open/append/(local) converge. No Boot/Writer,
// no migrator, no LegacyProjection assertions.

import assert from 'node:assert/strict'
import { createHash } from 'node:crypto'
import { execFileSync } from 'node:child_process'
import {
  mkdirSync,
  readFileSync,
  writeFileSync,
  statSync,
  existsSync,
} from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'
import {
  caseOf,
  eventId,
  isSome,
  listItems,
  mapEntries,
  payloadOf,
  toList,
} from '../../unit/support/domain.mjs'
import {
  createBareWorkspace,
  readRemoteStoreOid,
  runGitIn,
} from './dumb-remote.mjs'

const Domain = await import('../../../dist/Domain/EventStore.js')
const Persist = await import('../../../dist/Infrastructure/Persist/StoreTypes.js')
const Process = await import('../../../dist/Infrastructure/Persist/ProcessGitRawStore.js')
const GitRaw = await import('../../../dist/Infrastructure/Persist/GitRawStore.js')
const Store = await import('../../../dist/Infrastructure/Persist/EventStore.js')
const Gateway = await import('../../../dist/Infrastructure/Git/GitGateway.js')

const POISON = 'LEAVE_UNREAD_POISON_SENTINEL_NEVER_PARSE\n{not-a-journal-envelope\n'
const STALE_RUNTIME_ID = 'abandoned-runtime'

const streamId = (v) => Domain.EventStreamIdModule_create(v)
const oidValue = (rootOid) => Persist.GitObjectIdModule_value(Persist.RootOidModule_value(rootOid))
const snapshotOid = (snapshot) => oidValue(snapshot.RootOid)

const envelope = ({
  id,
  stream = 'job/main',
  eventType = 'JobRequested',
  parents = [],
  payload = { status: 'open' },
} = {}) =>
  new Domain.EventEnvelope(
    eventId(id),
    streamId(stream),
    eventType,
    toList(parents.map(eventId)),
    payload,
    toList([]),
  )

const mustOk = (result, label = 'result') => {
  assert.equal(caseOf(result), 'Ok', `${label} should be Ok, got ${caseOf(result)}`)
  return payloadOf(result)
}

const openProcessStore = (repoPath) => Process.ProcessGitRawStoreModule_create(repoPath)

const gatewayRunner = (repoPath) => async (argsEnv) => {
  const args = listItems(Array.isArray(argsEnv) ? argsEnv[0] : argsEnv)
  const envOpt = Array.isArray(argsEnv) ? argsEnv[1] : undefined
  const overlay = {}
  if (envOpt != null) {
    for (const [key, value] of mapEntries(envOpt)) overlay[key] = value
  }
  const result = runGitIn(repoPath, args, Object.keys(overlay).length > 0 ? overlay : undefined)
  return [result.code, result.stdout, result.stderr]
}

const gitCommonDir = (repoPath) => {
  const common = execFileSync('git', ['-C', repoPath, 'rev-parse', '--git-common-dir'], {
    encoding: 'utf8',
  }).trim()
  return common.startsWith('/') ? common : join(repoPath, common)
}

const fingerprint = (path) => {
  const st = statSync(path)
  return {
    size: st.size,
    mtimeMs: st.mtimeMs,
    ino: st.ino,
    sha256: createHash('sha256').update(readFileSync(path)).digest('hex'),
  }
}

/** Plant garbage legacy Journal NDJSON + blobs under git-common-dir (leave-unread fixtures). */
const plantStaleLegacyRuntime = (repoPath) => {
  const runtimeDir = join(gitCommonDir(repoPath), 'wanxiangshu-next', 'runtimes')
  const blobsDir = join(runtimeDir, 'blobs')
  mkdirSync(blobsDir, { recursive: true, mode: 0o700 })

  const ndjsonPath = join(runtimeDir, `${STALE_RUNTIME_ID}.ndjson`)
  const blobDigest = createHash('sha256').update(POISON).digest('hex')
  const blobPath = join(blobsDir, blobDigest)

  writeFileSync(ndjsonPath, POISON, { mode: 0o600 })
  writeFileSync(blobPath, POISON, { mode: 0o600 })

  assert.equal(existsSync(ndjsonPath), true)
  assert.equal(existsSync(blobPath), true)

  return {
    runtimeDir,
    ndjsonPath,
    blobPath,
    blobDigest,
    before: {
      ndjson: fingerprint(ndjsonPath),
      blob: fingerprint(blobPath),
    },
  }
}

const assertUntouched = (label, path, before) => {
  const after = fingerprint(path)
  assert.deepEqual(after, before, `${label} must stay byte+inode/mtime identical (leave-unread)`)
  assert.equal(readFileSync(path, 'utf8'), POISON, `${label} content must remain unparsed poison`)
}

const eventPaths = async (raw, rootOid) =>
  listItems(mustOk(await GitRaw.GitRawStore_listEventBlobs(raw, rootOid))).map(([path]) => path).sort()

const eventBlobTexts = async (raw, rootOid) => {
  const blobs = listItems(mustOk(await GitRaw.GitRawStore_listEventBlobs(raw, rootOid)))
  return await Promise.all(blobs.map(async ([, oid]) => {
    const bytes = await raw.ReadObject(oid)
    return Buffer.from(bytes).toString('utf8')
  }))
}

const withWorkspace = async (clientNames, body) => {
  const ws = createBareWorkspace(clientNames)
  try {
    return await body(ws)
  } finally {
    ws.cleanup()
  }
}

test('leave_unread_helper_surface_does_not_import_Boot_or_Writer', () => {
  const source = readFileSync(fileURLToPath(import.meta.url), 'utf8')
  // Import graph only — comments may name Boot/Writer as forbidden APIs.
  assert.doesNotMatch(source, /from ['"].*Journal\/(Boot|Writer)/)
  assert.doesNotMatch(source, /await import\(['"].*Journal\/(Boot|Writer)/)
  assert.doesNotMatch(source, /await import\(['"].*(?:journalStore|LegacyProjection)/)
  assert.doesNotMatch(source, /\bjournalStore\s*\(/)
  // Domain codecs only via EventStore test helpers (mirror dumb-server).
  assert.match(source, /dist\/Domain\/EventStore\.js/)
})

test('EventStore_open_append_leaves_stale_ndjson_and_blobs_unread', async () => {
  await withWorkspace(['local'], async (ws) => {
    const repo = ws.client('local')
    const planted = plantStaleLegacyRuntime(repo)

    const raw = openProcessStore(repo)
    const es = Store.EventStore_create(raw)

    const snap = await es.OpenSnapshot()
    assert.equal(typeof snapshotOid(snap), 'string')
    assert.equal(snapshotOid(snap).length, 40)

    const published = mustOk(
      await es.Append(
        snap,
        toList([
          envelope({
            id: 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa',
            payload: { leaveUnread: true },
          }),
        ]),
      ),
      'append',
    )

    assert.equal(isSome(await raw.ReadRef(Persist.StoreRef_canonical)), true)
    assert.deepEqual(await eventPaths(raw, published.RootOid), [
      'events/aa/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.jsonl',
    ])

    for (const text of await eventBlobTexts(raw, published.RootOid)) {
      assert.doesNotMatch(text, /LEAVE_UNREAD_POISON_SENTINEL/)
    }

    assertUntouched('stale ndjson', planted.ndjsonPath, planted.before.ndjson)
    assertUntouched('stale blob', planted.blobPath, planted.before.blob)
  })
})

test('EventStore_local_converge_leaves_stale_legacy_runtime_unread', async () => {
  await withWorkspace(['writer'], async (ws) => {
    const repo = ws.client('writer')
    const planted = plantStaleLegacyRuntime(repo)

    const raw = openProcessStore(repo)
    const es = Store.EventStore_create(raw)
    const published = mustOk(
      await es.Append(
        await es.OpenSnapshot(),
        toList([
          envelope({
            id: 'bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb',
            payload: { n: 1 },
          }),
        ]),
      ),
      'append before converge',
    )
    const tip = snapshotOid(published)

    // Local-only converge against the bare origin (no second client): upload path
    // still must not open abandoned Journal/Blob fixtures.
    mustOk(await Gateway.GitGateway_convergeStore(raw, gatewayRunner(repo), 8, 'origin'), 'local converge')
    assert.equal(readRemoteStoreOid(ws.bare), tip)

    assert.deepEqual(await eventPaths(raw, published.RootOid), [
      'events/bb/bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb.jsonl',
    ])
    for (const text of await eventBlobTexts(raw, published.RootOid)) {
      assert.doesNotMatch(text, /LEAVE_UNREAD_POISON_SENTINEL/)
    }

    assertUntouched('stale ndjson after converge', planted.ndjsonPath, planted.before.ndjson)
    assertUntouched('stale blob after converge', planted.blobPath, planted.before.blob)
  })
})

test('planted_legacy_layout_matches_RuntimePath_convention', async () => {
  await withWorkspace(['layout'], async (ws) => {
    const repo = ws.client('layout')
    const planted = plantStaleLegacyRuntime(repo)
    const common = gitCommonDir(repo)

    assert.equal(planted.runtimeDir, join(common, 'wanxiangshu-next', 'runtimes'))
    assert.equal(planted.ndjsonPath, join(planted.runtimeDir, `${STALE_RUNTIME_ID}.ndjson`))
    assert.equal(planted.blobPath, join(planted.runtimeDir, 'blobs', planted.blobDigest))
    assert.match(planted.blobDigest, /^[0-9a-f]{64}$/)
  })
})
