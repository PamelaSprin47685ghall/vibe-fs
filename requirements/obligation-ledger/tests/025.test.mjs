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

test('WHAT[obligation-ledger-025] accept rejects unknown physical success evidence', async () => {
  await withJournal(async (handle) => {
    const result = await membrane.MagicTodoMembraneSurface_accept(handle, null, 'UNKNOWN', 'input', 'output')
    assert.equal(result.ok, false)
    assert.equal(result.error.code, 'InvalidPhysicalEvidence')
  })
})

test('WHAT[obligation-ledger-025] openLife and compatibility injection do not wait for snapshot IO', async () => {
  await withJournal(async (handle) => {
    const session = 'ses-before-latency'
    const life = 'life-before-latency'
    // openLife is a journal append — completes without any snapshot port
    await openLife(handle, session, life)
    // V1 compatibility injection is synchronous — no snapshot dependency
    const obligations = [{ name: 'diagnose', work: 'Fix the todowrite snapshot race.' }]
    const rows = host.projectCompatibilityRows('diagnose', obligations)
    const output = { args: { planComplete: false, workingOn: 'diagnose', obligations } }
    host.replaceCompatibilityArgs(output, rows)
    assert.equal('obligations' in output.args, true)
    assert.equal(Object.prototype.propertyIsEnumerable.call(output.args, 'todos'), false)
    assert.equal(output.args.todos[0].content, 'diagnose: Fix the todowrite snapshot race.')
    assert.equal(output.args.todos[0].status, 'in_progress')
  })
})

test('WHAT[obligation-ledger-025] prepare rejects a pending ToolPart whose provider input is still empty', async () => {
  await withJournal(async (handle) => {
    const session = 'ses-magic-todo-pending-input'
    await openLife(handle, session, 'life-magic-todo-pending-input')
    const result = await membrane.MagicTodoMembraneSurface_prepare(
      handle,
      session,
      'call-magic-todo-pending-input',
      '{}',
      'provider-input-digest',
      false,
      [{ name: 'diagnose', work: 'Fix the todowrite snapshot race.' }],
      0,
    )
    assert.equal(result.ok, false)
    assert.equal(result.error.code, 'SnapshotInputMismatch')
  })
})

test('WHAT[obligation-ledger-025] before materializes the exact provider input including planComplete and workingOn', () => {
  const expected = host.canonicalInput({ planComplete: false, workingOn: 'diagnose', obligations: [{ name: 'diagnose', work: 'Fix the todowrite snapshot race.' }] })
  const result = locality.materializeInput('call-magic-todo-await-input', '{}', 0, expected)
  assert.equal(result.ok, true)
  assert.equal(result.value.inputCanonical, expected)
})

test('WHAT[obligation-ledger-025] materialization fails closed when the provider input differs', () => {
  const actual = host.canonicalInput({ planComplete: false, workingOn: 'other', obligations: [{ name: 'other', work: 'Different provider input.' }] })
  const expected = host.canonicalInput({ planComplete: false, workingOn: 'diagnose', obligations: [{ name: 'diagnose', work: 'Fix the todowrite snapshot race.' }] })
  const result = locality.materializeInput('call-magic-todo-await-conflict', actual, 1, expected)
  assert.equal(result.ok, false)
  assert.equal(result.error.code, 'InputMismatch')
})

test('WHAT[obligation-ledger-025] materialized snapshot input must still match tool.execute.before args', async () => {
  await withJournal(async (handle) => {
    const session = 'ses-magic-todo-conflicting-input'
    await openLife(handle, session, 'life-magic-todo-conflicting-input')
    const input = host.canonicalInput({ planComplete: false, workingOn: 'other', obligations: [{ name: 'other', work: 'Different provider input.' }] })
    const submitted = [{ name: 'diagnose', work: 'Fix the todowrite snapshot race.' }]
    const result = await membrane.MagicTodoMembraneSurface_prepare(handle, session, 'call-magic-todo-conflicting-input', input, 'provider-input-digest', false, submitted, 0)
    assert.equal(result.ok, false)
    assert.equal(result.error.code, 'SnapshotInputMismatch')
  })
})
