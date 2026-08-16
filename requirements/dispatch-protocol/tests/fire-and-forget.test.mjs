// requirements/dispatch-protocol/tests/fire-and-forget.test.mjs — PROMPT-007.
//
// Fire-and-forget = AwaitMode.Detached: caller does not wait for PhysicalAccepted.
// Claim, authority, persist and error recording still run. No standalone
// SendChildPromptFireAndForget port may exist.

import assert from 'node:assert/strict'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import * as authority from '../../../dist/Interaction/Authority/RuntimeSurface.js'
import * as dispatch from '../../../dist/Interaction/Dispatch/DispatchSurface.js'
import * as journal from '../../../dist/Persistence/Journal/Surface.js'

const hash = (value) => `H(${value})`

const capturingPort = (captured, outcome = () => dispatch.admittedWithReceipt('accepted-007')) => ({
  SubscribeTerminal: () => ({ Dispose: () => {} }),
  SendPrompt: async (session, text, options) => {
    captured.push({ session, text, options })
    return outcome()
  },
})

const profileFor = (session, runtime = 'rt-007c') => {
  const built = authority.createAuthorityRoot(hash, runtime, session, 'AgentOwnerRoot', 'msg-root-007c', 'fast-coder')
  assert.equal(built.ok, true, built.ok ? '' : JSON.stringify(built.error))
  return built.value
}

test('WHAT[DISPATCH-PROTOCOL-009] PROMPT_007_detached_claims_and_persists_without_physical_accepted', async () => {
  const base = mkdtempSync(join(tmpdir(), 'wxs-prompt-007-'))
  try {
    const opened = await journal.JournalSurface_bootWithWriterId(base, 'writer-007', 'rt-007', 4242, '2026-01-01T00:00:00Z')
    assert.equal(opened.ok, true, opened.ok ? '' : JSON.stringify(opened.error))
    try {
      const captured = []
      const sent = await dispatch.sendAgentOwnerRoot(
        capturingPort(captured),
        opened.journal,
        'ses_007',
        'detached dispatch',
        'fast-coder',
      )
      assert.equal(sent.ok, true, sent.ok ? '' : sent.error)
      assert.equal(typeof sent.key, 'string', 'Detached still returns PromptKey')
      assert.equal(captured.length, 1, 'SendPrompt must be reached')
      assert.equal(dispatch.pendingClaimCount(opened.journal, 'ses_007'), 1, 'Detached must claim/submit (PendingClaims = 1)')
    } finally {
      journal.JournalSurface_dispose(opened.journal)
    }
  } finally {
    rmSync(base, { recursive: true, force: true })
  }
})

test('WHAT[DISPATCH-PROTOCOL-009] PROMPT_007_detached_sdk_physical_id_does_not_race_chat_message_acceptance', async () => {
  const base = mkdtempSync(join(tmpdir(), 'wxs-prompt-007-physical-'))
  try {
    const opened = await journal.JournalSurface_bootWithWriterId(base, 'writer-007-physical', 'rt-007-physical', 4242, '2026-01-01T00:00:00Z')
    assert.equal(opened.ok, true, opened.ok ? '' : JSON.stringify(opened.error))
    try {
      const port = capturingPort([], () => dispatch.admittedWithPhysicalMessage('msg-sdk-early-007'))
      const sent = await dispatch.sendAgentOwnerRoot(
        port,
        opened.journal,
        'ses_007_physical',
        'detached sdk physical return',
        'fast-coder',
      )
      assert.equal(sent.ok, true, sent.ok ? '' : sent.error)
      await Promise.resolve()
      assert.equal(
        dispatch.pendingClaimCount(opened.journal, 'ses_007_physical'),
        1,
        'Detached leaves PhysicalAccepted to the real chat.message ingress even if SDK returns an id early',
      )
    } finally {
      journal.JournalSurface_dispose(opened.journal)
    }
  } finally {
    rmSync(base, { recursive: true, force: true })
  }
})

test('WHAT[DISPATCH-PROTOCOL-009] PROMPT_007_detached_returns_even_when_session_send_task_never_settles', async () => {
  const base = mkdtempSync(join(tmpdir(), 'wxs-prompt-007-never-'))
  let release
  try {
    const opened = await journal.JournalSurface_bootWithWriterId(base, 'writer-007-never', 'rt-007-never', 4242, '2026-01-01T00:00:00Z')
    assert.equal(opened.ok, true, opened.ok ? '' : JSON.stringify(opened.error))
    try {
      let invoked = 0
      const never = new Promise((resolve) => { release = resolve })
      const port = {
        SubscribeTerminal: () => ({ Dispose: () => {} }),
        SendPrompt: () => {
          invoked += 1
          return never
        },
      }

      const pending = dispatch.sendAgentOwnerRoot(
        port,
        opened.journal,
        'ses_007_never',
        'detached must hand control back after invocation',
        'deep-devops',
      )
      const result = await Promise.race([
        pending,
        new Promise((resolve) => setTimeout(() => resolve({ timedOut: true }), 120)),
      ])

      assert.equal(result?.timedOut, undefined, 'Detached must not await ISessionHostPort.SendPrompt settlement')
      assert.equal(result.ok, true, result.ok ? '' : result.error)
      assert.equal(invoked, 1, 'Detached still invokes Host enqueue exactly once')
      assert.equal(dispatch.pendingClaimCount(opened.journal, 'ses_007_never'), 1)
    } finally {
      release?.(dispatch.admittedWithReceipt('accepted-late-007'))
      journal.JournalSurface_dispose(opened.journal)
    }
  } finally {
    rmSync(base, { recursive: true, force: true })
  }
})

test('WHAT[DISPATCH-PROTOCOL-009] PROMPT_007_detached_continuation_same_claim_path', async () => {
  const base = mkdtempSync(join(tmpdir(), 'wxs-prompt-007c-'))
  try {
    const opened = await journal.JournalSurface_bootWithWriterId(base, 'writer-007c', 'rt-007c', 4242, '2026-01-01T00:00:00Z')
    assert.equal(opened.ok, true, opened.ok ? '' : JSON.stringify(opened.error))
    try {
      const captured = []
      const port = capturingPort(captured)
      const root = await dispatch.sendAgentOwnerRoot(
        port,
        opened.journal,
        'ses_007c',
        'root for continuation',
        'fast-coder',
      )
      assert.equal(root.ok, true, root.ok ? '' : root.error)

      const cont = await dispatch.sendContinuation(
        port,
        opened.journal,
        'ses_007c',
        'busy nudge text',
        'BusyAgentNudge',
        profileFor('ses_007c'),
        'fast-coder',
        'Detached',
      )
      assert.equal(cont.ok, true, cont.ok ? '' : cont.error)
      assert.equal(captured.length, 2)
      assert.ok(dispatch.projectionObservation(opened.journal, 'ses_007c').pendingClaims.length >= 1, 'continuation claim must persist')
    } finally {
      journal.JournalSurface_dispose(opened.journal)
    }
  } finally {
    rmSync(base, { recursive: true, force: true })
  }
})

test('WHAT[DISPATCH-PROTOCOL-009] PROMPT_007_await_mode_constructors_exist', () => {
  assert.deepEqual(dispatch.awaitModeObservation(), { await: 'Await', detached: 'Detached' })
})
