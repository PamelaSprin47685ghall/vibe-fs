import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const casebook = await import("../../../dist/Repository/Knowledge/Casebook/Surface.js");


test('WHAT[knowledge-reuse-003] CASE003_read_capture_is_typed_and_hashed', () => {
  const obs = casebook.capture('read', { path: 'src/a.fs' }, 'module A // 观察 🧭\r\n')
  assert.notEqual(obs, null)
  assert.equal(obs.kind, 'file-read')
  assert.equal(obs.path, 'src/a.fs')
  assert.equal(obs.contentHash, 'af5ff296cbbdd95c83f69ae8ad47f52a58778dc97c8fadc3c3609313c3a3f35d')
  // empty output → no observation
  assert.equal(casebook.capture('read', { path: 'src/a.fs' }, ''), null)
  // missing path → no observation
  assert.equal(casebook.capture('read', {}, 'text'), null)
})
test('WHAT[knowledge-reuse-003] CASE003_glob_capture_parses_rendered_paths', () => {
  const obs = casebook.capture('glob', { pattern: 'src/**/*.fs' }, 'src/a.fs\nsrc/b.fs\n')
  assert.equal(obs.kind, 'glob-result')
  assert.equal(obs.pattern, 'src/**/*.fs')
  assert.deepEqual(obs.paths, ['src/a.fs', 'src/b.fs'])
})
test('WHAT[knowledge-reuse-003] CASE003_grep_capture_keeps_match_lines', () => {
  const obs = casebook.capture('grep', { pattern: 'TODO' }, 'src/a.fs:3:TODO fix\n')
  assert.equal(obs.kind, 'grep-result')
  assert.equal(obs.pattern, 'TODO')
  assert.equal(obs.matches.length, 1)
})
test('WHAT[knowledge-reuse-003] CASE003_unknown_tool_yields_nothing', () => {
  assert.equal(casebook.capture('executor', { command: 'ls' }, 'x'), null)
  assert.equal(casebook.capture('write', { path: 'a' }, 'x'), null)
})
test('WHAT[knowledge-reuse-003] S63_executor_reading_positives', () => {
  const fileOf = (cmd) => {
    const obs = casebook.ofExecCommand(cmd)
    assert.notEqual(obs, null, `${cmd} must be recognized`)
    return obs.path
  }
  assert.equal(fileOf('cat src/a.fs'), 'src/a.fs')
  assert.equal(fileOf('cat -n src/a.fs'), 'src/a.fs')
  assert.equal(fileOf('head src/a.fs'), 'src/a.fs')
  assert.equal(fileOf('head -n 30 src/a.fs'), 'src/a.fs')
  assert.equal(fileOf('tail -100 src/a.fs'), 'src/a.fs')
  assert.equal(fileOf('tail -f src/a.fs'), 'src/a.fs')
  assert.equal(fileOf("sed -n '20,80p' src/a.fs"), 'src/a.fs')
  assert.equal(fileOf('cat src/a.fs | grep bar'), 'src/a.fs')
})
test('WHAT[knowledge-reuse-003] S63_executor_reading_negatives_skip_safely', () => {
  for (const cmd of ['cat "$(echo x)"', 'sh -c "cat a"', 'bash -c "cat a"', 'grep -r x .', 'ls -la']) {
    assert.equal(casebook.ofExecCommand(cmd), null, `${cmd} must be skipped`)
  }
})
}

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

test('WHAT[knowledge-reuse-003] CASE003_normalize_dedupes_and_orders_observations', () => {
  const obs = [read('a.txt', 'h1'), read('a.txt', 'h1'), glob('**/*.fs', ['x', 'y']), glob('**/*.fs', ['y', 'x'])]
  // same identity → one entry; glob paths order-insensitive
  assert.equal(casebook.normalize(obs).length, 2)
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

test('WHAT[knowledge-reuse-003] CASE003_normalize_dedupes_and_orders_observations', () => {
  const obs = [read('a.txt', 'h1'), read('a.txt', 'h1'), glob('**/*.fs', ['x', 'y']), glob('**/*.fs', ['y', 'x'])]
  // same identity → one entry; glob paths order-insensitive
  assert.equal(casebook.normalize(obs).length, 2)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const casebook = await import("../../../dist/Repository/Knowledge/Casebook/Surface.js");

test('WHAT[knowledge-reuse-003] T17_substantive_access_collects_read_create_edit_delete_move_and_ignores_grep_glob', async () => {
  // Substantive access collects:
  // - successful read: entire file associated (path recorded)
  // - successful create: new path
  // - successful edit: target path
  // - successful delete: original path, ending as Missing
  // - successful move: both old and new paths
  // - grep/glob/ls/mentions: NOT collected
  assert.equal(typeof casebook.recordSubstantiveAccess, 'function', 'casebook must export recordSubstantiveAccess')
  const access = casebook.createAccessTracker()
  access.recordRead('src/lib.fs', 'hash1')
  access.recordCreate('src/new.fs')
  access.recordEdit('src/edit.fs')
  access.recordDelete('src/deleted.fs')
  access.recordMove('src/old.fs', 'src/renamed.fs')
  // grep & glob should be ignored
  access.recordGrep('TODO', 'src/lib.fs')
  access.recordGlob('src/**/*.fs')

  const paths = access.getRelatedPaths()
  assert.deepEqual(paths.sort(), [
    'src/deleted.fs',
    'src/edit.fs',
    'src/lib.fs',
    'src/new.fs',
    'src/old.fs',
    'src/renamed.fs',
  ].sort())
})

test('WHAT[knowledge-reuse-003] T18_grep_glob_ls_and_prose_mentions_do_not_enter_case_related_paths', () => {
  assert.equal(typeof casebook.isSubstantiveTool, 'function', 'casebook must export isSubstantiveTool predicate')
  assert.equal(casebook.isSubstantiveTool('read'), true)
  assert.equal(casebook.isSubstantiveTool('write'), true)
  assert.equal(casebook.isSubstantiveTool('edit'), true)
  assert.equal(casebook.isSubstantiveTool('mv'), true)
  assert.equal(casebook.isSubstantiveTool('rm'), true)
  assert.equal(casebook.isSubstantiveTool('grep'), false)
  assert.equal(casebook.isSubstantiveTool('glob'), false)
  assert.equal(casebook.isSubstantiveTool('ls'), false)
})

test('WHAT[knowledge-reuse-003] T19_failed_or_uncommitted_mutations_do_not_record_modification_access', () => {
  assert.equal(typeof casebook.createAccessTracker, 'function')
  const tracker = casebook.createAccessTracker()
  tracker.recordRead('src/a.fs', 'h1')
  tracker.recordAttemptedMutation('src/b.fs', false) // uncommitted/failed
  const paths = tracker.getRelatedPaths()
  assert.deepEqual(paths, ['src/a.fs'])
})
}
