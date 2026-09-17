import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, utimesSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import * as eventStore from '../../../dist/Persistence/EventStore/Surface.js'
import * as retention from '../../../dist/Persistence/EventStore/RetentionSurface.js'

const DAY = 24 * 60 * 60 * 1000

const A = 'a'.repeat(40)

const B = 'b'.repeat(40)

const make = (id, stream, parents = []) => ({ id, stream, type: 'JobRequested', parents, payload: {}, payloadRefs: [] })

const canonicalLine = (event) => JSON.stringify({
  event_id: event.id,
  event_type: event.type,
  parents: [...event.parents].sort(),
  payload: event.payload,
  payload_refs: [...event.payloadRefs].sort(),
  stream_id: event.stream,
}) + '\n'

const writeCanonicalEvents = (commonDir, writerId, events) => {
  const directory = join(commonDir, 'wanxiang', 'events')
  mkdirSync(directory, { recursive: true })
  const path = join(directory, `${writerId}.ndjson`)
  writeFileSync(path, events.map(canonicalLine).join(''))
  return path
}

const withRepo = async (fn) => {
  const root = mkdtempSync(join(tmpdir(), 'wxs-writer-retention-'))
  execFileSync('git', ['init', '-q', root])
  const commonDir = join(root, '.git')
  try {
    await fn(root, commonDir)
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
}

const writeEvent = async (commonDir, writerId, event) => {
  const handle = eventStore.create(commonDir, writerId)
  try {
    const result = await eventStore.append(handle, [event])
    assert.equal(result.ok, true, result.ok ? '' : JSON.stringify(result.error))
  } finally {
    eventStore.dispose(handle)
  }
  return join(commonDir, 'wanxiang', 'events', `${writerId}.ndjson`)
}

test('WHAT[DURABLE-CONVERGENCE-011] reverse tail read is exact across block boundaries', () => {
  const root = mkdtempSync(join(tmpdir(), 'wxs-writer-tail-'))
  const path = join(root, 'writer.ndjson')
  try {
    const first = JSON.stringify({ marker: 'first' })
    const tail = JSON.stringify({ marker: 'last', text: 'x'.repeat(9000) })
    writeFileSync(path, `${first}\n${tail}\n`)
    assert.equal(retention.readLastCompleteLine(path), tail)
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
})

test('WHAT[DURABLE-CONVERGENCE-011] durable Journal ObservedAt outranks refreshed writer mtime', () => {
  const root = mkdtempSync(join(tmpdir(), 'wxs-writer-observed-at-'))
  const commonDir = join(root, '.git')
  const now = Date.parse('2026-08-21T00:00:00Z')

  try {
    mkdirSync(commonDir, { recursive: true })

    const oldJournal = {
      id: A,
      stream: 'retention/journal-old',
      type: 'JournalEnvelope',
      parents: [],
      payload: { ObservedAt: '2026-08-19T00:00:00.000+00:00' },
      payloadRefs: [],
    }
    const cutTail = {
      id: B,
      stream: 'integrator/cut-tail/test',
      type: 'ProjectionCutTail',
      parents: [A],
      payload: {},
      payloadRefs: [],
    }
    const oldPath = writeCanonicalEvents(commonDir, 'writer-old-journal', [oldJournal, cutTail])
    utimesSync(oldPath, now / 1000, now / 1000)

    const freshJournal = {
      id: 'c'.repeat(40),
      stream: 'retention/journal-fresh',
      type: 'JournalEnvelope',
      parents: [],
      payload: { ObservedAt: '2026-08-20T23:59:00.000+00:00' },
      payloadRefs: [],
    }
    const freshPath = writeCanonicalEvents(commonDir, 'writer-fresh-journal', [freshJournal])
    utimesSync(freshPath, (now - 2 * DAY) / 1000, (now - 2 * DAY) / 1000)

    assert.deepEqual(
      retention.retainedWriterIdsAt(commonDir, now),
      ['writer-fresh-journal'],
      'Journal tail ObservedAt is durable activity truth; mtime is only a non-Journal fallback',
    )
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
})

test('WHAT[DURABLE-CONVERGENCE-011] 24h expiry removes local writer and remote materialization', async () => {
  await withRepo(async (repo, commonDir) => {
    // EventStore boot deliberately applies retention against its physical current
    // time. Keep this proof relative to the same captured instant instead of
    // freezing a calendar date that eventually makes the "fresh" writer expire.
    const now = Date.now()
    const oldPath = await writeEvent(commonDir, 'writer-old', make(A, 'retention/old'))
    const freshPath = await writeEvent(commonDir, 'writer-fresh', make(B, 'retention/fresh', [A]))
    utimesSync(oldPath, (now - 2 * DAY) / 1000, (now - 2 * DAY) / 1000)
    utimesSync(freshPath, (now - 60_000) / 1000, (now - 60_000) / 1000)

    assert.deepEqual(retention.retainedWriterIdsAt(commonDir, now), ['writer-fresh'],
      'activation/replay must not read an expired writer even before physical GC')

    const reopened = eventStore.create(commonDir, 'writer-retained-replay')
    try {
      assert.deepEqual(eventStore.heads(reopened, 'retention/fresh'), [B],
        'retained replay treats a parent outside the window as an already-satisfied causal boundary')
    } finally {
      eventStore.dispose(reopened)
    }

    const result = await retention.syncAt(repo, commonDir, null, now)
    assert.equal(result.ok, true, result.ok ? '' : JSON.stringify(result.error))
    assert.equal(existsSync(oldPath), false)
    assert.equal(existsSync(freshPath), true)

    const rootTree = execFileSync('git', ['-C', repo, 'ls-tree', result.root], { encoding: 'utf8' })
    assert.match(rootTree, /writer-manifest/)
    const writers = execFileSync('git', ['-C', repo, 'ls-tree', `${result.root}:writers`], { encoding: 'utf8' })
    assert.doesNotMatch(writers, /writer-old\.ndjson/)
    assert.match(writers, /writer-fresh\.ndjson/)
  })
})

test('WHAT[DURABLE-CONVERGENCE-011] stale remote snapshot cannot revive writer after cache crosses expiry', async () => {
  await withRepo(async (repo, commonDir) => {
    const born = Date.parse('2026-08-20T00:00:00Z')
    const writerPath = await writeEvent(commonDir, 'writer-aging', make(A, 'retention/aging'))
    utimesSync(writerPath, born / 1000, born / 1000)

    const before = await retention.syncAt(repo, commonDir, null, born + 60_000)
    assert.equal(before.ok, true, before.ok ? '' : JSON.stringify(before.error))
    assert.equal(existsSync(writerPath), true)

    const after = await retention.syncAt(repo, commonDir, before.root, born + DAY + 60_000)
    assert.equal(after.ok, true, after.ok ? '' : JSON.stringify(after.error))
    assert.equal(existsSync(writerPath), false, 'time-aware cache must not preserve an expired local writer')
    const writers = execFileSync('git', ['-C', repo, 'ls-tree', `${after.root}:writers`], { encoding: 'utf8' })
    assert.doesNotMatch(writers, /writer-aging\.ndjson/, 'stale remote root must be filtered by its manifest')
  })
})

test('WHAT[DURABLE-CONVERGENCE-011] manifest-less legacy remote is ignored instead of refreshing writer activity', async () => {
  await withRepo(async (repo, commonDir) => {
    const born = Date.parse('2026-08-20T00:00:00Z')
    const writerPath = await writeEvent(commonDir, 'writer-legacy-remote', make(A, 'retention/legacy'))
    utimesSync(writerPath, born / 1000, born / 1000)

    const current = await retention.syncAt(repo, commonDir, null, born + 60_000)
    assert.equal(current.ok, true, current.ok ? '' : JSON.stringify(current.error))

    const rootEntries = execFileSync('git', ['-C', repo, 'ls-tree', current.root], { encoding: 'utf8' })
      .split('\n')
      .filter((line) => line && !line.endsWith('\twriter-manifest'))
      .join('\n') + '\n'
    const legacyRoot = execFileSync('git', ['-C', repo, 'mktree'], {
      encoding: 'utf8',
      input: rootEntries,
    }).trim()

    const after = await retention.syncAt(repo, commonDir, legacyRoot, born + DAY + 60_000)
    assert.equal(after.ok, true, after.ok ? '' : JSON.stringify(after.error))
    assert.equal(existsSync(writerPath), false)
    const writers = execFileSync('git', ['-C', repo, 'ls-tree', `${after.root}:writers`], { encoding: 'utf8' })
    assert.doesNotMatch(writers, /writer-legacy-remote\.ndjson/)
  })
})

test('WHAT[DURABLE-CONVERGENCE-011] v1 mtime manifest is legacy and cannot resurrect an expired writer', async () => {
  await withRepo(async (repo, commonDir) => {
    const born = Date.parse('2026-08-20T00:00:00Z')
    const writerPath = await writeEvent(commonDir, 'writer-v1-remote', make(A, 'retention/v1'))
    utimesSync(writerPath, born / 1000, born / 1000)

    const current = await retention.syncAt(repo, commonDir, null, born + 60_000)
    assert.equal(current.ok, true, current.ok ? '' : JSON.stringify(current.error))

    const manifestText = execFileSync('git', ['-C', repo, 'show', `${current.root}:writer-manifest`], { encoding: 'utf8' })
    const legacyText = manifestText
      .replace(/^v\d+$/m, 'v1')
      .replace(/\t[-+]?\d+(?:\.\d+)?$/m, `\t${born + 10 * DAY}`)
    const legacyManifestOid = execFileSync('git', ['-C', repo, 'hash-object', '-w', '--stdin'], {
      encoding: 'utf8',
      input: legacyText,
    }).trim()
    const legacyRootEntries = execFileSync('git', ['-C', repo, 'ls-tree', current.root], { encoding: 'utf8' })
      .split('\n')
      .filter(Boolean)
      .map((line) => line.endsWith('\twriter-manifest')
        ? `100644 blob ${legacyManifestOid}\twriter-manifest`
        : line)
      .join('\n') + '\n'
    const legacyRoot = execFileSync('git', ['-C', repo, 'mktree'], {
      encoding: 'utf8',
      input: legacyRootEntries,
    }).trim()

    const after = await retention.syncAt(repo, commonDir, legacyRoot, born + DAY + 60_000)
    assert.equal(after.ok, true, after.ok ? '' : JSON.stringify(after.error))
    assert.equal(existsSync(writerPath), false)
    const writers = execFileSync('git', ['-C', repo, 'ls-tree', `${after.root}:writers`], { encoding: 'utf8' })
    assert.doesNotMatch(writers, /writer-v1-remote\.ndjson/)
  })
})

test('WHAT[DURABLE-CONVERGENCE-011] declared manifest must cover every remote writer blob exactly', async () => {
  await withRepo(async (repo, commonDir) => {
    const born = Date.parse('2026-08-20T00:00:00Z')
    const writerPath = await writeEvent(commonDir, 'writer-invalid-manifest', make(A, 'retention/invalid-manifest'))
    utimesSync(writerPath, born / 1000, born / 1000)

    const current = await retention.syncAt(repo, commonDir, null, born + 60_000)
    assert.equal(current.ok, true, current.ok ? '' : JSON.stringify(current.error))

    const emptyManifestOid = execFileSync('git', ['-C', repo, 'hash-object', '-w', '--stdin'], {
      encoding: 'utf8',
      input: 'v2\n',
    }).trim()
    const malformedRootEntries = execFileSync('git', ['-C', repo, 'ls-tree', current.root], { encoding: 'utf8' })
      .split('\n')
      .filter(Boolean)
      .map((line) => line.endsWith('\twriter-manifest')
        ? `100644 blob ${emptyManifestOid}\twriter-manifest`
        : line)
      .join('\n') + '\n'
    const malformedRoot = execFileSync('git', ['-C', repo, 'mktree'], {
      encoding: 'utf8',
      input: malformedRootEntries,
    }).trim()

    const result = await retention.syncAt(repo, commonDir, malformedRoot, born + 120_000)
    assert.equal(result.ok, false)
    assert.match(String(result.error), /writer manifest does not bind writer blob/i)
  })
})

test('WHAT[DURABLE-CONVERGENCE-011] writer lifecycle observes active, expiry, and reactivation without mtime resurrection', async () => {
  await withRepo(async (repo, commonDir) => {
    const HOUR = 60 * 60 * 1000
    const born = Date.parse('2026-08-20T00:00:00Z')
    const eventsDir = join(commonDir, 'wanxiang', 'events')
    const writerPath = join(eventsDir, 'writer-lifecycle.ndjson')

    const writeJournalLine = (event) => {
      mkdirSync(eventsDir, { recursive: true })
      const line = JSON.stringify({
        event_id: event.id,
        event_type: 'JournalEnvelope',
        parents: event.parents,
        payload: { ObservedAt: new Date(event.observedAt).toISOString() },
        payload_refs: [],
        stream_id: event.stream,
      }) + '\n'
      writeFileSync(writerPath, line, { flag: 'a' })
    }

    // 1. Initial event at born
    writeJournalLine({ id: A, stream: 'retention/lifecycle', parents: [], observedAt: born })

    // Active at born + 12h: retained and materialized in remote snapshot
    assert.deepEqual(retention.retainedWriterIdsAt(commonDir, born + 12 * HOUR), ['writer-lifecycle'])
    const snap1 = await retention.syncAt(repo, commonDir, null, born + 12 * HOUR)
    assert.equal(snap1.ok, true, snap1.ok ? '' : JSON.stringify(snap1.error))
    const writers1 = execFileSync('git', ['-C', repo, 'ls-tree', `${snap1.root}:writers`], { encoding: 'utf8' })
    assert.match(writers1, /writer-lifecycle\.ndjson/)

    // Expired at born + 25h: even if file mtime is artificially touched to "now", durable ObservedAt governs
    assert.deepEqual(retention.retainedWriterIdsAt(commonDir, born + 25 * HOUR), [])
    utimesSync(writerPath, (born + 25 * HOUR) / 1000, (born + 25 * HOUR) / 1000)
    assert.deepEqual(retention.retainedWriterIdsAt(commonDir, born + 25 * HOUR), [], 'refreshed mtime cannot revive expired journal writer')

    // Sync at born + 25h purges expired writer from local disk and new snapshot
    const snap2 = await retention.syncAt(repo, commonDir, snap1.root, born + 25 * HOUR)
    assert.equal(snap2.ok, true, snap2.ok ? '' : JSON.stringify(snap2.error))
    assert.equal(existsSync(writerPath), false, 'expired writer removed from disk')
    const writers2 = execFileSync('git', ['-C', repo, 'ls-tree', `${snap2.root}:writers`], { encoding: 'utf8' })
    assert.equal(writers2.trim(), '', 'expired writer removed from remote tree')

    // 2. Reactivation by writing a new event at born + 26h
    writeJournalLine({ id: B, stream: 'retention/lifecycle', parents: [A], observedAt: born + 26 * HOUR })
    assert.deepEqual(retention.retainedWriterIdsAt(commonDir, born + 27 * HOUR), ['writer-lifecycle'], 'reactivated writer is retained')
    const snap3 = await retention.syncAt(repo, commonDir, snap2.root, born + 27 * HOUR)
    assert.equal(snap3.ok, true, snap3.ok ? '' : JSON.stringify(snap3.error))
    const writers3 = execFileSync('git', ['-C', repo, 'ls-tree', `${snap3.root}:writers`], { encoding: 'utf8' })
    assert.match(writers3, /writer-lifecycle\.ndjson/, 'reactivated writer rematerialized in snapshot')
  })
})
