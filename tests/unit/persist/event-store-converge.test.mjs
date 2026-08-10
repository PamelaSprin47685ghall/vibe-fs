// tests/unit/persist/event-store-converge.test.mjs
// Phase 3 Wave A — EventStore.Converge injection + GitGateway retry/CAS mapping.

import assert from 'node:assert/strict'
import test from 'node:test'
import { caseOf, eventId, isSome, listItems, payloadOf, toList } from '../support/domain.mjs'

const Domain = await import('../../../dist/Domain/EventStore.js')
const Persist = await import('../../../dist/Infrastructure/Persist/StoreTypes.js')
const GitRaw = await import('../../../dist/Infrastructure/Persist/GitRawStore.js')
const Store = await import('../../../dist/Infrastructure/Persist/EventStore.js')
const Gateway = await import('../../../dist/Infrastructure/Git/GitGateway.js')

const streamId = (v) => Domain.EventStreamIdModule_create(v)
const oidValue = (rootOid) => Persist.GitObjectIdModule_value(Persist.RootOidModule_value(rootOid))
const snapshotOid = (snapshot) => oidValue(snapshot.RootOid)
const rootOid = (snapshot) => Persist.RootOidModule_value(snapshot.RootOid)

const envelope = ({
  id = 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa',
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

const createRaw = () => GitRaw.GitRawStore_createInMemory()

const argvOf = (argsEnv) => {
  // Fable calls `run (args, env)` → JS receives a 2-tuple array.
  const args = Array.isArray(argsEnv) ? argsEnv[0] : argsEnv
  return listItems(args)
}

const setRef = (raw, name, oid) => {
  const current = raw.ReadRef(name)
  if (isSome(current) && Persist.GitObjectIdModule_value(current) === Persist.GitObjectIdModule_value(oid)) {
    return
  }
  const expected = isSome(current) ? current : undefined
  assert.equal(raw.CompareAndSwapRef(name, expected, oid), true, `CAS ${name}`)
}

test('StoreRef_remoteTracking_helper', () => {
  assert.equal(Persist.StoreRef_canonical, 'refs/wanxiang/store')
  assert.equal(Persist.StoreRef_remoteTracking('origin'), 'refs/wanxiang/remotes/origin/store')
  assert.equal(Persist.StoreRef_remoteTracking('upstream'), 'refs/wanxiang/remotes/upstream/store')
  assert.throws(() => Persist.StoreRef_remoteTracking('a/b'))
  assert.throws(() => Persist.StoreRef_remoteTracking(''))
})

test('SyncActiveEnv_constant', () => {
  assert.equal(Gateway.GitGateway_SyncActiveEnv, 'WANXIANG_GIT_SYNC_ACTIVE')
})

test('Converge_unbound_transport', () => {
  const es = Store.EventStore_create(createRaw())
  const err = mustErr(es.Converge('origin'))
  assert.equal(caseOf(err), 'Transport')
  assert.match(payloadOf(err), /no GitGateway bound/)
})

test('ConvergeStoreWithObservedRemote_skips_fetch_and_merges', () => {
  const raw = createRaw()
  const seed = Store.EventStore_create(raw)
  const localSnap = mustOk(
    seed.Append(
      seed.OpenSnapshot(),
      toList([envelope({ id: 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa', payload: { n: 1 } })]),
    ),
  )
  const remoteSnap = mustOk(
    seed.Append(
      localSnap,
      toList([envelope({ id: 'bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb', payload: { n: 2 } })]),
    ),
  )

  // Diverge: canonical back to local-only; remote-tracking holds full tip.
  setRef(raw, Persist.StoreRef_canonical, rootOid(localSnap))
  setRef(raw, Persist.StoreRef_remoteTracking('origin'), rootOid(remoteSnap))

  let fetchCount = 0
  let pushCount = 0
  const run = (argsEnv) => {
    const argv = argvOf(argsEnv)
    if (argv.includes('fetch')) {
      fetchCount += 1
      return [1, '', "couldn't find remote ref"]
    }
    if (argv.includes('push')) {
      pushCount += 1
      return [0, '', '']
    }
    return [0, '', '']
  }

  const merged = mustOk(
    Gateway.GitGateway_convergeStoreWithObservedRemote(raw, run, 8, 'origin', remoteSnap),
    'observed converge',
  )
  assert.equal(fetchCount, 0)
  assert.equal(pushCount, 1)
  const blobs = mustOk(GitRaw.GitRawStore_listEventBlobs(raw, merged.RootOid))
  assert.equal(listItems(blobs).length, 2)
})

test('ConvergeStore_lease_reject_retries_then_ok', () => {
  const raw = createRaw()
  const seed = Store.EventStore_create(raw)
  const snap = mustOk(
    seed.Append(seed.OpenSnapshot(), toList([envelope({ id: 'cccccccccccccccccccccccccccccccccccccccc' })])),
  )
  setRef(raw, Persist.StoreRef_remoteTracking('origin'), rootOid(snap))

  let pushAttempts = 0
  const run = (argsEnv) => {
    const argv = argvOf(argsEnv)
    if (argv.includes('push')) {
      pushAttempts += 1
      if (pushAttempts === 1) return [1, '', 'stale info']
      return [0, '', '']
    }
    // fetch after lease reject
    return [0, '', '']
  }

  mustOk(Gateway.GitGateway_convergeStore(raw, run, 8, 'origin'), 'retry converge')
  assert.ok(pushAttempts >= 2)
})

test('ConvergeStore_retry_exhausted', () => {
  const raw = createRaw()
  const seed = Store.EventStore_create(raw)
  const snap = mustOk(
    seed.Append(seed.OpenSnapshot(), toList([envelope({ id: 'dddddddddddddddddddddddddddddddddddddddd' })])),
  )
  setRef(raw, Persist.StoreRef_remoteTracking('origin'), rootOid(snap))

  const run = (argsEnv) => {
    const argv = argvOf(argsEnv)
    if (argv.includes('push')) return [1, '', 'rejected']
    return [0, '', '']
  }

  const err = mustErr(Gateway.GitGateway_convergeStore(raw, run, 2, 'origin'))
  assert.equal(caseOf(err), 'ConvergeRetryExhausted')
})

test('ConvergeStore_cas_rejected_when_maxRetries_zero', () => {
  const raw = createRaw()
  const seed = Store.EventStore_create(raw)
  const snap = mustOk(
    seed.Append(seed.OpenSnapshot(), toList([envelope({ id: 'eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee' })])),
  )
  setRef(raw, Persist.StoreRef_remoteTracking('origin'), rootOid(snap))

  const run = (argsEnv) => {
    const argv = argvOf(argsEnv)
    if (argv.includes('push')) return [1, '', 'rejected']
    return [0, '', '']
  }

  const err = mustErr(Gateway.GitGateway_convergeStore(raw, run, 0, 'origin'))
  assert.equal(caseOf(err), 'ConvergeCasRejected')
})

test('EventStore_createWithConverge_delegates_to_gateway', () => {
  const raw = createRaw()
  const seed = Store.EventStore_create(raw)
  const snap = mustOk(
    seed.Append(seed.OpenSnapshot(), toList([envelope({ id: 'ffffffffffffffffffffffffffffffffffffffff' })])),
  )
  setRef(raw, Persist.StoreRef_remoteTracking('origin'), rootOid(snap))

  const run = (argsEnv) => {
    const argv = argvOf(argsEnv)
    if (argv.includes('fetch')) throw new Error('observed path must not fetch')
    return [0, '', '']
  }

  const es = Store.EventStore_createWithConverge(raw, 8, (remote) =>
    Gateway.GitGateway_convergeStoreWithObservedRemote(raw, run, 8, remote, snap),
  )

  const out = mustOk(es.Converge('origin'))
  assert.equal(snapshotOid(out), snapshotOid(snap))
})

test('GitGateway_bindEventStore_wires_Converge', () => {
  const raw = createRaw()
  const seed = Store.EventStore_create(raw)
  const snap = mustOk(
    seed.Append(seed.OpenSnapshot(), toList([envelope({ id: '1212121212121212121212121212121212121212' })])),
  )
  setRef(raw, Persist.StoreRef_remoteTracking('origin'), rootOid(snap))

  const run = (argsEnv) => {
    const argv = argvOf(argsEnv)
    if (argv.includes('push')) return [0, '', '']
    return [0, '', '']
  }

  const es = Gateway.GitGateway_bindEventStore(raw, run, 8)
  const out = mustOk(es.Converge('origin'))
  assert.equal(typeof snapshotOid(out), 'string')
  assert.equal(snapshotOid(out).length, 40)
})
