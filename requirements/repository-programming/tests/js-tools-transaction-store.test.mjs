// JS-012/015: transaction modules never enumerate EventStore history.
// A crash leaves Prepared as audit evidence; the next process never mutates files to hide the broken tool.

import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

import {
  JsToolsTransactionStore_appendPrepared as appendPrepared,
  JsToolsTransactionStore_appendCommitted as appendCommitted,
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

test('WHAT[REPOSITORY-PROGRAMMING-012] JS012_prepare_then_commit_updates_only_integrator_Current', async () => {
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

test('WHAT[REPOSITORY-PROGRAMMING-015] JS015_prepared_without_committed_is_interrupted_tool_evidence', async () => {
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

test('WHAT[REPOSITORY-PROGRAMMING-015] JS015_reopening_store_never_undoes_an_interrupted_tool', async () => {
  const { dir, cleanup } = sandbox()
  const common = mkdtempSync(join(tmpdir(), 'wxs-txstore-events-'))
  try {
    writeFileSync(join(dir, 'a.txt'), 'new', 'utf8')
    const first = createLocalEventStore({ commonDir: common, writerId: 'before-crash' })
    try {
      unwrap(await appendPrepared(first.store, prepared('tx-3', dir, [mutation('a.txt', 'old', 'new')])))
    } finally {
      first.close()
    }

    const reopened = createLocalEventStore({ commonDir: common, writerId: 'after-crash' })
    try {
      assert.equal(readFileSync(join(dir, 'a.txt'), 'utf8'), 'new')
      assert.equal(listItems(pending(current(reopened.store))).length, 1)
    } finally {
      reopened.close()
    }
  } finally {
    rmSync(common, { recursive: true, force: true })
    cleanup()
  }
})

test('WHAT[REPOSITORY-PROGRAMMING-015] JS015_store_source_has_no_manual_history_reader', async () => {
  const source = readFileSync(new URL('../../../src/Wanxiangshu/Repository/Programming/Js/TransactionStore.fs', import.meta.url), 'utf8')
  assert.doesNotMatch(source, /loadEvents|scanUncommitted|OpenSnapshot|readStreams/)
  assert.doesNotMatch(source, /recoverCurrent|undoIfMatches/)
})
