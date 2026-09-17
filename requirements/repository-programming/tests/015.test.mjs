import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const { readUtf8, glob, findAnchor, requireUnique, grep, commitPlan, rollbackPlan } = await import("../../../dist/Repository/Programming/Js/FilesystemSurface.js");

const exact = (text) => ({ kind: 'exact', text })
const regex = (text) => ({ kind: 'regex', text })
const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-jstools-'))
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}
const ok = (result) => result.ok
const codeOf = (result) => result.code
const unwrap = (result) => {
  assert.equal(result.ok, true, `expected Ok, got ${JSON.stringify(result.error)}`)
  return result.value
}

test('WHAT[REPOSITORY-PROGRAMMING-015] JS015_rollbackPlan_is_CAS_and_preserves_third_party_changes', () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'oldA', 'utf8')
    // simulate a partial commit, then roll back
    writeFileSync(join(dir, 'c.txt'), 'oldC', 'utf8')
    const committed = commitPlan(dir, [
      { kind: 'rewrite', path: 'a.txt', expectedCurrent: 'oldA', newText: 'newA' },
      { kind: 'create', path: 'b.txt', newText: 'newB' },
      { kind: 'rewrite', path: 'c.txt', expectedCurrent: 'oldC', newText: 'newC' },
    ])
    assert.equal(ok(committed), true)
    assert.equal(readFileSync(join(dir, 'a.txt'), 'utf8'), 'newA')
    assert.equal(readFileSync(join(dir, 'b.txt'), 'utf8'), 'newB')
    assert.equal(readFileSync(join(dir, 'c.txt'), 'utf8'), 'newC')
    writeFileSync(join(dir, 'c.txt'), 'third-party', 'utf8')
    const rollback = [
      { kind: 'restore', path: 'c.txt', expectedCurrent: 'newC', originalText: 'oldC' },
      { kind: 'removeCreated', path: 'b.txt', expectedCurrent: 'newB' },
      { kind: 'restore', path: 'a.txt', expectedCurrent: 'newA', originalText: 'oldA' },
    ]
    rollbackPlan(dir, rollback)
    assert.equal(readFileSync(join(dir, 'a.txt'), 'utf8'), 'oldA')
    assert.equal(existsSync(join(dir, 'b.txt')), false)
    assert.equal(readFileSync(join(dir, 'c.txt'), 'utf8'), 'third-party')
  } finally {
    cleanup()
  }
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { mkdtempSync, mkdirSync, readFileSync, rmSync, writeFileSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const { randomUUID } = await import("node:crypto");
const { create: createEventStore, dispose: disposeEventStore } = await import("../../../dist/Persistence/EventStore/Surface.js");
const { appendPrepared, appendCommitted, pending } = await import("../../../dist/Repository/Programming/Js/TransactionSurface.js");

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-txstore-'))
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}
const localStore = (commonDir) => {
  const owned = commonDir ?? mkdtempSync(join(tmpdir(), 'wxs-txstore-events-'))
  const gitCommonDir = join(owned, '.git')
  mkdirSync(gitCommonDir, { recursive: true })
  const handle = createEventStore(gitCommonDir, randomUUID().replaceAll('-', ''))
  return { handle, owned, close: () => { disposeEventStore(handle); if (!commonDir) rmSync(owned, { recursive: true, force: true }) } }
}
const unwrap = (result) => {
  assert.equal(result.ok, true, `expected Ok, got ${JSON.stringify(result.error)}`)
  return result
}
const mutation = (path, originalText, newText) => ({
  path,
  originalText,
  newText,
})
const prepared = (id, root, mutations) => ({
  transactionId: id,
  workspaceRoot: root,
  mutations,
})

test('WHAT[REPOSITORY-PROGRAMMING-015] JS015_prepared_without_committed_is_interrupted_tool_evidence', async () => {
  const local = localStore()
  try {
    const p = prepared('tx-2', '/ws', [mutation('a.txt', 'old', 'new'), mutation('b.txt', null, 'fresh')])
    unwrap(await appendPrepared(local.handle, p))
    const waiting = pending(local.handle)
    assert.equal(waiting.length, 1)
    assert.equal(waiting[0].workspaceRoot, '/ws')
    assert.equal(waiting[0].mutations.length, 2)
  } finally {
    local.close()
  }
})
test('WHAT[REPOSITORY-PROGRAMMING-015] JS015_reopening_store_never_undoes_an_interrupted_tool', async () => {
  const { dir, cleanup } = sandbox()
  const common = mkdtempSync(join(tmpdir(), 'wxs-txstore-events-'))
  try {
    writeFileSync(join(dir, 'a.txt'), 'new', 'utf8')
    const first = localStore(common)
    try {
      unwrap(await appendPrepared(first.handle, prepared('tx-3', dir, [mutation('a.txt', 'old', 'new')])))
    } finally {
      first.close()
    }

    const reopened = localStore(common)
    try {
      assert.equal(readFileSync(join(dir, 'a.txt'), 'utf8'), 'new')
      assert.equal(pending(reopened.handle).length, 1)
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
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const { randomUUID } = await import("node:crypto");
const { create: createEventStore, dispose: disposeEventStore } = await import("../../../dist/Persistence/EventStore/Surface.js");
const { run, caseName, rewritten, created, failureCode } = await import("../../../dist/Repository/Programming/Js/WorkflowSurface.js");
const { pending } = await import("../../../dist/Repository/Programming/Js/TransactionSurface.js");

const makeDirectory = (prefix) => mkdtempSync(join(tmpdir(), prefix))
const openStore = (commonDir) => {
  mkdirSync(commonDir, { recursive: true })
  const handle = createEventStore(commonDir, randomUUID().replaceAll('-', ''))
  return { handle, close: () => disposeEventStore(handle) }
}
const runWorkflow = (workspaceRoot, store, body) => run(
  workspaceRoot,
  'Coder',
  'en',
  `class Js extends JsProgram {
  async run() {
${body}
  }
}`,
  2000,
  Date.now() + 60_000,
  1 << 20,
  store,
)
const mutationView = (mutation) => ({
  path: mutation.path,
  originalText: mutation.originalText,
  newText: mutation.newText,
})

test('WHAT[REPOSITORY-PROGRAMMING-015] Adapter JS015_MutationFs_commits_atomically_and_reopen_never_blind_retries_an_ambiguous_partial_commit', async () => {
  const workspace = makeDirectory('wxs-js-mutation-adapter-')
  const committedEvents = makeDirectory('wxs-js-mutation-committed-')
  const interruptedEvents = makeDirectory('wxs-js-mutation-interrupted-')

  try {
    writeFileSync(join(workspace, 'first.txt'), 'first:old', 'utf8')
    writeFileSync(join(workspace, 'second.txt'), 'second:old', 'utf8')

    let committedStore = openStore(committedEvents)
    const committed = await runWorkflow(workspace, committedStore.handle, `    this.rewrite('first.txt', 'first:new');
    this.rewrite('second.txt', 'second:new');
    await this.write('created.txt', 'created:new');
    return { receipt: 'multi-file-committed' };`)

    assert.equal(caseName(committed), 'Succeeded', 'workflow returns a committed receipt')
    assert.deepEqual(rewritten(committed), ['first.txt', 'second.txt'])
    assert.deepEqual(created(committed), ['created.txt'])
    assert.equal(readFileSync(join(workspace, 'first.txt'), 'utf8'), 'first:new')
    assert.equal(readFileSync(join(workspace, 'second.txt'), 'utf8'), 'second:new')
    assert.equal(readFileSync(join(workspace, 'created.txt'), 'utf8'), 'created:new')
    assert.deepEqual(pending(committedStore.handle), [], 'Committed removes Prepared from Current')
    committedStore.close()

    committedStore = openStore(committedEvents)
    assert.deepEqual(pending(committedStore.handle), [], 'Committed projection survives EventStore reopen')
    assert.equal(readFileSync(join(workspace, 'first.txt'), 'utf8'), 'first:new')
    assert.equal(readFileSync(join(workspace, 'second.txt'), 'utf8'), 'second:new')
    assert.equal(readFileSync(join(workspace, 'created.txt'), 'utf8'), 'created:new')
    committedStore.close()

    // The second target snapshots as absent, but its missing parent makes the
    // physical write fail after MutationFs has written the first target. The
    // production adapter must roll that partial effect back and leave Prepared
    // durable because no Committed receipt is safe to claim.
    writeFileSync(join(workspace, 'first.txt'), 'cut:old', 'utf8')
    const interruptedStore = openStore(interruptedEvents)
    const interrupted = await runWorkflow(workspace, interruptedStore.handle, `    this.rewrite('first.txt', 'cut:new');
    await this.write('missing-parent/second.txt', 'must-not-survive');
    return { receipt: 'must-not-commit' };`)

    assert.equal(caseName(interrupted), 'Failed')
    assert.equal(failureCode(interrupted), 'TRANSACTION_COMMIT_FAILED')
    assert.equal(readFileSync(join(workspace, 'first.txt'), 'utf8'), 'cut:old', 'partial first write was rolled back')
    assert.equal(existsSync(join(workspace, 'missing-parent/second.txt')), false)

    const waiting = pending(interruptedStore.handle)
    assert.equal(waiting.length, 1, 'Prepared remains durable after ambiguous physical failure')
    assert.equal(waiting[0].workspaceRoot, workspace)
    assert.equal(typeof waiting[0].transactionId, 'string')
    assert.notEqual(waiting[0].transactionId, '')
    assert.deepEqual(waiting[0].mutations.map(mutationView), [
      { path: 'first.txt', originalText: 'cut:old', newText: 'cut:new' },
      { path: 'missing-parent/second.txt', originalText: null, newText: 'must-not-survive' },
    ])
    interruptedStore.close()

    // Make a retry physically possible only after the interrupted process is
    // gone. Reopening Current must observe evidence, never replay the plan.
    mkdirSync(join(workspace, 'missing-parent'))
    const reopenedInterrupted = openStore(interruptedEvents)
    assert.equal(readFileSync(join(workspace, 'first.txt'), 'utf8'), 'cut:old')
    assert.equal(existsSync(join(workspace, 'missing-parent/second.txt')), false, 'reopen did not blindly retry')
    assert.deepEqual(
      pending(reopenedInterrupted.handle).map((item) => ({
        transactionId: item.transactionId,
        workspaceRoot: item.workspaceRoot,
        mutations: item.mutations.map(mutationView),
      })),
      waiting.map((item) => ({
        transactionId: item.transactionId,
        workspaceRoot: item.workspaceRoot,
        mutations: item.mutations.map(mutationView),
      })),
      'pending projection is durable and unchanged across reopen',
    )
    reopenedInterrupted.close()
  } finally {
    rmSync(workspace, { recursive: true, force: true })
    rmSync(committedEvents, { recursive: true, force: true })
    rmSync(interruptedEvents, { recursive: true, force: true })
  }
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { validateSingleIntent, validateTargets, validateFreshness, preflight, commitPlan, rollbackPlan } = await import("../../../dist/Repository/Programming/Js/TransactionSurface.js");

const ok = (result) => result.ok
const codeOf = (result) => result.code
const rewrite = (path, originalText, newText) => ({ kind: 'rewrite', path, originalText, newText })
const create = (path, text) => ({ kind: 'create', path, text })
const current = { 'a.txt': 'current' }

test('WHAT[REPOSITORY-PROGRAMMING-015] JS015_rollback_plan_is_exact', () => {
  const mutations = [create('b.txt', 'newB'), rewrite('a.txt', 'oldA', 'newA')]
  // rollback restores rewrites and marks creates for removal, reversed order
  assert.deepEqual(rollbackPlan(mutations), [
    { kind: 'removeCreated', path: 'b.txt', expectedCurrent: 'newB' },
    { kind: 'restore', path: 'a.txt', expectedCurrent: 'newA', originalText: 'oldA' },
  ])
})
}
