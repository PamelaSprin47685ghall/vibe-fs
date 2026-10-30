import assert from 'node:assert/strict'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import * as journal from '../../../dist/Persistence/Journal/Surface.js'
import * as owner from '../../../dist/Participant/Provider/Attempt/Fallback/ProviderFailureSurface.js'

test('WHAT[provider-attempt-recovery-026] missing active profile is terminal, never superseded', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-recovery-026-none-'))
  const created = await journal.JournalSurface_bootWithWriterId(directory, 'writer-026-none', 'runtime-026-none', 1, '2026-01-01T00:00:00Z')
  try {
    const verdict = await owner.continueSameProviderRecoveryVerdict(created.journal, 'ses-026-none', 'msg-root', 'run-026')
    assert.equal(verdict, 'Terminal')
  } finally {
    journal.JournalSurface_dispose(created.journal)
    rmSync(directory, { recursive: true, force: true })
  }
})

test('WHAT[provider-attempt-recovery-026] a different authority root stays a silent supersession', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-recovery-026-takeover-'))
  const created = await journal.JournalSurface_bootWithWriterId(directory, 'writer-026-takeover', 'runtime-026-takeover', 1, '2026-01-01T00:00:00Z')
  try {
    const accepted = await owner.acceptHumanRoot(created.journal, 'ses-026-takeover', 'msg-root', 'engineer')
    assert.equal(accepted.ok, true)
    const verdict = await owner.continueSameProviderRecoveryVerdict(created.journal, 'ses-026-takeover', 'msg-other', 'run-026')
    assert.equal(verdict, 'Superseded')
  } finally {
    journal.JournalSurface_dispose(created.journal)
    rmSync(directory, { recursive: true, force: true })
  }
})
