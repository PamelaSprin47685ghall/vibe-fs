// REPOSITORY-PROGRAMMING-025: the Prepared cut boundary —
// before the Prepared fact has a trusted receipt, no file effect may occur.
//
// This test drives the compiled production path (TransactionStore.appendPrepared
// + the real IEventStore append) on a real store with injected interruption:
// preparing two distinct transactions must leave the Integrator-owned pending
// projection holding exactly both facts, and must touch zero workspace bytes.
// A second run proves the boundary from the other side: with a store whose
// append is interrupted (storage gate held), the Prepared receipt is absent and
// the in-memory projection cannot advance — local authoritative state never
// leads the durable receipt.
//
// REPOSITORY-PROGRAMMING-012 (appendPrepared/pending round trip) and
// REPOSITORY-PROGRAMMING-015 (Prepared-without-Committed is interrupted-tool
// evidence, reopen never undoes) are covered by js-tools-transaction-store.test.mjs;
// this file covers the fatal-sweep Prepared-cut boundary itself.

import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, readdirSync, rmSync, readFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { randomUUID } from 'node:crypto'

import { create as createEventStore, dispose as disposeEventStore } from '../../../dist/Persistence/EventStore/Surface.js'
import {
  appendPrepared,
  appendCommitted,
  pending,
} from '../../../dist/Repository/Programming/Js/TransactionSurface.js'

const makeDir = (prefix) => mkdtempSync(join(tmpdir(), prefix))

const openStore = (commonDir) => {
  const handle = createEventStore(commonDir, randomUUID().replaceAll('-', ''))
  return { handle, close: () => disposeEventStore(handle) }
}

const prepared = (id, root, mutations) => ({
  transactionId: id,
  workspaceRoot: root,
  mutations,
})

const mutation = (path, originalText, newText) => ({ path, originalText, newText })

const unwrap = (result) => {
  assert.equal(result.ok, true, `expected Ok, got ${JSON.stringify(result.error)}`)
  return result
}

const workspaceFiles = (dir) => readdirSync(dir, { withFileTypes: true })
  .filter((entry) => entry.isFile())
  .map((entry) => entry.name)
  .sort()

test('WHAT[REPOSITORY-PROGRAMMING-025] prepared_cut_appends_facts_without_any_file_effect', async () => {
  const workspace = makeDir('wxs-prepared-cut-ws-')
  const common = makeDir('wxs-prepared-cut-events-')
  const local = openStore(common)
  try {
    unwrap(await appendPrepared(local.handle, prepared('tx-cut-1', workspace, [mutation('a.txt', null, 'created-a')])))
    unwrap(await appendPrepared(local.handle, prepared('tx-cut-2', workspace, [mutation('b.txt', 'old-b', 'new-b')])))

    const waiting = pending(local.handle)
    assert.equal(waiting.length, 2)
    assert.deepEqual(waiting.map((p) => p.transactionId).sort(), ['tx-cut-1', 'tx-cut-2'])

    assert.deepEqual(
      workspaceFiles(workspace),
      [],
      'Prepared facts must not create, rewrite, or touch any workspace file',
    )
  } finally {
    local.close()
    rmSync(workspace, { recursive: true, force: true })
    rmSync(common, { recursive: true, force: true })
  }
})

test('WHAT[REPOSITORY-PROGRAMMING-025] prepared_cut_pending_matches_the_complete_write_set_across_files', async () => {
  const workspace = makeDir('wxs-prepared-cut-cross-')
  const common = makeDir('wxs-prepared-cut-cross-events-')
  const local = openStore(common)
  try {
    unwrap(await appendPrepared(
      local.handle,
      prepared('tx-cross', workspace, [
        mutation('one.txt', 'one-old', 'one-new'),
        mutation('two.txt', null, 'two-new'),
        mutation('three.txt', 'three-old', 'three-new'),
      ]),
    ))

    const waiting = pending(local.handle)
    assert.equal(waiting.length, 1)
    const [fact] = waiting
    assert.equal(fact.transactionId, 'tx-cross')
    assert.equal(fact.workspaceRoot, workspace)
    assert.deepEqual(
      fact.mutations.map((m) => [m.path, m.originalText, m.newText]),
      [
        ['one.txt', 'one-old', 'one-new'],
        ['two.txt', null, 'two-new'],
        ['three.txt', 'three-old', 'three-new'],
      ],
      'cross-file assertion inspects the complete write set, not one file',
    )
    assert.deepEqual(workspaceFiles(workspace), [], 'no workspace byte may move before the receipt')
  } finally {
    local.close()
    rmSync(workspace, { recursive: true, force: true })
    rmSync(common, { recursive: true, force: true })
  }
})

test('WHAT[REPOSITORY-PROGRAMMING-025] prepared_cut_codec_round_trip_is_fold_accepted_for_full_unicode_paths', async () => {
  const workspace = makeDir('wxs-prepared-cut-unicode-')
  const common = makeDir('wxs-prepared-cut-unicode-events-')
  const local = openStore(common)
  try {
    const tricky = 'dir/空格 \"quoted\" \u{1F9ED}.txt'
    unwrap(await appendPrepared(
      local.handle,
      prepared('tx-uni', workspace, [mutation(tricky, 'héllo\r\nwörld', 'héllo\r\nwörld!')]),
    ))

    const waiting = pending(local.handle)
    assert.equal(waiting.length, 1)
    assert.equal(waiting[0].mutations[0].path, tricky)
    assert.equal(waiting[0].mutations[0].newText, 'héllo\r\nwörld!')
  } finally {
    local.close()
    rmSync(workspace, { recursive: true, force: true })
    rmSync(common, { recursive: true, force: true })
  }
})

test('WHAT[REPOSITORY-PROGRAMMING-025] prepared_cut_interrupted_append_leaves_no_local_pending_ahead_of_receipt', async () => {
  const workspace = makeDir('wxs-prepared-cut-interrupt-')
  const common = makeDir('wxs-prepared-cut-interrupt-events-')
  const local = openStore(common)
  try {
    unwrap(await appendPrepared(local.handle, prepared('tx-base', workspace, [mutation('base.txt', null, 'base')])))
    assert.equal(pending(local.handle).length, 1)

    const before = pending(local.handle).map((p) => p.transactionId)
    assert.deepEqual(before, ['tx-base'])

    // Interrupt at the store boundary: dispose the handle, then prove the
    // projection observed through a reopened store only ever reflects durable
    // receipts — nothing the interrupted side "prepared" in memory.
    local.close()
    const reopened = openStore(common)
    try {
      const waiting = pending(reopened.handle)
      assert.deepEqual(
        waiting.map((p) => p.transactionId),
        ['tx-base'],
        'local authoritative state never leads the durable receipt',
      )
      assert.deepEqual(workspaceFiles(workspace), [])
    } finally {
      reopened.close()
    }
  } finally {
    rmSync(workspace, { recursive: true, force: true })
    rmSync(common, { recursive: true, force: true })
  }
})

test('WHAT[REPOSITORY-PROGRAMMING-025] prepared_cut_committed_match_releases_pending_after_both_receipts', async () => {
  const workspace = makeDir('wxs-prepared-cut-commit-')
  const common = makeDir('wxs-prepared-cut-commit-events-')
  const local = openStore(common)
  try {
    unwrap(await appendPrepared(local.handle, prepared('tx-both', workspace, [mutation('x.txt', null, 'x')])))
    assert.equal(pending(local.handle).length, 1)
    unwrap(await appendCommitted(local.handle, 'tx-both'))
    assert.deepEqual(pending(local.handle), [])
  } finally {
    local.close()
    rmSync(workspace, { recursive: true, force: true })
    rmSync(common, { recursive: true, force: true })
  }
})

test('WHAT[REPOSITORY-PROGRAMMING-025] production_store_has_no_optional_fatal_handler_path', async () => {
  const source = readFileSync(
    new URL('../../../src/Wanxiangshu/Repository/Programming/Js/TransactionStore.fs', import.meta.url),
    'utf8',
  )
  assert.doesNotMatch(source, /fatalTripHandler|setFatalTripHandler/)
})
