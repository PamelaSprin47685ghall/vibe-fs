import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { readFile, readdir } = await import("node:fs/promises");
const { mkdtempSync, rmSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const { default: test } = await import("node:test");
const eventStore = await import("../../../dist/Persistence/EventStore/Surface.js");

const id = (n) => n.toString(16).padStart(40, '0')
const event = (id, parents = []) => ({
  id,
  stream: 'append/law',
  type: 'JobRequested',
  parents,
  payload: { id },
  payloadRefs: [],
})
const withTemp = (fn) => {
  const base = mkdtempSync(join(tmpdir(), 'wxs-append-law-'))
  return fn(base)
}

test('WHAT[durable-events-006] duplicate_same_identity_is_idempotent_but_collision_is_rejected', async () => {
  const dir = withTemp((base) => base)
  const store = eventStore.create(dir, 'collision-law')
  try {
    const same = event(id(1))
    assert.equal((await eventStore.append(store, [same])).ok, true)
    assert.equal((await eventStore.append(store, [same])).ok, true)
    const conflict = { ...same, payload: { id: 'different' } }
    assert.equal((await eventStore.append(store, [conflict])).ok, false)
  } finally {
    eventStore.dispose(store)
    rmSync(dir, { recursive: true, force: true })
  }
})
}

{
const { default: assert } = await import("node:assert/strict");
const { execFileSync } = await import("node:child_process");
const { existsSync, mkdtempSync, readFileSync, rmSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const { default: test } = await import("node:test");
const journal = await import("../../../dist/Persistence/Journal/Surface.js");

const CLOSED_AGENT = {
  family: 'Companion',
  case: 'CompanionBloggerClosed',
  payload: { SessionId: 'ses_es_writer' },
}
const mustOk = (result, label) => {
  assert.equal(result.ok, true, `${label}: ${JSON.stringify(result.error)}`)
  return result
}
const withRepo = (writerId, fn) => {
  const repo = mkdtempSync(join(tmpdir(), 'wxs-journal-'))
  execFileSync('git', ['init', '--quiet', repo])
  const commonDir = join(repo, '.git')
  return fn(commonDir)
    .finally(() => rmSync(repo, { recursive: true, force: true }))
}

test('WHAT[durable-events-006] append_adds_one_local_line_and_Current_is_already_integrated', async () => {
  await withRepo('journal-append-proof', async (commonDir) => {
    const booted = mustOk(await journal.JournalSurface_bootWithWriterId(commonDir, 'journal-append-proof', 'rt_es_append', 4242, '2026-04-01T00:00:00Z'), 'boot')
    const file = join(commonDir, 'wanxiang', 'events', 'journal-append-proof.ndjson')

    assert.equal(existsSync(file), false)
    const appended = mustOk(
      await journal.JournalSurface_appendAgent(
        booted.journal,
        { kind: 'Session', session: 'ses_es_writer' },
        null,
        CLOSED_AGENT,
      ),
      'append',
    )

    const after = readFileSync(file, 'utf8')
    assert.equal(after.trim().split('\n').length, 2, 'first business append writes RuntimeStarted then the business fact')
    assert.ok(appended.projection)
    journal.JournalSurface_dispose(booted.journal)
  })
})
}
