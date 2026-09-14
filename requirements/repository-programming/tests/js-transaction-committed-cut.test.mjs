// REPOSITORY-PROGRAMMING-025: the Committed cut boundary —
// file effects may already exist, so recovery must distinguish
// committed/unknown precisely; an Unknown read is never treated as not-written.
//
// This test drives the compiled production path (TransactionStore.appendPrepared/
// appendCommitted + the real IEventStore append) on a real store with injected
// interruption at both sides of the effect boundary:
//   1. interrupt before Committed  -> pending still holds the Prepared fact
//      (interrupted-tool evidence; reopen never auto-completes, never rolls back);
//   2. interrupt after Committed   -> pending is released; the (Prepared, Committed)
//      pair survives reopen as the commit receipt;
//   3. cross-file write sets      -> the pending/committed observation covers the
//      complete mutation list, not one file;
//   4. unknown outcome            -> a reopened store that cannot find the Committed
//      fact keeps the Prepared pending; the caller must re-verify, never assume
//      not-written.
//
// REPOSITORY-PROGRAMMING-015 (crash leaves Prepared as audit evidence, next
// process never mutates files) is covered by js-tools-transaction-store.test.mjs;
// this file covers the fatal-sweep Committed-cut boundary itself.

import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, readdirSync, rmSync, writeFileSync, readFileSync } from 'node:fs'
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

const pendingIds = (handle) => pending(handle).map((p) => p.transactionId).sort()

test('WHAT[REPOSITORY-PROGRAMMING-025] committed_cut_interrupt_before_commit_keeps_prepared_pending', async () => {
  const workspace = makeDir('wxs-committed-cut-before-')
  const common = makeDir('wxs-committed-cut-before-events-')
  const local = openStore(common)
  try {
    unwrap(await appendPrepared(local.handle, prepared('tx-hold', workspace, [
      mutation('keep-a.txt', null, 'a'),
      mutation('keep-b.txt', 'old-b', 'new-b'),
    ])))

    // Interrupt here: file effects "may already exist" on the interrupted side,
    // but no Committed receipt exists. Recovery must still see the Prepared.
    local.close()
    const reopened = openStore(common)
    try {
      assert.deepEqual(pendingIds(reopened.handle), ['tx-hold'])
      const [fact] = pending(reopened.handle)
      assert.equal(fact.mutations.length, 2, 'complete write set survives the interrupt')
    } finally {
      reopened.close()
    }
  } finally {
    rmSync(workspace, { recursive: true, force: true })
    rmSync(common, { recursive: true, force: true })
  }
})

test('WHAT[REPOSITORY-PROGRAMMING-025] committed_cut_interrupt_after_commit_keeps_pair_as_receipt', async () => {
  const workspace = makeDir('wxs-committed-cut-after-')
  const common = makeDir('wxs-committed-cut-after-events-')
  const local = openStore(common)
  try {
    unwrap(await appendPrepared(local.handle, prepared('tx-done', workspace, [mutation('done.txt', null, 'done')])))
    unwrap(await appendCommitted(local.handle, 'tx-done'))
    assert.deepEqual(pendingIds(local.handle), [])

    // Interrupt after the Committed receipt: reopen must observe the release,
    // never resurrect the Prepared as pending.
    local.close()
    const reopened = openStore(common)
    try {
      assert.deepEqual(pendingIds(reopened.handle), [], 'committed pair is the receipt; nothing stays pending')
    } finally {
      reopened.close()
    }
  } finally {
    rmSync(workspace, { recursive: true, force: true })
    rmSync(common, { recursive: true, force: true })
  }
})

test('WHAT[REPOSITORY-PROGRAMMING-025] committed_cut_unknown_is_never_treated_as_not_written', async () => {
  const workspace = makeDir('wxs-committed-cut-unknown-')
  const common = makeDir('wxs-committed-cut-unknown-events-')
  const local = openStore(common)
  try {
    unwrap(await appendPrepared(local.handle, prepared('tx-amb', workspace, [mutation('amb.txt', null, 'amb')])))

    // Simulate an ambiguous outcome: the committer side cannot prove whether
    // its Committed append landed (here: the handle is gone before the receipt
    // is observed). Recovery reads only durable receipts: the Prepared is still
    // pending, so the transaction is NOT declared not-written — the caller must
    // re-verify against the durable pair, never assume absence.
    local.close()
    const reopened = openStore(common)
    try {
      assert.deepEqual(
        pendingIds(reopened.handle),
        ['tx-amb'],
        'Unknown outcome keeps Prepared pending; never auto-treated as not-written',
      )

      // The only legal resolution is an explicit Committed receipt for the same
      // transaction id — which the fold accepts idempotently-adjacent by
      // releasing the exact pending entry, not by rewriting history.
      unwrap(await appendCommitted(reopened.handle, 'tx-amb'))
      assert.deepEqual(pendingIds(reopened.handle), [])
    } finally {
      reopened.close()
    }
  } finally {
    rmSync(workspace, { recursive: true, force: true })
    rmSync(common, { recursive: true, force: true })
  }
})

test('WHAT[REPOSITORY-PROGRAMMING-025] committed_cut_foreign_commit_never_releases_local_pending', async () => {
  const workspace = makeDir('wxs-committed-cut-foreign-')
  const common = makeDir('wxs-committed-cut-foreign-events-')
  const local = openStore(common)
  try {
    unwrap(await appendPrepared(local.handle, prepared('tx-local', workspace, [mutation('l.txt', null, 'l')])))
    // A stale/foreign Committed for a different transaction id has no release
    // authority over this pending entry (EXECFAIL-006: stale/foreign callbacks
    // have no release authority).
    unwrap(await appendCommitted(local.handle, 'tx-foreign'))
    assert.deepEqual(
      pendingIds(local.handle),
      ['tx-local'],
      'foreign Committed must not release an unrelated pending Prepared',
    )
  } finally {
    local.close()
    rmSync(workspace, { recursive: true, force: true })
    rmSync(common, { recursive: true, force: true })
  }
})

test('WHAT[REPOSITORY-PROGRAMMING-025] committed_cut_file_effects_are_owned_by_workflow_not_the_fact', async () => {
  // The fact layer never touches the filesystem: even a fully committed pair
  // leaves the workspace byte-identical. File effects belong to the workflow
  // adapter (JsMutationFs.commitPlan), observed here by asserting the store
  // path alone moves zero bytes.
  const workspace = makeDir('wxs-committed-cut-facts-')
  const common = makeDir('wxs-committed-cut-facts-events-')
  const local = openStore(common)
  try {
    writeFileSync(join(workspace, 'pinned.txt'), 'pinned', 'utf8')
    unwrap(await appendPrepared(local.handle, prepared('tx-facts', workspace, [mutation('pinned.txt', 'pinned', 'changed')])))
    unwrap(await appendCommitted(local.handle, 'tx-facts'))
    assert.equal(readFileSync(join(workspace, 'pinned.txt'), 'utf8'), 'pinned')
    assert.deepEqual(
      readdirSync(workspace).sort(),
      ['pinned.txt'],
      'fact appends must not create, rewrite, or remove workspace files',
    )
  } finally {
    local.close()
    rmSync(workspace, { recursive: true, force: true })
    rmSync(common, { recursive: true, force: true })
  }
})
