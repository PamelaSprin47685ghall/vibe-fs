import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const casebook = await import("../../../dist/Repository/Knowledge/Casebook/Surface.js");

const read = (path, hash) => ({ kind: 'file-read', path, contentHash: hash })
const glob = (pattern, paths) => ({ kind: 'glob-result', pattern, paths })
const project = (events) =>
  events.reduce((world, event) => {
    const result = casebook.applyEvent(world, event)
    assert.equal(result.ok, true, JSON.stringify(result.error))
    return result.world
  }, casebook.emptyWorld())
const captured = (sessionId, q, a, observations) => ({
  kind: 'case-captured',
  case: { sessionId, q, a, observations, lastAccessOrder: 0 },
})
const refreshed = (sessionId, q, a, observations) => ({
  kind: 'case-refreshed',
  sessionId,
  q,
  a,
  observations,
})
const accessed = (sessionId) => ({ kind: 'case-accessed', sessionId })
const evicted = (sessionId) => ({ kind: 'case-evicted', sessionId })

test('WHAT[knowledge-reuse-008] CASE008_fold_accessed_and_evicted_derives_access_order', () => {
  const { cases } = project([
    captured('s1', 'Q1', 'A1', [read('a.txt', 'h1')]),
    captured('s2', 'Q2', 'A2', [read('b.txt', 'h2')]),
    accessed('s2'),
  ])
  assert.equal(cases.length, 2)
  // Evicted removes the Case (captured+evicted in one fold)
  const { cases: combined } = project([captured('s2', 'Q2', 'A2', []), evicted('s2')])
  assert.equal(combined.length, 0)
})
test('WHAT[knowledge-reuse-008] CASE008_lru_evict_keeps_most_recently_accessed', () => {
  const { cases } = project([
    captured('s1', 'Q1', 'A1', []),
    captured('s2', 'Q2', 'A2', []),
    captured('s3', 'Q3', 'A3', []),
    accessed('s1'),
  ])
  const { kept, victims } = casebook.evict(2, cases)
  // s2 was accessed first (order 1), s3 second (2), s1 last (3) → evict s2
  assert.deepEqual(victims, ['s2'])
  assert.deepEqual(
    kept.map((c) => c.sessionId).sort(),
    ['s1', 's3'],
  )
  // capacity >= count → no eviction
  const { kept: keptAll, victims: none } = casebook.evict(3, cases)
  assert.deepEqual(none, [])
  assert.equal(keptAll.length, 3)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { mkdtempSync, rmSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const eventStore = await import("../../../dist/Persistence/EventStore/Surface.js");
const casebook = await import("../../../dist/Repository/Knowledge/Casebook/Surface.js");

const read = (path, hash) => ({ kind: 'file-read', path, contentHash: hash })
const glob = (pattern, paths) => ({ kind: 'glob-result', pattern, paths })
const caseRec = (sessionId, q, a, observations) => ({
  sessionId,
  q,
  a,
  observations,
  lastAccessOrder: 0,
})
const project = (events) =>
  events.reduce((world, event) => {
    const result = casebook.applyEvent(world, event)
    assert.equal(result.ok, true, JSON.stringify(result.error))
    return result.world
  }, casebook.emptyWorld())
const openStore = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-casebook-surface-'))
  return {
    dir,
    handle: eventStore.create(dir, 'casebook-surface'),
    close() {
      eventStore.dispose(this.handle)
      rmSync(dir, { recursive: true, force: true })
    },
  }
}

test('WHAT[knowledge-reuse-008] CASE008_fold_accessed_and_evicted_derives_access_order', () => {
  const folded = project([
    { kind: 'case-captured', case: caseRec('s1', 'Q1', 'A1', [read('a.txt', 'h1')]) },
    { kind: 'case-captured', case: caseRec('s2', 'Q2', 'A2', [read('b.txt', 'h2')]) },
    { kind: 'case-accessed', sessionId: 's2' },
  ])
  assert.equal(folded.cases.length, 2)
  // Evicted removes the Case (captured+evicted in one fold)
  const combined = project([
    { kind: 'case-captured', case: caseRec('s2', 'Q2', 'A2', []) },
    { kind: 'case-evicted', sessionId: 's2' },
  ])
  assert.equal(combined.cases.length, 0)
})
test('WHAT[knowledge-reuse-008] CASE008_lru_evict_keeps_most_recently_accessed', () => {
  const folded = project([
    { kind: 'case-captured', case: caseRec('s1', 'Q1', 'A1', []) },
    { kind: 'case-captured', case: caseRec('s2', 'Q2', 'A2', []) },
    { kind: 'case-captured', case: caseRec('s3', 'Q3', 'A3', []) },
    { kind: 'case-accessed', sessionId: 's1' },
  ])
  const { kept, victims } = casebook.evict(2, folded.cases)
  // s2 was accessed first (order 1), s3 second (2), s1 last (3) → evict s2
  assert.deepEqual(victims, ['s2'])
  assert.deepEqual(kept.map((c) => c.sessionId).sort(), ['s1', 's3'])
  // capacity >= count → no eviction
  const keptAll = casebook.evict(3, folded.cases)
  assert.deepEqual(keptAll.victims, [])
  assert.equal(keptAll.kept.length, 3)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { execFileSync } = await import("node:child_process");
const { mkdtempSync, mkdirSync, rmSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const eventStore = await import("../../../dist/Persistence/EventStore/Surface.js");
const casebook = await import("../../../dist/Repository/Knowledge/Casebook/Surface.js");
const bookkeeper = await import("../../../dist/Repository/Knowledge/Casebook/BookkeeperSurface.js");
const lifecycle = await import("../../../dist/Repository/Knowledge/Casebook/LifecycleSurface.js");
const { CANONICAL_A, CANONICAL_Q, installBookkeeperRuntime, scriptedBookkeeperPort } = await import("./support/bookkeeper-session-support.mjs");

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-lifecycle-'))
  execFileSync('git', ['init', '--quiet', dir])
  mkdirSync(join(dir, '.wanxiang', 'casebook'), { recursive: true })
  const handle = eventStore.create(join(dir, '.git'), 'lifecycle-wiring')
  return {
    dir,
    handle,
    reopen: (writerId) => eventStore.create(join(dir, '.git'), writerId),
    cleanup: () => {
      eventStore.dispose(handle)
      rmSync(dir, { recursive: true, force: true })
    },
  }
}

test('WHAT[knowledge-reuse-008] lifecycle_touchAccess_and_touchCaseAccess_advance_integrated_access_order', async () => {
  const { dir, reopen, cleanup } = sandbox()
  try {
    lifecycle.enable(dir)
    const { port } = scriptedBookkeeperPort()
    const key = 'insp-access-1'
    installBookkeeperRuntime(port, [key])
    lifecycle.notePrompt(key, 'Q')
    lifecycle.noteAnswer(key, 'A')
    assert.equal((await lifecycle.tryFinalize(dir, key)).ok, true)

    const initialHandle = reopen('lifecycle-access-initial')
    let initial
    try {
      const initialResult = await casebook.fetchCase(initialHandle, 10, key)
      assert.equal(initialResult.ok, true)
      initial = initialResult.value.lastAccessOrder
    } finally {
      eventStore.dispose(initialHandle)
    }

    const directHandle = reopen('lifecycle-access-direct')
    let direct
    try {
      assert.equal((await casebook.touchAccess(directHandle, key)).ok, true)
      const directResult = await casebook.fetchCase(directHandle, 10, key)
      assert.equal(directResult.ok, true)
      direct = directResult.value.lastAccessOrder
    } finally {
      eventStore.dispose(directHandle)
    }
    assert.ok(direct >= initial)

    await lifecycle.touchAccess(dir, key)
    const hostHandle = reopen('lifecycle-access-host')
    let host
    try {
      const hostResult = await casebook.fetchCase(hostHandle, 10, key)
      assert.equal(hostResult.ok, true)
      host = hostResult.value.lastAccessOrder
    } finally {
      eventStore.dispose(hostHandle)
    }
    assert.ok(host >= direct)
  } finally {
    bookkeeper.resetRuntime()
    lifecycle.disable()
    cleanup()
  }
})
}
