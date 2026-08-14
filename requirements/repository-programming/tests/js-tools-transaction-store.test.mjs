// FROZEN — 2026-08-14. Rewritten for canonical-Integrator Current.
// Intentionally NOT executed before implementation.
// JS-012/015: transaction modules never enumerate EventStore history.

import assert from 'node:assert/strict'
import test from 'node:test'
import { existsSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

import {
  JsToolsTransactionStore_appendPrepared as appendPrepared,
  JsToolsTransactionStore_appendCommitted as appendCommitted,
  JsToolsTransactionStore_recoverCurrent as recoverCurrent,
} from '../../../dist/Repository/Programming/Js/TransactionStore.js'
import {
  JsTransactionIdModule_create as txId,
  JsTransactionProjectionModule_pending as pending,
} from '../../../dist/Repository/Programming/Js/Transaction.js'
import { createLocalEventStore } from '../../verification-system/tests/support/local-event-store.mjs'
import { listItems, resultOf, toList } from '../../verification-system/tests/support/domain.mjs'

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-txstore-'))
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}

const unwrap = (result) => {
  const r = resultOf(result)
  assert.equal(r.ok, true, `expected Ok, got ${JSON.stringify(r.error)}`)
  return r.value
}

const mutation = (path, originalText, newText) => ({
  Path: path,
  OriginalText: originalText === null ? undefined : originalText,
  NewText: newText,
})

const prepared = (id, root, mutations) => ({
  TransactionId: txId(id),
  WorkspaceRoot: root,
  Mutations: toList(mutations),
})

const current = (store) => store.TryCurrent('JsTransaction')

test('JS012_prepare_then_commit_updates_only_integrator_Current', async () => {
  const local = createLocalEventStore()
  try {
    const p = prepared('tx-1', '/ws', [mutation('a.txt', 'old', 'new')])
    unwrap(await appendPrepared(local.store, p))
    assert.equal(listItems(pending(current(local.store))).length, 1)

    unwrap(await appendCommitted(local.store, txId('tx-1')))
    assert.deepEqual(listItems(pending(current(local.store))), [])
  } finally {
    local.close()
  }
})

test('JS015_prepared_without_committed_is_recovery_Current', async () => {
  const local = createLocalEventStore()
  try {
    const p = prepared('tx-2', '/ws', [mutation('a.txt', 'old', 'new'), mutation('b.txt', null, 'fresh')])
    unwrap(await appendPrepared(local.store, p))
    const waiting = listItems(pending(current(local.store)))
    assert.equal(waiting.length, 1)
    assert.equal(waiting[0].WorkspaceRoot, '/ws')
    assert.equal(listItems(waiting[0].Mutations).length, 2)
  } finally {
    local.close()
  }
})

test('JS015_recoverCurrent_undoes_only_what_the_integrator_says_is_pending', async () => {
  const { dir, cleanup } = sandbox()
  const local = createLocalEventStore()
  try {
    writeFileSync(join(dir, 'a.txt'), 'new', 'utf8')
    writeFileSync(join(dir, 'b.txt'), 'theirs', 'utf8')
    writeFileSync(join(dir, 'c.txt'), 'created', 'utf8')

    const p = prepared('tx-3', dir, [
      mutation('a.txt', 'old', 'new'),
      mutation('b.txt', 'original', 'fresh'),
      mutation('c.txt', null, 'created'),
      mutation('d.txt', null, 'vanished'),
    ])
    unwrap(await appendPrepared(local.store, p))
    recoverCurrent(local.store)

    assert.equal(readFileSync(join(dir, 'a.txt'), 'utf8'), 'old')
    assert.equal(readFileSync(join(dir, 'b.txt'), 'utf8'), 'theirs')
    assert.equal(existsSync(join(dir, 'c.txt')), false)
    assert.equal(existsSync(join(dir, 'd.txt')), false)
  } finally {
    local.close()
    cleanup()
  }
})

test('JS015_store_source_has_no_manual_history_reader', async () => {
  const source = readFileSync(new URL('../../../src/Wanxiangshu/Repository/Programming/Js/TransactionStore.fs', import.meta.url), 'utf8')
  assert.doesNotMatch(source, /loadEvents|scanUncommitted|OpenSnapshot|readStreams/)
  assert.match(source, /TryCurrent "JsTransaction"/)
})
