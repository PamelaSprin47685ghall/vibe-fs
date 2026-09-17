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

test('WHAT[KNOWLEDGE-REUSE-002] CASE002_fold_captured_and_refreshed_keeps_qa_verbatim', () => {
  const { cases } = project([
    captured('s1', 'Q1', 'A1', [read('a.txt', 'h1')]),
    captured('s2', 'Q2', 'A2', [read('b.txt', 'h2')]),
    refreshed('s1', 'Q1b', 'A1b', [read('a.txt', 'h1'), read('c.txt', 'h3')]),
  ])
  assert.equal(cases.length, 2)
  const s1 = cases.find((c) => c.sessionId === 's1')
  assert.equal(s1.a, 'A1b')
  assert.equal(s1.observations.length, 2)
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

test('WHAT[KNOWLEDGE-REUSE-002] CASE002_fold_captured_and_refreshed_keeps_qa_verbatim', () => {
  const folded = project([
    { kind: 'case-captured', case: caseRec('s1', 'Q1', 'A1', [read('a.txt', 'h1')]) },
    { kind: 'case-captured', case: caseRec('s2', 'Q2', 'A2', [read('b.txt', 'h2')]) },
    { kind: 'case-refreshed', sessionId: 's1', q: 'Q1b', a: 'A1b', observations: [read('a.txt', 'h1'), read('c.txt', 'h3')] },
  ])
  assert.equal(folded.cases.length, 2)
  const s1 = folded.cases.find((c) => c.sessionId === 's1')
  assert.equal(s1.a, 'A1b')
  assert.equal(s1.observations.length, 2)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const eventStore = await import("../../../dist/Persistence/EventStore/Surface.js");
const casebook = await import("../../../dist/Repository/Knowledge/Casebook/Surface.js");
const fetchSurface = await import("../../../dist/Repository/Knowledge/Casebook/FetchSurface.js");
const index = await import("../../../dist/Repository/Knowledge/Casebook/IndexSurface.js");
const bookkeeper = await import("../../../dist/Repository/Knowledge/Casebook/BookkeeperSurface.js");
const { CANONICAL_A, CANONICAL_Q, installBookkeeperRuntime, scriptedBookkeeperPort } = await import("./support/bookkeeper-session-support.mjs");

const fileRead = (path, contentHash) => ({ kind: 'file-read', path, contentHash })
const sandbox = ({ enabled = true } = {}) => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-fetch-'))
  if (enabled) mkdirSync(join(dir, '.wanxiang', 'casebook'), { recursive: true })
  const handle = eventStore.create(dir, 'fetch-tool')
  return { dir, handle, cleanup: () => { eventStore.dispose(handle); rmSync(dir, { recursive: true, force: true }) } }
}
const factory = { tool: { schema: { string: () => ({}) } } }
const record = (sessionId, q, a, observations) => ({ sessionId, q, a, observations, lastAccessOrder: 0 })
const execute = (tool, shelfmark) => tool.execute({ shelfmark }, { sessionID: 'ses', agent: 'inspector' })
const assertFresh = (text) => assert.match(text, /No change was found in the evidence this answer depended on\.|这份答案所依赖的证据没有变化。/i)
const assertRefreshed = (text) => assert.match(text, /The evidence this case depended on had changed\.|这份 case 所依赖的证据已经变化。/i)
const assertNoCase = (text) => assert.match(text, /The Casebook contains no entry under that shelfmark\.|Casebook 在该 shelfmark 下没有条目。/i)
const assertUnavailable = (text) => assert.match(text, /could not be read from this execution context|无法从当前执行环境读取|当前执行上下文无法读取/i)
const assertNoMachineFreshness = (text) => assert.doesNotMatch(text, /\b(session_id|status|freshness|refresh)\s*=/)

test('WHAT[KNOWLEDGE-REUSE-002] CASE004_fetch_returns_exact_canonical_a', async () => {
  const { dir, handle, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'hello', 'utf8')
    const caseRec = record('s1', 'When does CaseFinalize run?', 'A1', [fileRead('a.txt', casebook.contentHash('hello'))])
    assert.equal((await casebook.archive(handle, caseRec)).ok, true)
    const tool = fetchSurface.contract(factory, dir, handle)
    const shelfmark = index.shelfmarkFor('s1', caseRec.q)

    const fresh = await execute(tool, shelfmark)
    assert.match(fresh, /answer = "A1"/)
  } finally {
    cleanup()
  }
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

test('WHAT[KNOWLEDGE-REUSE-002] lifecycle_notePrompt_noteAnswer_tryFinalize_creates_case_once', async () => {
  const { dir, reopen, cleanup } = sandbox()
  try {
    lifecycle.enable(dir)
    const { port } = scriptedBookkeeperPort()
    const key = 'insp-finalize-1'
    installBookkeeperRuntime(port, [key])
    lifecycle.notePrompt(key, 'What owns PromptAuthority?')
    lifecycle.collect(key, 'read', { path: 'a.txt' }, 'hello')
    const rawA = 'PromptAuthority is owned by the Host.'
    lifecycle.noteAnswer(key, rawA)
    assert.equal((await lifecycle.tryFinalize(dir, key)).ok, true)

    const handle = reopen('lifecycle-finalize-read')
    try {
      const fetched = await casebook.fetchCase(handle, 10, key)
      assert.equal(fetched.ok, true)
      assert.equal(fetched.value.q, CANONICAL_Q)
      assert.notEqual(fetched.value.a, rawA)
      assert.equal(fetched.value.a, CANONICAL_A)
      assert.equal(fetched.value.observations.length, 1)
      const publishedA = fetched.value.a
      lifecycle.notePrompt(key, 'Q2')
      lifecycle.noteAnswer(key, 'A2')
      const second = await lifecycle.tryFinalize(dir, key)
      assert.equal(second.ok, false)
      assert.match(String(second.error), /already finalized/)
      assert.equal((await casebook.fetchCase(handle, 10, key)).value.a, publishedA)
    } finally {
      eventStore.dispose(handle)
    }
  } finally {
    bookkeeper.resetRuntime()
    lifecycle.disable()
    cleanup()
  }
})
}
