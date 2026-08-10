// tests/integration/persist/dumb-server.test.mjs
// Phase 3.4 / §12 / §38 — GitGateway + ProcessGitRawStore against a dumb bare remote.
//
// Client may use Domain + Persist. The bare "server" (dumb-remote.mjs) must not
// import Domain codecs to decide refs — only git objects / refs / lease / CAS.

import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
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
  STORE_REF,
  createBareWorkspace,
  leasePushStore,
  readRemoteStoreOid,
  remoteHasObject,
  remoteObjectType,
  runGitIn,
} from './dumb-remote.mjs'

const Domain = await import('../../../dist/Domain/EventStore.js')
const Persist = await import('../../../dist/Infrastructure/Persist/StoreTypes.js')
const Process = await import('../../../dist/Infrastructure/Persist/ProcessGitRawStore.js')
const GitRaw = await import('../../../dist/Infrastructure/Persist/GitRawStore.js')
const Store = await import('../../../dist/Infrastructure/Persist/EventStore.js')
const Gateway = await import('../../../dist/Infrastructure/Git/GitGateway.js')

const streamId = (v) => Domain.EventStreamIdModule_create(v)
const oidValue = (rootOid) => Persist.GitObjectIdModule_value(Persist.RootOidModule_value(rootOid))
const snapshotOid = (snapshot) => oidValue(snapshot.RootOid)
const gitOid = (oid) => Persist.GitObjectIdModule_value(oid)

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

const mustErr = (result, label = 'result') => {
  assert.equal(caseOf(result), 'Error', `${label} should be Error`)
  return payloadOf(result)
}

const eventPaths = (raw, rootOid) =>
  listItems(mustOk(GitRaw.GitRawStore_listEventBlobs(raw, rootOid))).map(([path]) => path).sort()

/** GitGatewaySyncRunner over a real client workspace (optional wrap for injection). */
const gatewayRunner = (repoPath, wrap) => {
  const base = (argsEnv) => {
    const args = listItems(Array.isArray(argsEnv) ? argsEnv[0] : argsEnv)
    const envOpt = Array.isArray(argsEnv) ? argsEnv[1] : undefined
    const overlay = {}
    if (envOpt != null) {
      for (const [key, value] of mapEntries(envOpt)) overlay[key] = value
    }
    const result = runGitIn(repoPath, args, Object.keys(overlay).length > 0 ? overlay : undefined)
    return [result.code, result.stdout, result.stderr]
  }
  return wrap ? wrap(base) : base
}

const openProcessStore = (repoPath) => Process.ProcessGitRawStoreModule_create(repoPath)

const withWorkspace = (clientNames, body) => {
  const ws = createBareWorkspace(clientNames)
  try {
    return body(ws)
  } finally {
    ws.cleanup()
  }
}

test('dumb_remote_helper_does_not_import_Domain_codecs', () => {
  const helperPath = join(dirname(fileURLToPath(import.meta.url)), 'dumb-remote.mjs')
  const source = readFileSync(helperPath, 'utf8')
  assert.equal(STORE_REF, 'refs/wanxiang/store')
  assert.equal(STORE_REF, Persist.StoreRef_canonical)
  assert.doesNotMatch(source, /Domain\//)
  assert.doesNotMatch(source, /EventStore|CanonicalEventCodec|EventEnvelope/)
  assert.match(source, /STORE_REF\s*=\s*'refs\/wanxiang\/store'/)
})

test('ProcessGitRawStore_ref_CAS_against_real_git', () => {
  withWorkspace(['cas'], (ws) => {
    const raw = openProcessStore(ws.client('cas'))
    const blob = raw.WriteBlob(Buffer.from('cas-blob\n'))
    const tree = raw.WriteTree(toList([new Persist.TreeEntry('100644', 'blob', blob)]))

    assert.equal(raw.CompareAndSwapRef(Persist.StoreRef_canonical, undefined, tree), true)
    assert.equal(isSome(raw.ReadRef(Persist.StoreRef_canonical)), true)
    assert.equal(gitOid(raw.ReadRef(Persist.StoreRef_canonical)), gitOid(tree))

    // Absent expectation must fail once the ref exists.
    assert.equal(raw.CompareAndSwapRef(Persist.StoreRef_canonical, undefined, tree), false)
    // Wrong expected OID rejects.
    assert.equal(raw.CompareAndSwapRef(Persist.StoreRef_canonical, blob, tree), false)
    // Correct expected OID succeeds (including no-op same tip).
    assert.equal(raw.CompareAndSwapRef(Persist.StoreRef_canonical, tree, tree), true)

    const nextBlob = raw.WriteBlob(Buffer.from('cas-blob-2\n'))
    const nextTree = raw.WriteTree(toList([new Persist.TreeEntry('100644', 'blob', nextBlob)]))
    assert.equal(raw.CompareAndSwapRef(Persist.StoreRef_canonical, tree, nextTree), true)
    assert.equal(gitOid(raw.ReadRef(Persist.StoreRef_canonical)), gitOid(nextTree))
  })
})

test('object_upload_to_bare_remote_via_GitGateway_converge', () => {
  withWorkspace(['uploader'], (ws) => {
    const raw = openProcessStore(ws.client('uploader'))
    const es = Store.EventStore_create(raw)
    const published = mustOk(
      es.Append(
        es.OpenSnapshot(),
        toList([envelope({ id: 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa', payload: { n: 1 } })]),
      ),
      'append',
    )
    const tip = snapshotOid(published)

    mustOk(Gateway.GitGateway_convergeStore(raw, gatewayRunner(ws.client('uploader')), 8, 'origin'), 'upload converge')

    assert.equal(readRemoteStoreOid(ws.bare), tip)
    assert.equal(remoteHasObject(ws.bare, tip), true)
    assert.equal(remoteObjectType(ws.bare, tip), 'tree')
  })
})

test('object_fetch_from_bare_remote_into_second_client', () => {
  withWorkspace(['writer', 'reader'], (ws) => {
    const writerRaw = openProcessStore(ws.client('writer'))
    const writerEs = Store.EventStore_create(writerRaw)
    const published = mustOk(
      writerEs.Append(
        writerEs.OpenSnapshot(),
        toList([envelope({ id: 'bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb', payload: { n: 2 } })]),
      ),
      'writer append',
    )
    mustOk(
      Gateway.GitGateway_convergeStore(writerRaw, gatewayRunner(ws.client('writer')), 8, 'origin'),
      'writer upload',
    )
    const tip = snapshotOid(published)
    assert.equal(readRemoteStoreOid(ws.bare), tip)

    const readerRaw = openProcessStore(ws.client('reader'))
    const merged = mustOk(
      Gateway.GitGateway_convergeStore(readerRaw, gatewayRunner(ws.client('reader')), 8, 'origin'),
      'reader fetch/converge',
    )
    assert.equal(snapshotOid(merged), tip)
    assert.deepEqual(eventPaths(readerRaw, merged.RootOid), [
      'events/bb/bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb.jsonl',
    ])
    assert.equal(gitOid(readerRaw.ReadRef(Persist.StoreRef_remoteTracking('origin'))), tip)
  })
})

test('two_clients_merge_through_dumb_remote', () => {
  withWorkspace(['a', 'b'], (ws) => {
    const rawA = openProcessStore(ws.client('a'))
    const esA = Store.EventStore_create(rawA)
    mustOk(
      esA.Append(
        esA.OpenSnapshot(),
        toList([envelope({ id: 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa', payload: { side: 'a' } })]),
      ),
      'A append',
    )
    mustOk(Gateway.GitGateway_convergeStore(rawA, gatewayRunner(ws.client('a')), 8, 'origin'), 'A upload')

    const rawB = openProcessStore(ws.client('b'))
    const esB = Store.EventStore_create(rawB)
    mustOk(
      esB.Append(
        esB.OpenSnapshot(),
        toList([envelope({ id: 'bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb', payload: { side: 'b' } })]),
      ),
      'B append',
    )
    const merged = mustOk(
      Gateway.GitGateway_convergeStore(rawB, gatewayRunner(ws.client('b')), 8, 'origin'),
      'B converge',
    )

    assert.deepEqual(eventPaths(rawB, merged.RootOid), [
      'events/aa/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.jsonl',
      'events/bb/bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb.jsonl',
    ])
    assert.equal(readRemoteStoreOid(ws.bare), snapshotOid(merged))
    assert.equal(remoteHasObject(ws.bare, snapshotOid(merged)), true)
  })
})

test('lease_rejection_refetches_and_bounded_retry_succeeds', () => {
  withWorkspace(['seed', 'rival', 'local'], (ws) => {
    const seedRaw = openProcessStore(ws.client('seed'))
    const seedEs = Store.EventStore_create(seedRaw)
    const seedSnap = mustOk(
      seedEs.Append(
        seedEs.OpenSnapshot(),
        toList([envelope({ id: 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa', payload: { n: 1 } })]),
      ),
      'seed',
    )
    mustOk(Gateway.GitGateway_convergeStore(seedRaw, gatewayRunner(ws.client('seed')), 8, 'origin'), 'seed upload')
    const seedOid = snapshotOid(seedSnap)
    assert.equal(readRemoteStoreOid(ws.bare), seedOid)

    // Rival prepares a competing tip offline, then injects it during local's first push.
    const rivalRaw = openProcessStore(ws.client('rival'))
    mustOk(Gateway.GitGateway_convergeStore(rivalRaw, gatewayRunner(ws.client('rival')), 8, 'origin'), 'rival fetch')
    const rivalEs = Store.EventStore_create(rivalRaw)
    const rivalSnap = mustOk(
      rivalEs.Append(
        rivalEs.OpenSnapshot(),
        toList([envelope({ id: 'rrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrr', payload: { n: 9 } })]),
      ),
      'rival append',
    )
    const rivalOid = snapshotOid(rivalSnap)

    const localRaw = openProcessStore(ws.client('local'))
    const localEs = Store.EventStore_create(localRaw)
    mustOk(
      localEs.Append(
        localEs.OpenSnapshot(),
        toList([envelope({ id: 'bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb', payload: { n: 2 } })]),
      ),
      'local append',
    )

    let pushAttempts = 0
    let leaseRejections = 0
    const wrap = (base) => (argsEnv) => {
      const args = listItems(Array.isArray(argsEnv) ? argsEnv[0] : argsEnv)
      if (args.includes('push')) {
        pushAttempts += 1
        if (pushAttempts === 1) {
          const injected = leasePushStore(ws.client('rival'), rivalOid, seedOid)
          assert.equal(injected.code, 0, `rival inject should succeed: ${injected.stderr}`)
          assert.equal(readRemoteStoreOid(ws.bare), rivalOid)
        }
      }
      const result = base(argsEnv)
      if (args.includes('push') && result[0] !== 0) leaseRejections += 1
      return result
    }

    const merged = mustOk(
      Gateway.GitGateway_convergeStore(localRaw, gatewayRunner(ws.client('local'), wrap), 8, 'origin'),
      'local retry converge',
    )

    assert.ok(pushAttempts >= 2, `expected retry push, got ${pushAttempts}`)
    assert.equal(leaseRejections, 1)
    assert.deepEqual(eventPaths(localRaw, merged.RootOid), [
      'events/aa/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.jsonl',
      'events/bb/bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb.jsonl',
      'events/rr/rrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrr.jsonl',
    ])
    assert.equal(readRemoteStoreOid(ws.bare), snapshotOid(merged))
  })
})

test('lease_rejection_bounded_retry_exhausted', () => {
  withWorkspace(['seed', 'rival', 'local'], (ws) => {
    const seedRaw = openProcessStore(ws.client('seed'))
    const seedEs = Store.EventStore_create(seedRaw)
    const seedSnap = mustOk(
      seedEs.Append(
        seedEs.OpenSnapshot(),
        toList([envelope({ id: 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa', payload: { n: 1 } })]),
      ),
      'seed',
    )
    mustOk(Gateway.GitGateway_convergeStore(seedRaw, gatewayRunner(ws.client('seed')), 8, 'origin'), 'seed upload')
    const seedOid = snapshotOid(seedSnap)

    const rivalRaw = openProcessStore(ws.client('rival'))
    mustOk(Gateway.GitGateway_convergeStore(rivalRaw, gatewayRunner(ws.client('rival')), 8, 'origin'), 'rival fetch')
    const rivalEs = Store.EventStore_create(rivalRaw)

    // Pre-build a chain of rival tips so each push attempt sees a newer remote tip.
    const rivalTips = []
    let base = rivalEs.OpenSnapshot()
    for (const id of [
      'r1r1r1r1r1r1r1r1r1r1r1r1r1r1r1r1r1r1r1r1',
      'r2r2r2r2r2r2r2r2r2r2r2r2r2r2r2r2r2r2r2r2',
      'r3r3r3r3r3r3r3r3r3r3r3r3r3r3r3r3r3r3r3r3',
    ]) {
      const snap = mustOk(rivalEs.Append(base, toList([envelope({ id, payload: { rival: id.slice(0, 2) } })])), id)
      rivalTips.push(snapshotOid(snap))
      base = snap
    }

    const localRaw = openProcessStore(ws.client('local'))
    const localEs = Store.EventStore_create(localRaw)
    mustOk(
      localEs.Append(
        localEs.OpenSnapshot(),
        toList([envelope({ id: 'bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb', payload: { n: 2 } })]),
      ),
      'local append',
    )

    let pushAttempts = 0
    let expectedOld = seedOid
    const wrap = (baseRunner) => (argsEnv) => {
      const args = listItems(Array.isArray(argsEnv) ? argsEnv[0] : argsEnv)
      if (args.includes('push')) {
        const rivalOid = rivalTips[Math.min(pushAttempts, rivalTips.length - 1)]
        const injected = leasePushStore(ws.client('rival'), rivalOid, expectedOld)
        assert.equal(injected.code, 0, `rival inject #${pushAttempts}: ${injected.stderr}`)
        expectedOld = rivalOid
        pushAttempts += 1
      }
      return baseRunner(argsEnv)
    }

    const err = mustErr(
      Gateway.GitGateway_convergeStore(localRaw, gatewayRunner(ws.client('local'), wrap), 2, 'origin'),
      'exhausted',
    )
    assert.equal(caseOf(err), 'ConvergeRetryExhausted')
    assert.ok(pushAttempts >= 3, `expected pushes beyond maxRetries, got ${pushAttempts}`)
  })
})
