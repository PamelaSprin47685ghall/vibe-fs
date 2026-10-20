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
