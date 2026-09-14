// W5 regression — MagicTodoMembrane's host-hook funnel must refuse through
// typed outcomes (ProviderInputRejection / JournalAppendException) instead of
// calling Diagnostic.fatal. The hooks are the real production set; only the
// snapshot port is controlled. Every case exercised here used to collapse into
// the same `magic-todo-infrastructure-failed` fatal before the W5 closeout.
import assert from 'node:assert/strict'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import * as journal from '../../../dist/Persistence/Journal/Surface.js'
import * as membrane from '../../../dist/Mission/Obligation/Todo/MagicTodoMembraneSurface.js'

const openJournal = async (runtime = 'rt_magic_todo_hook_refusal') => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-magic-todo-hook-refusal-'))
  const boot = await journal.JournalSurface_boot(directory, runtime, 4242, '2026-09-14T00:00:00Z')
  assert.equal(boot.ok, true, boot.ok ? '' : boot.error)
  return {
    handle: boot.journal,
    journal: boot.journal,
    close: () => {
      journal.JournalSurface_dispose(boot.journal)
      rmSync(directory, { recursive: true, force: true })
    },
  }
}

const todowriteInput = (sessionID, callID, rest = {}) => ({
  tool: 'todowrite',
  sessionID,
  callID,
  ...rest,
})

const argsWithObligations = (obligations = [{ name: 'task-1', horizon: 'near', work: 'Do work' }]) => ({
  args: {
    planComplete: true,
    workingOn: obligations[0]?.name ?? '',
    obligations,
  },
})

// T03: capability gap — no durable journal. The membrane must refuse the todowrite
// call through the host contract, not trip the process.
test('WHAT[OBLIGATION-LEDGER-028] before refuses when the durable journal is absent', async () => {
  const result = await membrane.MagicTodoMembraneSurface_runHooksBefore(
    null, // no AgentJournal — the capability is legitimately absent
    null,
    todowriteInput('ses-x', 'call-x'),
    argsWithObligations(),
  )
  assert.equal(result.kind, 'provider_input_rejected')
  assert.match(result.reason, /durable AgentJournal|durable/i)
})

// T01/T02: Host contract violation — missing/blank fields must decline the
// call, not fatal. Both the missing field and blank field are refusal paths.
test('WHAT[OBLIGATION-LEDGER-028] before refuses when sessionID or callID is missing or blank', async () => {
  const opened = await openJournal()
  try {
    const { journal: durable } = opened

    const missingSession = await membrane.MagicTodoMembraneSurface_runHooksBefore(
      durable,
      null,
      todowriteInput('', 'call-x'),
      argsWithObligations(),
    )
    assert.equal(missingSession.kind, 'provider_input_rejected')

    const missingCall = await membrane.MagicTodoMembraneSurface_runHooksBefore(
      durable,
      null,
      todowriteInput('ses-x', ''),
      argsWithObligations(),
    )
    assert.equal(missingCall.kind, 'provider_input_rejected')
  } finally {
    opened.close()
  }
})

// T03 + T11 in one: before completes on an unavailable snapshot by refusing the
// checkpoint bridge — never through fatalInfrastructure.
test('WHAT[OBLIGATION-LEDGER-028] before refuses when the snapshot port is absent', async () => {
  const opened = await openJournal()
  try {
    const { journal: durable } = opened
    const result = await membrane.MagicTodoMembraneSurface_runHooksBefore(
      durable,
      null,
      todowriteInput('ses-x', 'call-x'),
      argsWithObligations(),
    )
    assert.equal(result.kind, 'provider_input_rejected')
    assert.match(result.reason, /snapshot/i)
  } finally {
    opened.close()
  }
})
