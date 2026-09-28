import assert from 'node:assert/strict'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import * as journal from '../../../../dist/Persistence/Journal/Surface.js'

export async function withReopenableJournal(run) {
  const directory = mkdtempSync(join(tmpdir(), 'semantic-trace-reopen-'))
  let current
  const reopen = async () => {
    if (current) journal.JournalSurface_dispose(current)
    current = undefined
    const result = await journal.JournalSurface_boot(directory, 'trace-reopen', 4242, '9999-01-01T00:00:00Z')
    assert.equal(result.ok, true, result.ok ? '' : JSON.stringify(result.error))
    current = result.journal
    return current
  }
  try {
    return await run(await reopen(), reopen, directory)
  } finally {
    if (current) journal.JournalSurface_dispose(current)
    rmSync(directory, { recursive: true, force: true })
  }
}
