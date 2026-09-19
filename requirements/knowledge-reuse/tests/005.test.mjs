import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { execFileSync } = await import("node:child_process");
const { mkdtempSync, mkdirSync, rmSync, writeFileSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const eventStore = await import("../../../dist/Persistence/EventStore/Surface.js");
const casebook = await import("../../../dist/Repository/Knowledge/Casebook/Surface.js");
const bookkeeper = await import("../../../dist/Repository/Knowledge/Casebook/BookkeeperSurface.js");
const bookkeeperRefresh = await import("../../../dist/Repository/Knowledge/Casebook/BookkeeperRefreshSurface.js");
const lifecycle = await import("../../../dist/Repository/Knowledge/Casebook/LifecycleSurface.js");

const fileRead = (path, contentHash) => ({ kind: 'file-read', path, contentHash })
const CANONICAL_Q = 'Canonical maintained question'
const CANONICAL_A = 'Summary of Inspector answers across turns.'
const scriptedBookkeeperPort = () => {
  const createCalls = []
  const prompts = []
  const programCalls = []
  const terminals = new Set()
  let seq = 0
  const port = {
    CreateChildSession: async () => {
      throw new Error('Bookkeeper must not attach to a deleted physical parent')
    },
    CreateSiblingSession: async (_ownerSessionId, physicalParentId) => {
      seq += 1
      const child = `bk-child-${seq}`
      createCalls.push({ child, physicalParentId })
      return bookkeeper.acceptedSession(child)
    },
    AbortSession: async () => bookkeeper.aborted(),
    SubscribeTerminal: (_childId, callback) => {
      terminals.add(callback)
      return { Dispose: () => terminals.delete(callback) }
    },
    SendPrompt: async (childSession, text) => {
      prompts.push(text)
      const sid = bookkeeper.sessionValue(childSession)
      const tx = bookkeeper.txIdFor(sid)
      assert.notEqual(tx, '', 'SendPrompt must run against a bound Bookkeeper tx')
      const out = await bookkeeper.runProgram(
        sid,
        `class Js extends JsProgram { async run() { this.setQuestion(${JSON.stringify(CANONICAL_Q)}); this.setAnswer(${JSON.stringify(CANONICAL_A)}); return { changed: true }; } }`,
      )
      assert.equal(String(out).includes('changed = true'), true, out)
      programCalls.push(tx)
      for (const callback of terminals) callback(bookkeeper.sessionId(sid), bookkeeper.completed(sid))
      return bookkeeper.acceptedPrompt()
    },
  }
  return { port, createCalls, prompts, programCalls }
}
const installBookkeeperRuntime = (port, ownerSessionIds) => {
  const installed = bookkeeper.setRuntime(
    port,
    ownerSessionIds.map((sessionId) => ({
      sessionId,
      logicalRunId: `bookkeeper-run-${sessionId}`,
      authorityRootUserMessageId: `bookkeeper-root-${sessionId}`,
      agent: 'engineer',
    })),
  )
  assert.equal(installed.ok, true, installed.error)
}
const record = (sessionId, q, a, observations) => ({ sessionId, q, a, observations, lastAccessOrder: 0 })
const openStore = (dir, writerId) => eventStore.create(join(dir, '.git'), writerId)

test('WHAT[knowledge-reuse-005] CASE006_missing_runtime_keeps_old_case', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-bk-session-noport-'))
  const handle = eventStore.create(dir, 'bookkeeper-session-noport')
  try {
    bookkeeper.resetRuntime()
    writeFileSync(join(dir, 'a.txt'), 'hello', 'utf8')
    assert.equal((await casebook.archive(handle, record('s-noport', 'Q keep', 'A keep', [fileRead('a.txt', casebook.contentHash('hello'))]))).ok, true)
    writeFileSync(join(dir, 'a.txt'), 'changed', 'utf8')
    const refreshed = await bookkeeperRefresh.refreshStale(handle, dir, 's-noport')
    assert.equal(refreshed.ok, false)
    assert.match(String(refreshed.error), /runtime unavailable/)
    const fetched = await casebook.fetchCase(handle, 10, 's-noport')
    assert.equal(fetched.value.q, 'Q keep')
    assert.equal(fetched.value.a, 'A keep')
  } finally {
    eventStore.dispose(handle)
    rmSync(dir, { recursive: true, force: true })
  }
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { mkdtempSync, rmSync, mkdirSync, writeFileSync, readFileSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const { create: createEventStore, dispose: disposeEventStore } = await import("../../../dist/Persistence/EventStore/Surface.js");
const casebook = await import("../../../dist/Repository/Knowledge/Casebook/Surface.js");

const fileRead = (path, contentHash) => ({ kind: 'file-read', path, contentHash })
const globResult = (pattern, paths) => ({ kind: 'glob-result', pattern, paths })
const createCasebookEventStore = () => {
  const commonDir = mkdtempSync(join(tmpdir(), 'wxs-casebook-store-'))
  const store = createEventStore(commonDir, 'casebook-test-writer')
  return {
    store,
    close: () => {
      disposeEventStore(store)
      rmSync(commonDir, { recursive: true, force: true })
    },
  }
}
const unwrap = async (operation) => {
  const result = await operation
  assert.equal(result.ok, true, `expected successful Casebook operation, got ${JSON.stringify(result.error)}`)
  return result.value
}
const caseRec = (sessionId, q, a, observations) => ({
  sessionId,
  q,
  a,
  observations,
  lastAccessOrder: 0,
})
const findCase = async (store, sessionId) => {
  const result = await casebook.fetchCase(store, 10, sessionId)
  assert.equal(result.ok, true, JSON.stringify(result.error))
  return result.value
}

test('WHAT[knowledge-reuse-005] CASE004_005_freshness_check_is_hint_not_proof_reads_Current_only', async () => {
  const local = createCasebookEventStore()
  try {
    await unwrap(casebook.archive(local.store, caseRec('s1', 'Q1', 'A1', [fileRead('a.txt', 'h1')])))
    const fetched = await findCase(local.store, 's1')
    assert.equal(casebook.classifyReplay(fetched.observations, [fileRead('a.txt', 'h1')]), 'fresh')
    assert.equal(casebook.classifyReplay(fetched.observations, [fileRead('a.txt', 'h2')]), 'stale')
  } finally {
    local.close()
  }
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { mkdtempSync, rmSync, writeFileSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const eventStore = await import("../../../dist/Persistence/EventStore/Surface.js");
const casebook = await import("../../../dist/Repository/Knowledge/Casebook/Surface.js");

test('WHAT[knowledge-reuse-005] T23_T24_fetch_uses_diff_and_advances_maintenance_baseline_without_replay_loops', async () => {
  assert.equal(typeof casebook.refreshWithDiff, 'function', 'casebook must export refreshWithDiff')
  const dir = mkdtempSync(join(tmpdir(), 'wxs-kr-t23-'))
  const store = eventStore.create(dir, 'kr-t23-writer')
  try {
    writeFileSync(join(dir, 'a.txt'), 'version-B', 'utf8')
    const initialCase = {
      identity: 'eng-invocation-1',
      sourceTrace: 'trace-1',
      q: 'Task Q',
      a: 'Answer A',
      relatedPaths: ['a.txt'],
      completionFileState: 'state-ref-B',
      maintenanceFileState: 'state-ref-B',
      accessOrder: 0,
    }
    await casebook.archiveCase(store, initialCase)

    // External change B -> C
    writeFileSync(join(dir, 'a.txt'), 'version-C', 'utf8')
    const diff = 'diff --git a/a.txt b/a.txt\n--- a/a.txt\n+++ b/a.txt\n-version-B\n+version-C\n'
    const refreshResult = await casebook.refreshWithDiff(store, 'eng-invocation-1', diff, 'state-ref-C', {
      q: 'Task Q',
      a: 'Updated Answer A for C',
    })

    assert.equal(refreshResult.ok, true)
    const fetched = await casebook.fetchCaseByIdentity(store, 'eng-invocation-1')
    assert.equal(fetched.a, 'Updated Answer A for C')
    assert.equal(fetched.completionFileState, 'state-ref-B', 'completionFileState must NEVER be rewritten')
    assert.equal(fetched.maintenanceFileState, 'state-ref-C', 'maintenanceFileState advances to C')
  } finally {
    eventStore.dispose(store)
    rmSync(dir, { recursive: true, force: true })
  }
})
}
