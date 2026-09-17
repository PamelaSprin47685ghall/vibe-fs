import assert from 'node:assert/strict'
import { createHash } from 'node:crypto'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import * as journal from '../../../dist/Persistence/Journal/Surface.js'
import * as todoJournal from '../../../dist/Persistence/Journal/ObligationJournalSurface.js'
import * as host from '../../../dist/Mission/Obligation/Todo/OpenCode/MagicTodoHostSurface.js'
import * as membrane from '../../../dist/Mission/Obligation/Todo/MagicTodoMembraneSurface.js'
import * as locality from '../../../dist/Mission/Obligation/Todo/MagicTodoLocalitySurface.js'
import * as todo from '../../../dist/Mission/Obligation/Todo/MagicTodoSemanticSurface.js'

process.env.WANXIANGSHU_NO_FATAL_EXIT = '1'

const sha256Hex = (value) => createHash('sha256').update(value).digest('hex')

const openJournal = async (runtime = 'rt_magic_todo_membrane') => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-obligation-membrane-'))
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

const withJournal = async (body, runtime = 'rt_magic_todo_membrane') => {
  const opened = await openJournal(runtime)
  try {
    return await body(opened.handle)
  } finally {
    opened.close()
  }
}

const openLife = async (handle, session, life) => {
  const result = await membrane.MagicTodoMembraneSurface_openLife(handle, session, life)
  assert.equal(result.ok, true, result.ok ? '' : result.error)
}

const prepare = (handle, session, call, obligations, planComplete = true, state = 0) => {
  const args = { planComplete, workingOn: obligations[0]?.name ?? '', obligations }
  const canonical = host.canonicalInput(args)
  const digest = host.canonicalInputDigest(sha256Hex, args)
  return membrane.MagicTodoMembraneSurface_prepare(handle, session, call, canonical, digest, planComplete, obligations, state)
    .then((result) => ({ result, digest, args, canonical }))
}

const accept = async (handle, prepared, inputDigest, outputDigest) =>
  membrane.MagicTodoMembraneSurface_accept(handle, prepared, 'LiveAfterSuccess', inputDigest, outputDigest)

const assertOk = (result, message = '') => {
  assert.equal(result.ok, true, message || (result.ok ? '' : JSON.stringify(result.error)))
  return result.value
}

const fact = (caseName, payload) => JSON.stringify({ case: caseName, ...payload })

const append = async (handle, session, caseName, payload) => {
  const result = await todoJournal.appendMagicTodo(handle, session, null, fact(caseName, payload))
  assert.equal(result.ok, true, result.ok ? '' : result.error)
  return result
}

const acceptPlanningFalseCheckpoint = async (handle, session, life, callText) => {
  const planning = await prepare(handle, session, callText, [
    { name: 'inspect-startup', work: 'Inspect startup paths so the implementation plan can be completed.' },
  ], false)
  const prepared = assertOk(planning.result)
  const accepted = await accept(handle, prepared.bridge, planning.digest, sha256Hex('planning-false-physical-output'))
  assert.equal(accepted.ok, true, accepted.ok ? '' : JSON.stringify(accepted.error))
  return { planning, accepted }
}

const acceptT1Checkpoint = async (handle, session, callText) => {
  const t1 = await prepare(handle, session, callText, [{ name: 'diagnose', work: 'Establish why the first todowrite succeeds.' }])
  const prepared = assertOk(t1.result)
  const accepted = await accept(handle, prepared.bridge, t1.digest, sha256Hex('t1-physical-output'))
  assert.equal(accepted.ok, true, accepted.ok ? '' : JSON.stringify(accepted.error))
  return { t1, accepted }
}

test('WHAT[OBLIGATION-LEDGER-014] successive checkpoints can be prepared and accepted seamlessly', async () => {
  await withJournal(async (handle) => {
    const session = 'ses-magic-todo-t1-t2-resolve'
    const life = 'life-magic-todo-t1-t2-resolve'
    await openLife(handle, session, life)
    const { t1 } = await acceptT1Checkpoint(handle, session, 'call-magic-todo-t1')
    const t2 = await prepare(handle, session, 'call-magic-todo-t2', [
      { name: 'diagnose', work: 'Establish why the first todowrite succeeds.' },
      { name: 'fix', work: 'Keep later todowrite calls from failing red.' },
    ])
    const t2Prepared = assertOk(t2.result)
    const t2Accepted = await accept(handle, t2Prepared.bridge, t2.digest, sha256Hex('t2-physical-output'))
    assert.equal(t2Accepted.ok, true, t2Accepted.ok ? '' : JSON.stringify(t2Accepted.error))
  })
})
