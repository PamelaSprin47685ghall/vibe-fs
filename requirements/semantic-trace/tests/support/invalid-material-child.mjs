import assert from 'node:assert/strict'
import * as journal from '../../../../dist/Persistence/Journal/Surface.js'

const [directory, scenario] = process.argv.slice(2)
const opened = await journal.JournalSurface_boot(directory, 'invalid-material-probe', process.pid, new Date().toISOString())
assert.equal(opened.ok, true)
const handle = opened.journal
try {
  const actual = await journal.JournalSurface_writePayload(handle, 'actual terminal')
  const other = await journal.JournalSurface_writePayload(handle, 'a different actual payload')
  assert.equal(actual.ok, true)
  assert.equal(other.ok, true)
  const refused = await journal.JournalSurface_appendAgent(handle, { kind: 'Session', session: 'refused-material' }, null, {
    family: 'Companion', case: 'TerminalOutputCaptured',
    payload: {
      SessionId: 'refused-material', ProviderRun: 'actual-terminal-run',
      TextRef: scenario === 'placeholder' ? 'blobs/placeholder' : scenario === 'missing payload' ? `blobs/${'0'.repeat(64)}` : actual.blobRef,
      TextDigest: scenario === 'placeholder' ? 'sha-placeholder' : other.blobDigest,
    },
  })
  assert.equal(refused.ok, false)
  console.log(JSON.stringify(refused))
} finally {
  journal.JournalSurface_dispose(handle)
}
