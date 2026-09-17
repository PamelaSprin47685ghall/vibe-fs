import test from 'node:test'

{
const { default: test } = await import("node:test");
const { assertFatalBoundary } = await import("../../structured-workflow/tests/support/m6-boundary-proof.mjs");


test('WHAT[OBLIGATION-LEDGER-028] ledger fatal follows exact checkpoint settlement and one injected fuse', () => assertFatalBoundary('obligation-ledger'))
}

{
const { default: assert } = await import("node:assert/strict");
const { mkdtempSync, rmSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const { default: test } = await import("node:test");
const journal = await import("../../../dist/Persistence/Journal/Surface.js");
const membrane = await import("../../../dist/Mission/Obligation/Todo/MagicTodoMembraneSurface.js");

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
}
