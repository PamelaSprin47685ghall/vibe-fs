// Moved from tests/unit/prompt/send-format.test.mjs (cutover Wave 2a); owner: dispatch-protocol.
//
// PROMPT-006: every dispatch carries the effective Agent, no Model, untouched
// Directory, and PromptKey metadata. DispatchSurface returns the normalized
// JSON observation from the production Host boundary.

import assert from 'node:assert/strict'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import * as authority from '../../../dist/Interaction/Authority/RuntimeSurface.js'
import * as dispatch from '../../../dist/Interaction/Dispatch/DispatchSurface.js'
import * as journal from '../../../dist/Persistence/Journal/Surface.js'

const hash = (value) => `H(${value})`

/** The smallest ISessionHostPort: capture the send, admit it. */
const capturingPort = () => ({
  SubscribeTerminal: () => ({ Dispose: () => {} }),
  SendPrompt: async () => dispatch.admittedWithReceipt('accepted-006'),
})

const profileFor = () => {
  const built = authority.createAuthorityRoot(hash, 'rt-send', 'ses_006', 'HumanRoot', 'msg_u1', 'fast-coder')
  assert.equal(built.ok, true, built.ok ? '' : built.error)
  return built.value
}

const observation = (result) => {
  assert.equal(result.ok, true, result.ok ? '' : result.error)
  assert.ok(result.observation)
  return result.observation
}

test('WHAT[DISPATCH-PROTOCOL-010] PROMPT_006_unknown_authority_kind_fails_closed', async () => {
  const base = mkdtempSync(join(tmpdir(), 'wxs-send-format-invalid-'))
  try {
    const opened = await journal.JournalSurface_bootWithWriterId(base, 'writer-invalid', 'rt-invalid', 4242, '2026-01-01T00:00:00Z')
    assert.equal(opened.ok, true, opened.ok ? '' : JSON.stringify(opened.error))
    try {
      const result = await dispatch.sendContinuation(
        capturingPort(),
        opened.journal,
        'ses-invalid',
        'reject malformed profile',
        'ProviderRetryAttempt',
        { ...profileFor(), authorityKind: 'UnknownRoot' },
        'deep-coder',
        'Await',
      )
      assert.equal(result.ok, false)
      assert.match(result.error, /Unknown authority root kind/)
    } finally {
      journal.JournalSurface_dispose(opened.journal)
    }
  } finally {
    rmSync(base, { recursive: true, force: true })
  }
})

test('WHAT[DISPATCH-PROTOCOL-010] PROMPT_006_send_payload_carries_agent_and_no_model', async () => {
  const base = mkdtempSync(join(tmpdir(), 'wxs-send-format-'))
  try {
    const opened = await journal.JournalSurface_bootWithWriterId(base, 'writer-send', 'rt-send', 4242, '2026-01-01T00:00:00Z')
    assert.equal(opened.ok, true, opened.ok ? '' : JSON.stringify(opened.error))
    try {
      const ownerRoot = await dispatch.sendAgentOwnerRoot(
        capturingPort(),
        opened.journal,
        'ses_006',
        'dispatch this',
        'fast-coder',
      )
      const continuation = await dispatch.sendContinuation(
        capturingPort(),
        opened.journal,
        'ses_006',
        'retry on the other side',
        'ProviderRetryAttempt',
        profileFor(),
        'deep-coder',
        'Await',
      )
      const captured = [observation(ownerRoot), observation(continuation)]

      assert.deepEqual(
        captured.map((value) => ({ session: value.session, text: value.text })),
        [
          { session: 'ses_006', text: 'dispatch this' },
          { session: 'ses_006', text: 'retry on the other side' },
        ],
      )

      assert.deepEqual(
        { agent: captured[0].agent, model: captured[0].model },
        { agent: 'fast-coder', model: null },
        'SendAgentOwnerRoot must carry Agent = Some agent and Model = None',
      )
      assert.deepEqual(
        { agent: captured[1].agent, model: captured[1].model },
        { agent: 'deep-coder', model: null },
        'SendContinuation must carry Agent = Some effectiveAgent and Model = None',
      )

      assert.equal(captured[0].directory, null, 'no directory was given')
    } finally {
      journal.JournalSurface_dispose(opened.journal)
    }
  } finally {
    rmSync(base, { recursive: true, force: true })
  }
})

test('WHAT[DISPATCH-PROTOCOL-011] PROMPT_006_send_payload_carries_prompt_key_metadata', async () => {
  const base = mkdtempSync(join(tmpdir(), 'wxs-send-meta-'))
  try {
    const opened = await journal.JournalSurface_bootWithWriterId(base, 'writer-send-meta', 'rt-send-meta', 4242, '2026-01-01T00:00:00Z')
    assert.equal(opened.ok, true, opened.ok ? '' : JSON.stringify(opened.error))
    try {
      const ownerRoot = await dispatch.sendAgentOwnerRoot(
        capturingPort(),
        opened.journal,
        'ses_006m',
        'dispatch this',
        'fast-coder',
      )
      const continuation = await dispatch.sendContinuation(
        capturingPort(),
        opened.journal,
        'ses_006m',
        'retry on the other side',
        'ProviderRetryAttempt',
        authority.createAuthorityRoot(hash, 'rt-send-meta', 'ses_006m', 'HumanRoot', 'msg_u1', 'fast-coder').value,
        'deep-coder',
        'Await',
      )

      assert.ok(observation(ownerRoot).metadata, 'owner-root send must carry Metadata')
      assert.ok(observation(continuation).metadata, 'continuation send must carry Metadata')
    } finally {
      journal.JournalSurface_dispose(opened.journal)
    }
  } finally {
    rmSync(base, { recursive: true, force: true })
  }
})

test('WHAT[DISPATCH-PROTOCOL-002] HOST_004_stale_idle_repair_is_abandoned_at_the_final_physical_send_boundary', async () => {
  const base = mkdtempSync(join(tmpdir(), 'wxs-idle-send-race-'))
  try {
    const opened = await journal.JournalSurface_bootWithWriterId(base, 'writer-idle-race', 'rt-idle-race', 4242, '2026-01-01T00:00:00Z')
    assert.equal(opened.ok, true, opened.ok ? '' : JSON.stringify(opened.error))
    try {
      const session = 'ses_idle_race'
      const accepted = await dispatch.acceptHumanRoot(opened.journal, session, 'msg-root', 'fast-blogger')
      assert.equal(accepted.ok, true, accepted.ok ? '' : accepted.error)

      let sends = 0
      const port = {
        SubscribeTerminal: () => ({ Dispose: () => {} }),
        SendPrompt: async () => {
          sends += 1
          return dispatch.admittedWithReceipt('should-not-send')
        },
      }

      const outcome = await dispatch.sendIdleContinuation(
        port,
        opened.journal,
        session,
        'repair stale terminal',
        'InteractionRepair',
        accepted.profile,
        'fast-blogger',
        false,
      )

      assert.equal(outcome.outcome, 'Superseded', 'stale final admission must become Superseded, not a send failure')
      assert.equal(sends, 0, 'superseded idle repair must never invoke the physical Host SendPrompt')
      assert.equal(outcome.observation, null, 'no physical send means no Host observation is captured')

      const projection = dispatch.projectionObservation(opened.journal, session)
      assert.equal(projection.pendingClaims.length, 0, 'durable claim must be closed as Abandoned')
      assert.equal(projection.claimSequences.length, 1, 'abandon preserves the spent exact repair occasion')
    } finally {
      journal.JournalSurface_dispose(opened.journal)
    }
  } finally {
    rmSync(base, { recursive: true, force: true })
  }
})
