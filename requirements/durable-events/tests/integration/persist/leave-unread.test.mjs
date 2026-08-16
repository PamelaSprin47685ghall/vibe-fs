// Shock-cut legacy layouts are completely leave-unread and legacy source is absent.

import assert from 'node:assert/strict'
import { createHash } from 'node:crypto'
import { execFileSync } from 'node:child_process'
import { existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, statSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import * as eventStore from '../../../../../dist/Persistence/EventStore/Surface.js'

const POISON = 'LEAVE_UNREAD_POISON_SENTINEL_NEVER_PARSE\n{not-a-journal-envelope\n'

const fingerprint = (p) => {
  const s = statSync(p)
  return {
    size: s.size,
    mtimeMs: s.mtimeMs,
    ino: s.ino,
    sha256: createHash('sha256').update(readFileSync(p)).digest('hex'),
  }
}

const plant = (commonDir) => {
  const oldRuntime = join(commonDir, 'wanxiangshu-next', 'runtimes')
  const oldUnified = join(commonDir, 'objects-for-old-universal-store')
  mkdirSync(oldRuntime, { recursive: true })
  mkdirSync(oldUnified, { recursive: true })
  const a = join(oldRuntime, 'abandoned.ndjson')
  const b = join(oldUnified, 'legacy.poison')
  writeFileSync(a, POISON)
  writeFileSync(b, POISON)
  return [
    [a, fingerprint(a)],
    [b, fingerprint(b)],
  ]
}

test('local_EventStore_never_reads_or_rewrites_any_legacy_layout', async () => {
  const repo = mkdtempSync(join(tmpdir(), 'wxs-leave-unread-'))
  try {
    execFileSync('git', ['init', '--quiet', repo])
    const commonDir = join(repo, '.git')
    const planted = plant(commonDir)
    const local = eventStore.createLocalStore(commonDir, 'fresh-writer')
    const event = {
      id: 'a'.repeat(40),
      stream: 'leave-unread/new',
      type: 'JobRequested',
      parents: [],
      payload: { ok: true },
      payloadRefs: [],
    }

    const result = await eventStore.append(local.store, [event])
    assert.equal(result.ok, true, `append failed: ${JSON.stringify(result.error)}`)

    for (const [path, before] of planted) {
      assert.deepEqual(fingerprint(path), before)
      assert.equal(readFileSync(path, 'utf8'), POISON)
    }
  } finally {
    rmSync(repo, { recursive: true, force: true })
  }
})

test('shock_cut_source_has_no_legacy_shape_detection_migration_or_reset', async () => {
  const { readFile } = await import('node:fs/promises')
  const eventStore = await readFile(
    new URL('../../../../../src/Wanxiangshu/Persistence/EventStore/Store.fs', import.meta.url),
    'utf8',
  )
  const sync = await readFile(
    new URL('../../../../../src/Wanxiangshu/Persistence/EventStore/WriterStreamSync.fs', import.meta.url),
    'utf8',
  )
  const legacySources = [
    new URL('../../../../../src/Wanxiangshu/Infrastructure/Persist/GitRawStore.fs', import.meta.url),
    new URL('../../../../../src/Wanxiangshu/Infrastructure/Persist/UniversalGitRawStore.fs', import.meta.url),
  ]

  assert.doesNotMatch(
    eventStore + sync,
    /LegacyEventsDir|isLegacy|migrat|reset.*root|wanxiangshu-next/i,
  )
  for (const legacy of legacySources) {
    assert.equal(existsSync(legacy), false, `${legacy.pathname}: shock-cut legacy source must stay deleted`)
  }
})
