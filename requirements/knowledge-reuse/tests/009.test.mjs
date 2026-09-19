import test from 'node:test'

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

test('WHAT[knowledge-reuse-009] CASE009_marker_gates_the_surface', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-cbmarker-'))
  try {
    assert.equal(casebook.featureEnabled(dir), false)
    mkdirSync(join(dir, '.wanxiang', 'casebook'), { recursive: true })
    assert.equal(casebook.featureEnabled(dir), true)
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
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

test('WHAT[knowledge-reuse-009] CASE009_fetch_execution_rejects_a_workspace_without_the_marker', async () => {
  const { dir, handle, cleanup } = sandbox({ enabled: false })
  try {
    const tool = fetchSurface.contract(factory, dir, handle)
    const result = await execute(tool, 'Anything · 00000000')

    assertUnavailable(result)
    assert.equal((await casebook.fetchCase(handle, 10, 'ses')).value, null)
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

test('WHAT[knowledge-reuse-009] lifecycle_disabled_marker_skips_publication', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-lifecycle-off-'))
  execFileSync('git', ['init', '--quiet', dir])
  const handle = eventStore.create(join(dir, '.git'), 'lifecycle-off')
  try {
    lifecycle.enable(dir)
    const key = 'insp-off'
    lifecycle.notePrompt(key, 'Q')
    lifecycle.noteAnswer(key, 'A')
    assert.equal((await lifecycle.tryFinalize(dir, key)).ok, true)
    assert.equal((await casebook.fetchCase(handle, 10, key)).value, null)
  } finally {
    lifecycle.disable()
    eventStore.dispose(handle)
    rmSync(dir, { recursive: true, force: true })
  }
})
}
