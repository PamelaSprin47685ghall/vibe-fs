import assert from 'node:assert/strict'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import * as authority from '../../../dist/Interaction/Authority/RuntimeSurface.js'
import * as dispatch from '../../../dist/Interaction/Dispatch/DispatchSurface.js'
import * as journal from '../../../dist/Persistence/Journal/Surface.js'

const hash = (value) => `H(${value})`

const capturingPort = () => ({
  SubscribeTerminal: () => ({ Dispose: () => {} }),
  SendPrompt: async () => dispatch.admittedWithReceipt('accepted-006'),
})

const personas = {
  engineer: 'Engineer',
  coder: 'Coder',
  manager: 'Lead',
}

const rootSelection = (participant) => {
  const role = participant === 'predictor' ? 'inspector' : participant
  return {
    kind: 'RootSelection',
    ownerSession: null,
    ownerLogicalRun: null,
    ownerAuthorityRoot: null,
    participantIdentity: {
      participant,
      role,
      selectedTier: 'deep',
      persona: personas[participant] ?? 'Unknown',
      personaCatalogVersion: 1,
      origin: 'ResolvedAtRoot',
    },
  }
}

const profileFor = (runtime = 'rt-send', session = 'ses_006', physical = 'msg_u1', participant = 'engineer') => {
  const built = authority.createAuthorityRoot(hash, runtime, session, 'HumanRoot', physical, rootSelection(participant))
  assert.equal(built.ok, true, built.ok ? '' : built.error)
  return built.value
}

const acceptOwner = async (handle, session = 'ses_owner') => {
  const accepted = await dispatch.acceptHumanRootSelection(
    handle,
    session,
    `msg-${session}`,
    rootSelection('manager'),
  )
  assert.equal(accepted.ok, true, accepted.ok ? '' : accepted.error)
  return accepted.profile
}

const observation = (result) => {
  assert.equal(result.ok, true, result.ok ? '' : result.error)
  assert.ok(result.observation)
  return result.observation
}

test('WHAT[dispatch-protocol-011] PROMPT_006_send_payload_carries_prompt_key_metadata', async () => {
  const base = mkdtempSync(join(tmpdir(), 'wxs-send-meta-'))
  try {
    const opened = await journal.JournalSurface_bootWithWriterId(base, 'writer-send-meta', 'rt-send-meta', 4242, '2026-01-01T00:00:00Z')
    assert.equal(opened.ok, true, opened.ok ? '' : JSON.stringify(opened.error))
    try {
      const owner = await acceptOwner(opened.journal, 'ses_006m_owner')
      const seed = authority.issueInheritedIdentitySeed('engineer', owner).value
      const ownerRoot = await dispatch.sendAgentOwnerRoot(
        capturingPort(),
        opened.journal,
        'ses_006m',
        'dispatch this',
        seed,
      )
      const continuation = await dispatch.sendContinuation(
        capturingPort(),
        opened.journal,
        'ses_006m',
        'retry the fixed participant',
        'ProviderRetryAttempt',
        profileFor('rt-send-meta', 'ses_006m', 'msg_u1', 'engineer'),
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
