import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { mkdtempSync, readdirSync, rmSync, writeFileSync, readFileSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const { randomUUID } = await import("node:crypto");
const { create: createEventStore, dispose: disposeEventStore } = await import("../../../dist/Persistence/EventStore/Surface.js");
const { appendPrepared, appendCommitted, pending } = await import("../../../dist/Repository/Programming/Js/TransactionSurface.js");

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

test('WHAT[repository-programming-025] committed_cut_interrupt_before_commit_keeps_prepared_pending', async () => {
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
test('WHAT[repository-programming-025] committed_cut_interrupt_after_commit_keeps_pair_as_receipt', async () => {
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
test('WHAT[repository-programming-025] committed_cut_unknown_is_never_treated_as_not_written', async () => {
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
test('WHAT[repository-programming-025] committed_cut_foreign_commit_never_releases_local_pending', async () => {
  const workspace = makeDir('wxs-committed-cut-foreign-')
  const common = makeDir('wxs-committed-cut-foreign-events-')
  const local = openStore(common)
  try {
    unwrap(await appendPrepared(local.handle, prepared('tx-local', workspace, [mutation('l.txt', null, 'l')])))
    // A stale/foreign Committed for a different transaction id has no release
    // authority over this pending entry (execution-failure-policy-006: stale/foreign callbacks
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
test('WHAT[repository-programming-025] committed_cut_file_effects_are_owned_by_workflow_not_the_fact', async () => {
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
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { mkdtempSync, readdirSync, rmSync, readFileSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const { randomUUID } = await import("node:crypto");
const { create: createEventStore, dispose: disposeEventStore } = await import("../../../dist/Persistence/EventStore/Surface.js");
const { appendPrepared, appendCommitted, pending } = await import("../../../dist/Repository/Programming/Js/TransactionSurface.js");

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

test('WHAT[repository-programming-025] prepared_cut_appends_facts_without_any_file_effect', async () => {
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
test('WHAT[repository-programming-025] prepared_cut_pending_matches_the_complete_write_set_across_files', async () => {
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
test('WHAT[repository-programming-025] prepared_cut_codec_round_trip_is_fold_accepted_for_full_unicode_paths', async () => {
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
test('WHAT[repository-programming-025] prepared_cut_interrupted_append_leaves_no_local_pending_ahead_of_receipt', async () => {
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
test('WHAT[repository-programming-025] prepared_cut_committed_match_releases_pending_after_both_receipts', async () => {
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
test('WHAT[repository-programming-025] production_store_has_no_optional_fatal_handler_path', async () => {
  const source = readFileSync(
    new URL('../../../src/Wanxiangshu/Repository/Programming/Js/TransactionStore.fs', import.meta.url),
    'utf8',
  )
  assert.doesNotMatch(source, /fatalTripHandler|setFatalTripHandler/)
})
}

{
const { default: test } = await import("node:test");
const { assertFatalBoundary } = await import("../../structured-workflow/tests/support/m6-boundary-proof.mjs");


test('WHAT[repository-programming-025] transaction fatal preserves rollback or cut settlement and one injected fuse', () => assertFatalBoundary('repository-programming'))
}
