// tests/unit/git/hook-dispatcher.test.mjs
// Phase 3 Wave B — HookDispatcher: recursion guard + install ownership (§14/§15/§20/§21).

import assert from 'node:assert/strict'
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'
import { caseOf, okResult, errorResult, payloadOf, toList } from '../../../tests/unit/support/domain.mjs'

const Persist = await import('../../../dist/Infrastructure/Persist/StoreTypes.js')
const Hook = await import('../../../dist/Infrastructure/Git/HookDispatcher.js')

const FIXTURES = join(dirname(fileURLToPath(import.meta.url)), 'fixtures')
const SYNC_ENV = 'WANXIANG_GIT_SYNC_ACTIVE'
const MARKER = 'wanxiang-hook-dispatcher'
const OID = 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa'
const ZERO = '0000000000000000000000000000000000000000'

const snapshot = (oid = OID) =>
  new Persist.StoreSnapshot(Persist.RootOidModule_create(Persist.GitObjectIdModule_create(oid)))

const update = ({
  refName = Persist.StoreRef_remoteTracking('origin'),
  oldOid = ZERO,
  newOid = OID,
  isCommitted = true,
} = {}) => new Hook.ReferenceUpdate(refName, oldOid, newOid, isCommitted)

const counters = () => {
  const state = { full: 0, observed: 0, lastFullRemote: undefined, lastObserved: undefined }
  const convergeFull = async (remote) => {
    state.full += 1
    state.lastFullRemote = remote
    return okResult(snapshot('bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb'))
  }
  const convergeObserved = async (remote, observed) => {
    state.observed += 1
    state.lastObserved = { remote, oid: Persist.GitObjectIdModule_value(Persist.RootOidModule_value(observed.RootOid)) }
    return okResult(snapshot('cccccccccccccccccccccccccccccccccccccccc'))
  }
  const deps = Hook.createDeps(convergeFull, convergeObserved, 'origin')
  return { state, deps, convergeFull, convergeObserved }
}

const clearSyncEnv = () => {
  delete process.env[SYNC_ENV]
}

const sandboxHooks = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-hooks-'))
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}

test('GUARD_reference_transaction_noops_when_sync_active', async () => {
  clearSyncEnv()
  const { state, deps } = counters()
  const result = await Hook.withSyncActive(() =>
    Hook.onReferenceTransaction(deps, toList([update()])),
  )
  assert.equal(caseOf(result), 'NoOp')
  assert.equal(payloadOf(result), 'recursion guard')
  assert.equal(state.full, 0)
  assert.equal(state.observed, 0)
  clearSyncEnv()
})

test('GUARD_pre_push_noops_when_sync_active', async () => {
  clearSyncEnv()
  const { state, deps } = counters()
  const result = await Hook.withSyncActive(() => Hook.onPrePush(deps, 'origin'))
  assert.equal(caseOf(result), 'NoOp')
  assert.equal(payloadOf(result), 'recursion guard')
  assert.equal(state.full, 0)
  assert.equal(state.observed, 0)
  clearSyncEnv()
})

test('REF_TX_matching_store_remote_tracking_calls_observed_only', async () => {
  clearSyncEnv()
  const { state, deps } = counters()
  const result = await Hook.onReferenceTransaction(deps, toList([update()]))
  assert.equal(caseOf(result), 'Converged')
  assert.equal(state.observed, 1)
  assert.equal(state.full, 0)
  assert.equal(state.lastObserved.remote, 'origin')
  assert.equal(state.lastObserved.oid, OID)
})

test('REF_TX_unrelated_ref_is_noop', async () => {
  clearSyncEnv()
  const { state, deps } = counters()
  const result = await Hook.onReferenceTransaction(
    deps,
    toList([update({ refName: 'refs/heads/main' })]),
  )
  assert.equal(caseOf(result), 'NoOp')
  assert.match(payloadOf(result), /no store remote-tracking/)
  assert.equal(state.full, 0)
  assert.equal(state.observed, 0)
})

test('REF_TX_uncommitted_state_is_ignored', async () => {
  clearSyncEnv()
  const { state, deps } = counters()
  const result = await Hook.onReferenceTransaction(
    deps,
    toList([update({ isCommitted: false })]),
  )
  assert.equal(caseOf(result), 'NoOp')
  assert.equal(state.observed, 0)
})

test('PRE_PUSH_calls_full_only', async () => {
  clearSyncEnv()
  const { state, deps } = counters()
  const result = await Hook.onPrePush(deps, 'origin')
  assert.equal(caseOf(result), 'Converged')
  assert.equal(state.full, 1)
  assert.equal(state.observed, 0)
  assert.equal(state.lastFullRemote, 'origin')
})

test('PRE_PUSH_maps_converge_error_to_Failed', async () => {
  clearSyncEnv()
  const cases = new Persist.ConvergeError(0, []).cases()
  const err = new Persist.ConvergeError(cases.indexOf('Transport'), ['lease lost'])
  const deps = Hook.createDeps(
    async () => errorResult(err),
    async () => okResult(snapshot()),
    'origin',
  )
  const result = await Hook.onPrePush(deps, 'origin')
  assert.equal(caseOf(result), 'Failed')
  assert.equal(caseOf(payloadOf(result)), 'Transport')
})

test('CLASSIFY_absent_is_Installed', () => {
  assert.equal(caseOf(Hook.classifyExistingHook(undefined)), 'Installed')
})

test('CLASSIFY_marker_is_AlreadyOwned', () => {
  const body = readFileSync(join(FIXTURES, 'wanxiang-pre-push.sh'), 'utf8')
  assert.ok(body.includes(MARKER))
  assert.equal(caseOf(Hook.classifyExistingHook(body)), 'AlreadyOwned')
})

test('CLASSIFY_foreign_is_ForeignHook', () => {
  const body = readFileSync(join(FIXTURES, 'foreign-pre-push.sh'), 'utf8')
  assert.equal(body.includes(MARKER), false)
  assert.equal(caseOf(Hook.classifyExistingHook(body)), 'ForeignHook')
})

test('INSTALL_absent_writes_shim', () => {
  const { dir, cleanup } = sandboxHooks()
  try {
    const shim = readFileSync(join(FIXTURES, 'wanxiang-pre-push.sh'), 'utf8')
    const verdict = Hook.installOrDiagnose(dir, Hook.HookKind.PrePush, shim)
    assert.equal(caseOf(verdict), 'Installed')
    const written = readFileSync(join(dir, 'pre-push'), 'utf8')
    assert.ok(written.includes(MARKER))
  } finally {
    cleanup()
  }
})

test('INSTALL_owned_is_idempotent_refresh', () => {
  const { dir, cleanup } = sandboxHooks()
  try {
    const shim = readFileSync(join(FIXTURES, 'wanxiang-pre-push.sh'), 'utf8')
    writeFileSync(join(dir, 'pre-push'), shim)
    const refreshed = `${shim}\n# refreshed\n`
    const verdict = Hook.installOrDiagnose(dir, Hook.HookKind.PrePush, refreshed)
    assert.equal(caseOf(verdict), 'AlreadyOwned')
    assert.equal(readFileSync(join(dir, 'pre-push'), 'utf8'), refreshed)
  } finally {
    cleanup()
  }
})

test('INSTALL_foreign_diagnoses_incomplete_without_overwrite', () => {
  const { dir, cleanup } = sandboxHooks()
  try {
    const foreign = readFileSync(join(FIXTURES, 'foreign-pre-push.sh'), 'utf8')
    const path = join(dir, 'pre-push')
    writeFileSync(path, foreign)
    const shim = readFileSync(join(FIXTURES, 'wanxiang-pre-push.sh'), 'utf8')
    const verdict = Hook.installOrDiagnose(dir, Hook.HookKind.PrePush, shim)
    assert.equal(caseOf(verdict), 'DiagnoseIncomplete')
    const reason = payloadOf(verdict)
    assert.match(reason, /Git integration incomplete/)
    assert.equal(reason.includes('acceleration disabled'), false)
    assert.equal(readFileSync(path, 'utf8'), foreign)
  } finally {
    cleanup()
  }
})

test('INSTALL_reference_transaction_absent', () => {
  const { dir, cleanup } = sandboxHooks()
  try {
    const shim = readFileSync(join(FIXTURES, 'wanxiang-reference-transaction.sh'), 'utf8')
    const verdict = Hook.installOrDiagnose(dir, Hook.HookKind.ReferenceTransaction, shim)
    assert.equal(caseOf(verdict), 'Installed')
    assert.ok(readFileSync(join(dir, 'reference-transaction'), 'utf8').includes(MARKER))
  } finally {
    cleanup()
  }
})

test('SYNC_ENV_name_matches_shared_literal', () => {
  clearSyncEnv()
  assert.equal(Hook.isSyncActive(), false)
  process.env[SYNC_ENV] = '1'
  assert.equal(Hook.isSyncActive(), true)
  clearSyncEnv()
  assert.equal(Hook.isSyncActive(), false)
  // Shared contract with GitGateway.SyncActiveEnv — same string, never rename alone.
  assert.equal(SYNC_ENV, 'WANXIANG_GIT_SYNC_ACTIVE')
  assert.ok(Hook.shimHeaderComment.includes(MARKER))
})
