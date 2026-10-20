import assert from 'node:assert/strict'
import { mkdirSync, mkdtempSync, rmSync, utimesSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

const retention = await import('../../../dist/Persistence/EventStore/RetentionSurface.js')

const day = 24 * 60 * 60 * 1000
const line = (id) =>
  JSON.stringify({
    event_id: id, event_type: 'JobRequested', parents: [],
    payload: {}, payload_refs: [], stream_id: 'proof/retention',
  }) + '\n'

// Writer files without a Journal tail fall back to producer-side file activity,
// so the fixture controls activity through the file time.
const withWriters = (activities, run) => {
  const commonDir = join(mkdtempSync(join(tmpdir(), 'wxs-retention-')), '.Herdchestra')
  try {
    const directory = join(commonDir, 'wanxiangshu', 'events')
    mkdirSync(directory, { recursive: true })
    Object.entries(activities).forEach(([writerId, activityMs], index) => {
      const path = join(directory, `${writerId}.ndjson`)
      writeFileSync(path, line(String(index + 1).repeat(40)))
      utimesSync(path, activityMs / 1000, activityMs / 1000)
    })
    return run(commonDir)
  } finally {
    rmSync(join(commonDir, '..'), { recursive: true, force: true })
  }
}

test('WHAT[durable-convergence-011] the real read chain keeps today and yesterday writers and drops older ones whole', () => {
  const now = Date.UTC(2026, 9, 20, 15, 30)
  const today = Date.UTC(2026, 9, 20)
  withWriters(
    { 'writer-today': now - 1000, 'writer-yesterday': today - day, 'writer-old': today - day - 1000 },
    (commonDir) => {
      assert.deepEqual(retention.retainedWriterIdsAt(commonDir, now), ['writer-today', 'writer-yesterday'])
      // Same writers, one UTC day later: yesterday's writer becomes the day before and leaves whole.
      assert.deepEqual(retention.retainedWriterIdsAt(commonDir, now + day), ['writer-today'])
    },
  )
})

// CUT-19 counterexample: the previous host rule compared UTC days directly, so a
// non-finite cut-off made every comparison false and the read chain returned an
// empty retained set: the whole local history silently vanished from Current.
test('WHAT[durable-convergence-011] a non-finite cut-off is rejected instead of retaining nothing', () => {
  withWriters({ 'writer-today': Date.now() }, (commonDir) => {
    assert.throws(() => retention.retainedWriterIdsAt(commonDir, Number.NaN))
  })
})

// The writer activity is the durable Journal observation at the logical-stream
// tail, not the file time; a trailing ProjectionCutTail chain walks back, also
// across a segment seam; any other tail line means no durable activity, so the
// file time speaks. These hold on the real read chain (CUT-19 positive controls).
const codec = await import('../../../dist/Persistence/EventStore/CodecSurface.js')
const fact = (id, type, payload) =>
  codec.encode({ id, stream: 'proof/retention-tail', type, parents: [], payload, payloadRefs: [] })
const journalAt = (id, observedAtMs) => fact(id, 'JournalEnvelope', { ObservedAt: new Date(observedAtMs).toISOString() })
const cutTail = (id) => fact(id, 'ProjectionCutTail', { rule: 'r', failed_event_id: 'f'.repeat(40), reason: 'x', reset_json: '{}' })
const jobRequested = (id) => fact(id, 'JobRequested', {})

const withFiles = (files, run) => {
  const commonDir = join(mkdtempSync(join(tmpdir(), 'wxs-retention-tail-')), '.Herdchestra')
  try {
    const directory = join(commonDir, 'wanxiangshu', 'events')
    mkdirSync(directory, { recursive: true })
    for (const [name, text, mtimeMs] of files) {
      const path = join(directory, name)
      writeFileSync(path, text)
      utimesSync(path, mtimeMs / 1000, mtimeMs / 1000)
    }
    return run(commonDir)
  } finally {
    rmSync(join(commonDir, '..'), { recursive: true, force: true })
  }
}

test('WHAT[durable-convergence-011] the Journal ObservedAt at the tail decides activity, not the file time', () => {
  const now = Date.UTC(2026, 9, 20, 15, 30)
  withFiles(
    [
      ['journal-fresh.ndjson', journalAt('1'.repeat(40), now - 1000), now - 9 * day],
      ['journal-stale.ndjson', journalAt('2'.repeat(40), now - 9 * day), now - 1000],
    ],
    (commonDir) => assert.deepEqual(retention.retainedWriterIdsAt(commonDir, now), ['journal-fresh']),
  )
})

test('WHAT[durable-convergence-011] a trailing cut-tail chain walks back across a segment seam to the Journal observation', () => {
  const now = Date.UTC(2026, 9, 20, 15, 30)
  withFiles(
    [
      ['seam-old-0000.ndjson', journalAt('3'.repeat(40), now - 5 * day), now - 1000],
      ['seam-old-0001.ndjson', cutTail('4'.repeat(40)) + cutTail('5'.repeat(40)), now - 1000],
      ['seam-new-0000.ndjson', journalAt('6'.repeat(40), now - 2000), now - 9 * day],
      ['seam-new-0001.ndjson', cutTail('7'.repeat(40)), now - 9 * day],
    ],
    (commonDir) => assert.deepEqual(retention.retainedWriterIdsAt(commonDir, now), ['seam-new']),
  )
})

test('WHAT[durable-convergence-011] any other tail line means no durable activity, so the newest file time speaks', () => {
  const now = Date.UTC(2026, 9, 20, 15, 30)
  withFiles(
    [
      ['other-fresh.ndjson', journalAt('8'.repeat(40), now - 9 * day) + jobRequested('9'.repeat(40)), now - 1000],
      ['other-stale.ndjson', journalAt('a'.repeat(40), now - 1000) + jobRequested('b'.repeat(40)), now - 9 * day],
    ],
    (commonDir) => assert.deepEqual(retention.retainedWriterIdsAt(commonDir, now), ['other-fresh']),
  )
})
