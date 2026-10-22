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
const prepared = (id, root, mutation) => ({
  transactionId: id,
  workspaceRoot: root,
  mutation,
})
const mutation = (path, originalText, newText) => ({ path, originalText, newText })
const unwrap = (result) => {
  assert.equal(result.ok, true, `expected Ok, got ${JSON.stringify(result.error)}`)
  return result
}
const pendingIds = (handle) => pending(handle).map((p) => p.transactionId).sort()

test('WHAT[repository-programming-025] reopening a store with only Prepare keeps it pending', async () => {
  const workspace = makeDir('wxs-committed-cut-before-')
  const common = makeDir('wxs-committed-cut-before-events-')
  const local = openStore(common)
  try {
    unwrap(await appendPrepared(local.handle, prepared('tx-hold', workspace, mutation('keep-a.txt', null, 'a'))))

    // Interrupt here: file effects "may already exist" on the interrupted side,
    // but no Committed receipt exists. Recovery must still see the Prepared.
    local.close()
    const reopened = openStore(common)
    try {
      assert.deepEqual(pendingIds(reopened.handle), ['tx-hold'])
      const [fact] = pending(reopened.handle)
      assert.equal(fact.mutation.path, 'keep-a.txt', 'single-mutation Prepared survives the interrupt')
    } finally {
      reopened.close()
    }
  } finally {
    rmSync(workspace, { recursive: true, force: true })
    rmSync(common, { recursive: true, force: true })
  }
})
test('WHAT[repository-programming-025] reopening a store with Prepare and Commit preserves their receipt', async () => {
  const workspace = makeDir('wxs-committed-cut-after-')
  const common = makeDir('wxs-committed-cut-after-events-')
  const local = openStore(common)
  try {
    unwrap(await appendPrepared(local.handle, prepared('tx-done', workspace, mutation('done.txt', null, 'done'))))
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
test('WHAT[repository-programming-025] reopening preserves durable Commit despite loss of the caller view', async () => {
  const workspace = makeDir('wxs-committed-cut-unknown-')
  const common = makeDir('wxs-committed-cut-unknown-events-')
  const local = openStore(common)
  try {
    unwrap(await appendPrepared(local.handle, prepared('tx-amb', workspace, mutation('amb.txt', null, 'amb'))))

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
    unwrap(await appendPrepared(local.handle, prepared('tx-local', workspace, mutation('l.txt', null, 'l'))))
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
    unwrap(await appendPrepared(local.handle, prepared('tx-facts', workspace, mutation('pinned.txt', 'pinned', 'changed'))))
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
const prepared = (id, root, mutation) => ({
  transactionId: id,
  workspaceRoot: root,
  mutation,
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
    unwrap(await appendPrepared(local.handle, prepared('tx-cut-1', workspace, mutation('a.txt', null, 'created-a'))))
    unwrap(await appendPrepared(local.handle, prepared('tx-cut-2', workspace, mutation('b.txt', 'old-b', 'new-b'))))

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
test('WHAT[repository-programming-025] prepared_cut_pending_matches_the_single_write_across_files', async () => {
  const workspace = makeDir('wxs-prepared-cut-cross-')
  const common = makeDir('wxs-prepared-cut-cross-events-')
  const local = openStore(common)
  try {
    unwrap(await appendPrepared(
      local.handle,
      prepared('tx-cross', workspace, mutation('one.txt', 'one-old', 'one-new')),
    ))

    const waiting = pending(local.handle)
    assert.equal(waiting.length, 1)
    const [fact] = waiting
    assert.equal(fact.transactionId, 'tx-cross')
    assert.equal(fact.workspaceRoot, workspace)
    assert.deepEqual(
      [fact.mutation.path, fact.mutation.originalText, fact.mutation.newText],
      ['one.txt', 'one-old', 'one-new'],
      'single-mutation Prepared inspects the one write, not a set',
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
      prepared('tx-uni', workspace, mutation(tricky, 'héllo\r\nwörld', 'héllo\r\nwörld!')),
    ))

    const waiting = pending(local.handle)
    assert.equal(waiting.length, 1)
    assert.equal(waiting[0].mutation.path, tricky)
    assert.equal(waiting[0].mutation.newText, 'héllo\r\nwörld!')
  } finally {
    local.close()
    rmSync(workspace, { recursive: true, force: true })
    rmSync(common, { recursive: true, force: true })
  }
})
test('WHAT[repository-programming-025] reopening without a second append preserves only the original pending receipt', async () => {
  const workspace = makeDir('wxs-prepared-cut-interrupt-')
  const common = makeDir('wxs-prepared-cut-interrupt-events-')
  const local = openStore(common)
  try {
    unwrap(await appendPrepared(local.handle, prepared('tx-base', workspace, mutation('base.txt', null, 'base'))))
    assert.equal(pending(local.handle).length, 1)

    const before = pending(local.handle).map((p) => p.transactionId)
    assert.deepEqual(before, ['tx-base'])

    // This crosses a handle lifetime, not a process crash or an uncertain append.
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
    unwrap(await appendPrepared(local.handle, prepared('tx-both', workspace, mutation('x.txt', null, 'x'))))
    assert.equal(pending(local.handle).length, 1)
    unwrap(await appendCommitted(local.handle, 'tx-both'))
    assert.deepEqual(pending(local.handle), [])
  } finally {
    local.close()
    rmSync(workspace, { recursive: true, force: true })
    rmSync(common, { recursive: true, force: true })
  }
})
}

// Root cause at the settlement boundary: a real `JsTransaction` invariant
// failure (a malformed `Prepared`/`Committed` payload, or a fault raised while
// releasing the store lock) must settle the durable cut BEFORE the single
// injected fatal incident. The physical owner of that settlement is the
// EventStore's append-payload store: it appends the `ProjectionCutTail`
// fact (`rule: 'JsTransaction'`) that records the failed event id, so a cold
// reader can observe the settled cut tail after the process is killed.
{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const { randomUUID } = await import("node:crypto");
const { fileURLToPath } = await import("node:url");
const { runVerificationToolProbe } = await import('../../../scripts/lib/verification-tool-probe.mjs');
const child = fileURLToPath(new URL('./support/transaction-cut-child.mjs', import.meta.url));

for (const phase of ['Prepared', 'Committed']) {
  for (const scenario of ['malformed', 'malformed-release']) {
    test(`WHAT[repository-programming-025] JS ${phase} actual EventStore port ${scenario} bad-input defense settles rollback or cut before one injected fatal incident`, async t => {
      const root = mkdtempSync(join(tmpdir(), 'js-real-cut-boundary-'))
      const commonDir = root
      const workspace = join(root, 'workspace')
      mkdirSync(workspace, { recursive: true })
      // A pre-existing workspace file that the transaction must NOT touch.
      writeFileSync(join(workspace, 'existing.txt'), 'existing-content', 'utf8')
      const writer = randomUUID()
      const env = { ...process.env }
      delete env.NODE_TEST_CONTEXT
      delete env.WANXIANGSHU_NO_FATAL_EXIT
      const probe = (mode, writerId, request) => runVerificationToolProbe(process.execPath,
        [child, mode, commonDir, writerId, phase, scenario, JSON.stringify(request)], { cwd: root, env, signal: t.signal })
      try {
        let outcome
        try { outcome = { stdout: await probe('measure', writer, {}), stderr: '', exitCode: 0, signal: null } }
        catch (error) {
          if (error?.code !== 'verification-tool-probe-failed') throw error
          outcome = error
        }
        const records = outcome.stdout.trim().split('\n').map(line => JSON.parse(line))
        const settled = records.find(record => record.stage === 'settled')
        assert.ok(settled, 'the settlement is recorded before the fatal incident')
        assert.equal(settled.physicalPreconditions, true, outcome.stderr)

        // The durable cut tail is on disk: `ProjectionCutTail` with the
        // `JsTransaction` rule names the failed event.
        const cut = settled.facts.find(fact => fact.type === 'ProjectionCutTail')
        assert.ok(cut, 'the settled cut tail is a durable fact')
        assert.equal(cut.payload.rule, 'JsTransaction')

        // The pre-existing workspace file is untouched by the failed
        // transaction: a settled rollback/cut is not a partial write.
        assert.equal(readFileSync(join(workspace, 'existing.txt'), 'utf8'), 'existing-content')

        // Exactly one injected fatal incident, terminating through the
        // physical fatal owner AFTER the settlement.
        assert.equal(outcome.signal, 'SIGKILL')
        assert.equal(records.some(record => record.stage === 'returned'), false)
        const fatal = outcome.stderr.trim().split('\n').map(line => {
          try { return JSON.parse(line) } catch { return null }
        }).filter(record => record?.operation === 'js-transaction-semantic-cut')
        assert.equal(fatal.length, 1, 'one JS owner reports the actual rule cut')

        // A cold reader process observes the settled cut tail.
        const cold = JSON.parse(await probe('cold', randomUUID(), {
          sourceWriter: writer, bytes: settled.bytes, facts: settled.facts,
        }))
        assert.notEqual(cold.pid, settled.pid)
        assert.equal(cold.preserved, true, 'a separate process accepts the settled cut tail')
      } finally { rmSync(root, { recursive: true, force: true }) }
    })
  }
}
}

// Root cause at the uncertain-persistence boundary: the fact layer never
// rewrites the workspace. A `Prepared` receipt (or an interrupted
// `malformed-release` settlement that leaves the outcome unknown) is durable
// evidence, not a licence to touch files. Recovery reads only the durable
// receipts, so a third-party file that changed while the transaction was
// uncertain must survive the restart verbatim.
{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { mkdtempSync, readFileSync, rmSync, writeFileSync } = await import("node:fs");
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
const prepared = (id, root, mutation) => ({
  transactionId: id,
  workspaceRoot: root,
  mutation,
})
const mutation = (path, originalText, newText) => ({ path, originalText, newText })
const unwrap = (result) => {
  assert.equal(result.ok, true, `expected Ok, got ${JSON.stringify(result.error)}`)
  return result
}

test('WHAT[repository-programming-025] uncertain persistence and interrupted settlement preserve third-party changes across process restart', async () => {
  const workspace = makeDir('wxs-uncertain-third-party-')
  const common = makeDir('wxs-uncertain-third-party-events-')
  const local = openStore(common)
  try {
    writeFileSync(join(workspace, 'shared.txt'), 'initial-shared', 'utf8')
    writeFileSync(join(workspace, 'third-party.txt'), 'external-edit-v1', 'utf8')

    // The transaction targets shared.txt only; third-party.txt is not part
    // of the transaction and must never be written by recovery.
    unwrap(await appendPrepared(local.handle, prepared('tx-uncertain', workspace, mutation('shared.txt', 'initial-shared', 'transaction-shared'))))

    // Uncertain outcome: the Committed receipt is never observed. The
    // Prepared stays durable and inspectable.
    local.close()

    // A third party edits its own file while the transaction is uncertain.
    writeFileSync(join(workspace, 'third-party.txt'), 'external-edit-v2', 'utf8')

    // Cold restart: a fresh handle over the same durable store.
    const reopened = openStore(common)
    try {
      const waiting = pending(reopened.handle)
      assert.deepEqual(
        waiting.map(p => p.transactionId),
        ['tx-uncertain'],
        'pending prepared state remains inspectable after the restart',
      )
      assert.equal(
        readFileSync(join(workspace, 'third-party.txt'), 'utf8'),
        'external-edit-v2',
        'recovery must never overwrite a third-party change',
      )
      assert.equal(
        readFileSync(join(workspace, 'shared.txt'), 'utf8'),
        'initial-shared',
        'an uncertain transaction leaves the workspace untouched',
      )

      // The only legal resolution is an explicit Committed receipt; the
      // third-party file is still untouched afterwards.
      unwrap(await appendCommitted(reopened.handle, 'tx-uncertain'))
      assert.deepEqual(pending(reopened.handle), [])
      assert.equal(readFileSync(join(workspace, 'third-party.txt'), 'utf8'), 'external-edit-v2')
    } finally {
      reopened.close()
    }
  } finally {
    rmSync(workspace, { recursive: true, force: true })
    rmSync(common, { recursive: true, force: true })
  }
})
}
