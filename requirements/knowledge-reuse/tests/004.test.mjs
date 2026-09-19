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

test('WHAT[knowledge-reuse-004] CASE004_classifyReplay_fresh_only_on_exact_normalized_equality', () => {
  const stored = [read('a.txt', 'h1'), glob('**/*.fs', ['x', 'y'])]
  // exact replay (order-insensitive glob) → Fresh
  assert.equal(
    casebook.classifyReplay(stored, [glob('**/*.fs', ['y', 'x']), read('a.txt', 'h1')]),
    'fresh',
  )
  // content changed → Stale
  assert.equal(casebook.classifyReplay(stored, [read('a.txt', 'h2'), glob('**/*.fs', ['x', 'y'])]), 'stale')
  // file deleted → Stale
  assert.equal(casebook.classifyReplay(stored, [glob('**/*.fs', ['x', 'y'])]), 'stale')
  // extra result → Stale
  assert.equal(casebook.classifyReplay(stored, [read('a.txt', 'h1'), glob('**/*.fs', ['x', 'y', 'z'])]), 'stale')
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

test('WHAT[knowledge-reuse-004] CASE004_005_workflow_archive_fetch_closed_loop_reads_Current_only', async () => {
  const local = createCasebookEventStore()
  try {
    await unwrap(casebook.archive(local.store, caseRec('s1', 'Q1', 'A1', [fileRead('a.txt', 'h1')])))
    const fetched = await findCase(local.store, 's1')
    assert.equal(fetched.a, 'A1')
  } finally {
    local.close()
  }
})
test('WHAT[knowledge-reuse-004] CASE004_refresh_and_needsRefresh_replay_the_same_Current', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-cbrefresh-'))
  const local = createCasebookEventStore()
  try {
    writeFileSync(join(dir, 'a.txt'), 'hello', 'utf8')
    await unwrap(casebook.archive(local.store, caseRec('s1', 'Q1', 'A1', [fileRead('a.txt', '2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824')])))
    assert.equal((await casebook.needsRefresh(local.store, 10, 's1', dir)).value, false)
    writeFileSync(join(dir, 'a.txt'), 'changed', 'utf8')
    assert.equal((await casebook.needsRefresh(local.store, 10, 's1', dir)).value, true)
    await unwrap(casebook.refresh(local.store, 's1', 'Q1b', 'A1b', [fileRead('a.txt', 'd67e2e944994496c8d8ec76eed0cf9f09679448d584b532bebf941852a37f5ed')]))
    assert.equal((await findCase(local.store, 's1')).a, 'A1b')
  } finally {
    local.close()
    rmSync(dir, { recursive: true, force: true })
  }
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

test('WHAT[knowledge-reuse-004] CASE004_classifyReplay_fresh_only_on_exact_normalized_equality', () => {
  const stored = [read('a.txt', 'h1'), glob('**/*.fs', ['x', 'y'])]
  // exact replay (order-insensitive glob) → fresh
  assert.equal(casebook.classifyReplay(stored, [glob('**/*.fs', ['y', 'x']), read('a.txt', 'h1')]), 'fresh')
  // content changed → stale
  assert.equal(casebook.classifyReplay(stored, [read('a.txt', 'h2'), glob('**/*.fs', ['x', 'y'])]), 'stale')
  // file deleted → stale
  assert.equal(casebook.classifyReplay(stored, [glob('**/*.fs', ['x', 'y'])]), 'stale')
  // extra result → stale
  assert.equal(casebook.classifyReplay(stored, [read('a.txt', 'h1'), glob('**/*.fs', ['x', 'y', 'z'])]), 'stale')
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

test('WHAT[knowledge-reuse-004] CASE004_fetch_uses_shelfmark_and_replays_before_refreshing', async () => {
  const { dir, handle, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'hello', 'utf8')
    const caseRec = record('s1', 'When does CaseFinalize run?', 'A1', [fileRead('a.txt', casebook.contentHash('hello'))])
    assert.equal((await casebook.archive(handle, caseRec)).ok, true)
    const tool = fetchSurface.contract(factory, dir, handle)
    assert.equal(tool.name, 'fetch')
    const shelfmark = index.shelfmarkFor('s1', caseRec.q)

    const fresh = await execute(tool, shelfmark)
    assertFresh(fresh)
    assertNoMachineFreshness(fresh)
    assert.equal(fresh.includes('s1'), false)

    writeFileSync(join(dir, 'a.txt'), 'changed', 'utf8')
    const { port, createCalls, programCalls } = scriptedBookkeeperPort()
    await installBookkeeperRuntime(port, ['s1'])
    const afterChange = await execute(tool, shelfmark)
    assertRefreshed(afterChange)

    assert.equal(afterChange.includes(CANONICAL_A), true)
    assert.equal(createCalls.length, 1)
    assert.equal(programCalls.length >= 1, true)

    const again = await execute(tool, index.shelfmarkFor('s1', CANONICAL_Q))
    assertFresh(again)
    const missing = await execute(tool, 'Nothing here · 00000000')
    assertNoCase(missing)

  } finally {
    bookkeeper.resetRuntime()
    cleanup()
  }
})
test('WHAT[knowledge-reuse-004] CASE009_fetch_never_writes_the_subject', async () => {
  const { dir, handle, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'hello', 'utf8')
    await casebook.archive(handle, record('s1', 'Q', 'A', [fileRead('a.txt', casebook.contentHash('hello'))]))
    const tool = fetchSurface.contract(factory, dir, handle)
    await execute(tool, index.shelfmarkFor('s1', 'Q'))
    assert.equal(readFileSync(join(dir, 'a.txt'), 'utf8'), 'hello')
  } finally {
    cleanup()
  }
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { mkdtempSync, rmSync, writeFileSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const casebook = await import("../../../dist/Repository/Knowledge/Casebook/Surface.js");

test('WHAT[knowledge-reuse-004] T20_completion_boundary_freezes_baseline_B_and_tracks_diff_B_to_C', async () => {
  // Read A -> change to B -> end trajectory -> external changes to C
  // Initial completionFileState = B; maintenanceFileState = B; diff = B -> C
  assert.equal(typeof casebook.freezeCompletionState, 'function', 'casebook must export freezeCompletionState')
  const dir = mkdtempSync(join(tmpdir(), 'wxs-kr-t20-'))
  try {
    writeFileSync(join(dir, 'a.txt'), 'version-B', 'utf8')
    const baseline = await casebook.freezeCompletionState(dir, ['a.txt'])
    assert.equal(baseline.get('a.txt')?.kind, 'Present')
    assert.equal(baseline.get('a.txt')?.contentHash, casebook.contentHash('version-B'))

    // External change to C
    writeFileSync(join(dir, 'a.txt'), 'version-C', 'utf8')
    const diff = await casebook.computeMaintenanceDiff(dir, baseline)
    assert.equal(diff.hasDiff, true)
    assert.match(diff.diffSummary, /version-B.*version-C/s)
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})

test('WHAT[knowledge-reuse-004] T27_devops_self_repair_updates_related_case_maintenance_baseline_without_fake_engineer_source', async () => {
  // DevOps repair updates related cases from B -> C, but does NOT become an Engineer case source.
  assert.equal(typeof casebook.applyExternalChangeToCase, 'function', 'casebook must apply external change B->C to existing case')
  const updated = casebook.applyExternalChangeToCase({
    identity: 'eng-1',
    completionFileState: 'state-B',
    maintenanceFileState: 'state-B',
    diff: 'B -> C',
    newState: 'state-C',
  })
  assert.equal(updated.completionFileState, 'state-B')
  assert.equal(updated.maintenanceFileState, 'state-C')
  assert.equal(updated.sourceRole, 'engineer', 'must retain original engineer source, not forge devops source')
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

test('WHAT[knowledge-reuse-004] dual_baselines_freeze_pipeline_and_refresh_maintenance_evolution', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-kr-dual-baseline-'))
  const store = eventStore.create(dir, 'kr-dual-baseline-writer')
  try {
    const filePath = join(dir, 'code.fs')
    writeFileSync(filePath, 'let initial = 1', 'utf8')

    // 1. Freeze baseline via store
    const baselineJson = await casebook.freezeCompletionState(store, dir, ['code.fs', 'nonexistent.fs'])
    assert.equal(typeof baselineJson, 'string')
    const parsed = JSON.parse(baselineJson)

    // Code file is Present with payloadRef and sha256
    assert.equal(parsed['code.fs']?.kind, 'Present')
    assert.ok(parsed['code.fs']?.payloadRef)
    assert.equal(parsed['code.fs']?.sha256, casebook.contentHash('let initial = 1'))

    // Nonexistent file is Missing
    assert.equal(parsed['nonexistent.fs']?.kind, 'Missing')

    // Read payload from store to verify durable persistence
    const payloadBytes = await eventStore.readPayload(store, parsed['code.fs'].payloadRef)
    assert.ok(payloadBytes, 'payload must be readable from store')
    const readText = new TextDecoder().decode(payloadBytes)
    assert.equal(readText, 'let initial = 1')

    // 2. Finalize case with this baseline
    const identity = 'test-dual-case-1'
    const finalizeRes = await casebook.finalizeEngineerCase(
      store,
      identity,
      'trace-1',
      'How to implement?',
      'Answer initial',
      ['code.fs', 'nonexistent.fs'],
      baselineJson
    )
    assert.equal(finalizeRes.kind, 'finalized')

    // Fetch case and assert dual baselines are identical at completion
    const initialCase = await casebook.fetchCaseByIdentity(store, identity)
    assert.equal(initialCase.completionFileState, baselineJson)
    assert.equal(initialCase.maintenanceFileState, baselineJson)

    // 3. External modification triggers maintenance drift
    writeFileSync(filePath, 'let initial = 2 // modified', 'utf8')

    // Compute diff against baseline
    const diffObj = await casebook.computeMaintenanceDiff(dir, baselineJson)
    assert.equal(diffObj.hasDiff, true)

    // Freeze new target state
    const newTargetJson = await casebook.freezeCompletionState(store, dir, ['code.fs', 'nonexistent.fs'])
    const parsedNew = JSON.parse(newTargetJson)
    assert.notEqual(parsedNew['code.fs'].payloadRef, parsed['code.fs'].payloadRef)
    assert.equal(parsedNew['code.fs'].sha256, casebook.contentHash('let initial = 2 // modified'))

    // Refresh case with new maintenance baseline
    const refreshRes = await casebook.refreshWithDiff(
      store,
      identity,
      diffObj.diffSummary,
      newTargetJson,
      'How to implement?',
      'Answer maintained'
    )
    assert.equal(refreshRes.ok, true)

    // 4. Assert: completionFileState is unchanged, maintenanceFileState is updated to new blob baseline
    const maintainedCase = await casebook.fetchCaseByIdentity(store, identity)
    assert.equal(maintainedCase.completionFileState, baselineJson, 'completionFileState must NEVER be changed')
    assert.equal(maintainedCase.maintenanceFileState, newTargetJson, 'maintenanceFileState must advance to new baseline')
    assert.equal(maintainedCase.a, 'Answer maintained')
  } finally {
    eventStore.dispose(store)
    rmSync(dir, { recursive: true, force: true })
  }
})
}
