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

test('WHAT[DURABLE-CONVERGENCE-011] 24h expiry removes local writer and remote materialization', async () => {
  await withRepo(async (repo, commonDir) => {
    const now = Date.parse('2026-08-21T00:00:00Z')
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

test('WHAT[DURABLE-CONVERGENCE-011] declared manifest must cover every remote writer blob exactly', async () => {
  await withRepo(async (repo, commonDir) => {
    const born = Date.parse('2026-08-20T00:00:00Z')
    const writerPath = await writeEvent(commonDir, 'writer-invalid-manifest', make(A, 'retention/invalid-manifest'))
    utimesSync(writerPath, born / 1000, born / 1000)

    const current = await retention.syncAt(repo, commonDir, null, born + 60_000)
    assert.equal(current.ok, true, current.ok ? '' : JSON.stringify(current.error))

    const emptyManifestOid = execFileSync('git', ['-C', repo, 'hash-object', '-w', '--stdin'], {
      encoding: 'utf8',
      input: 'v1\n',
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

test('WHAT[DURABLE-CONVERGENCE-011] source binds activity to blob oid and never derives it from fetch mtime', () => {
  const source = readFileSync(new URL('../../../src/Wanxiangshu/Persistence/EventStore/WriterStreamSync.fs', import.meta.url), 'utf8')
  const log = readFileSync(new URL('../../../src/Wanxiangshu/Persistence/EventStore/ProcessEventLog.fs', import.meta.url), 'utf8')
  assert.match(source, /writer-manifest/)
  assert.match(source, /BlobOid[\s\S]*LastActivityMs/)
  assert.match(source, /nextExpiry|NextExpiry/)
  assert.match(log, /readSync/)
  assert.match(log, /lastIndexOfLf|readLastCompleteLine/)
  assert.doesNotMatch(source, /fetch[^\n]*Date\.now|Date\.now[^\n]*fetch/i)
})
