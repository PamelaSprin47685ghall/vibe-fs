import assert from 'node:assert/strict'
import { createHash } from 'node:crypto'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import * as journal from '../../../dist/Persistence/Journal/Surface.js'
import * as host from '../../../dist/Mission/Obligation/Todo/OpenCode/MagicTodoHostSurface.js'
import * as membrane from '../../../dist/Mission/Obligation/Todo/MagicTodoMembraneSurface.js'

process.env.WANXIANGSHU_NO_FATAL_EXIT = '1'

const sha256Hex = (value) => createHash('sha256').update(value).digest('hex')

const openJournal = async (runtime = 'rt_magic_todo_after') => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-magic-todo-after-'))
  const boot = await journal.JournalSurface_boot(directory, runtime, 4242, '2026-08-11T00:00:00Z')
  assert.equal(boot.ok, true, boot.ok ? '' : boot.error)
  return {
    handle: boot.journal,
    close: () => {
      journal.JournalSurface_dispose(boot.journal)
      rmSync(directory, { recursive: true, force: true })
    },
  }
}

const withJournal = async (body, runtime = 'rt_magic_todo_after') => {
  const opened = await openJournal(runtime)
  try {
    return await body(opened.handle)
  } finally {
    opened.close()
  }
}

test('WHAT[OBLIGATION-LEDGER-026] after hook accepts checkpoint durably and enriches T1 revelation', async () => {
  await withJournal(async (handle) => {
    const sessionId = 'ses-after-test'
    const incumbencyId = 'life-after-test'
    const callId = 'call-after-1'
    const obligations = [{ name: 'task-1', horizon: 'near', work: 'Do work' }]

    // Open life
    const openRes = await membrane.MagicTodoMembraneSurface_openLife(handle, sessionId, incumbencyId)
    assert.equal(openRes.ok, true)

    // Prepare checkpoint
    const args = { planComplete: true, workingOn: 'task-1', obligations }
    const canonical = host.canonicalInput(args)
    const digest = host.canonicalInputDigest(sha256Hex, args)
    const prep = await membrane.MagicTodoMembraneSurface_prepare(
      handle, sessionId, callId, canonical, digest, true, obligations, 0,
    )
    assert.equal(prep.ok, true, prep.ok ? '' : JSON.stringify(prep.error))

    // Accept checkpoint (after hook logic)
    const accepted = await membrane.MagicTodoMembraneSurface_accept(
      handle, prep.value.bridge, 'LiveAfterSuccess', digest, sha256Hex('physical-output-after-test'),
    )
    assert.equal(accepted.ok, true, accepted.ok ? '' : JSON.stringify(accepted.error))

    // After hook verified:
    // 1. Snapshot has latched firstPlanCommitment
    const snap = membrane.MagicTodoMembraneSurface_snapshot(handle, incumbencyId)
    assert.equal(snap.firstPlanCommitment, prep.value.prepared.todoWriteId)
    // 2. T1 revelation enriched result
    assert.match(accepted.value.enrichedResult, /Manager who will carry it is you|The road is yours/i)
  })
})
