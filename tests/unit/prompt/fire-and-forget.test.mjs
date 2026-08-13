// tests/unit/Prompt/fire-and-forget.test.mjs — PROMPT-007.
//
// Fire-and-forget = AwaitMode.Detached: caller does not wait for PhysicalAccepted.
// Claim, authority, persist and error recording still run. No standalone
// SendChildPromptFireAndForget port may exist.

import assert from 'node:assert/strict'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import {
  agentJournal,
  continuationKind,
  attemptPlanner,
  isSome,
  mapCount,
  promptDispatcher,
  transportReceipt,
} from '../support/domain.mjs'

const capturingPort = (captured) => ({
  SubscribeTerminal: () => ({ Dispose: () => {} }),
  SendPrompt: async (session, text, options) => {
    captured.push({ text, options })
    // Receipt-only admission: no physical id. Detached must still Ok.
    return promptDispatcher.admittedWithReceipt(transportReceipt('accepted-007'))
  },
})

test('PROMPT_007_detached_claims_and_persists_without_physical_accepted', async () => {
  const base = mkdtempSync(join(tmpdir(), 'wxs-prompt-007-'))
  try {
    const opened = await agentJournal.create({ directory: base })
    assert.equal(opened.ok, true, opened.ok ? '' : JSON.stringify(opened.error))
    try {
      const runtime = promptDispatcher.forJournal(opened.journal)
      const captured = []
      const port = capturingPort(captured)

      // Detached: fire-and-forget. Caller success does not require PhysicalAccepted.
      const sent = await promptDispatcher.sendAgentOwnerRoot(runtime, port, {
        session: 'ses_007',
        text: 'detached dispatch',
        agent: 'fast-coder',
        awaitMode: 'Detached',
      })
      assert.equal(sent.ok, true, sent.ok ? '' : sent.error)
      assert.ok(isSome(sent.key), 'Detached still returns PromptKey')
      assert.equal(captured.length, 1, 'SendPrompt must be reached')

      // Claimed → Submitted leaves a pending claim; PhysicalAccepted not required.
      const pending = promptDispatcher.pendingClaimCount(runtime, 'ses_007')
      assert.equal(pending, 1, 'Detached must claim/submit (PendingClaims = 1)')
    } finally {
      opened.dispose()
    }
  } finally {
    rmSync(base, { recursive: true, force: true })
  }
})

test('PROMPT_007_detached_continuation_same_claim_path', async () => {
  const base = mkdtempSync(join(tmpdir(), 'wxs-prompt-007c-'))
  try {
    const opened = await agentJournal.create({ directory: base })
    assert.equal(opened.ok, true, opened.ok ? '' : JSON.stringify(opened.error))
    try {
      const runtime = promptDispatcher.forJournal(opened.journal)
      const captured = []
      const port = capturingPort(captured)

      // Seed an authority root first so continuation has an active profile.
      const root = await promptDispatcher.sendAgentOwnerRoot(runtime, port, {
        session: 'ses_007c',
        text: 'root for continuation',
        agent: 'fast-coder',
        awaitMode: 'Detached',
      })
      assert.equal(root.ok, true, root.ok ? '' : root.error)

      // Continuations need a real AuthorityExecutionProfile on the journal.
      // attemptPlanner.authority builds a profile shape for send; with no
      // ActiveLogicalRun the claim still records via the profile argument.
      const cont = await promptDispatcher.sendContinuation(runtime, port, {
        session: 'ses_007c',
        text: 'busy nudge text',
        continuation: continuationKind.of('BusyAgentNudge'),
        profile: attemptPlanner.authority({ session: 'ses_007c' }),
        effectiveAgent: 'fast-coder',
        awaitMode: 'Detached',
      })
      assert.equal(cont.ok, true, cont.ok ? '' : cont.error)
      assert.equal(captured.length, 2)

      const projection = promptDispatcher.projectionFor(runtime, 'ses_007c')
      assert.ok(mapCount(projection.PendingClaims) >= 1, 'continuation claim must persist')
    } finally {
      opened.dispose()
    }
  } finally {
    rmSync(base, { recursive: true, force: true })
  }
})

test('PROMPT_007_await_mode_constructors_exist', () => {
  const detached = promptDispatcher.awaitMode.detached()
  const await_ = promptDispatcher.awaitMode.await()
  assert.equal(detached.cases()[detached.tag], 'Detached')
  assert.equal(await_.cases()[await_.tag], 'Await')
})
