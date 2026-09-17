import assert from 'node:assert/strict'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import * as journal from '../../../dist/Persistence/Journal/Surface.js'
import * as authority from '../../../dist/Interaction/Authority/RuntimeSurface.js'
import * as dispatch from '../../../dist/Interaction/Dispatch/DispatchSurface.js'
import * as recovery from '../../../dist/Interaction/Dispatch/RecoverySurface.js'

const BOOT_AFTER_CLAIM = '2099-01-01T00:00:00Z'

const capturingPort = (captured) => ({
  SubscribeTerminal: () => ({ Dispose: () => {} }),
  SendPrompt: async (session, text, options) => {
    captured.push({ text, options })
    return dispatch.admittedWithReceipt('accepted-011')
  },
})

const inheritedIdentity = {
  kind: 'RootSelection',
  ownerSession: null,
  ownerLogicalRun: null,
  ownerAuthorityRoot: null,
  participantIdentity: {
    participant: 'manager',
    role: 'manager',
    selectedTier: 'deep',
    persona: 'Lead',
    personaCatalogVersion: 1,
    origin: 'ResolvedAtRoot',
  },
}

const sendAgentOwnerRoot = async (port, handle, session, text) => {
  const ownerSession = `${session}_owner`
  const owner = await dispatch.acceptHumanRootSelection(
    handle,
    ownerSession,
    `msg_${ownerSession}`,
    inheritedIdentity,
  )
  assert.equal(owner.ok, true, owner.ok ? '' : owner.error)
  const seed = authority.issueInheritedIdentitySeed('coder', owner.profile)
  assert.equal(seed.ok, true, seed.ok ? '' : seed.error)
  return dispatch.sendAgentOwnerRoot(port, handle, session, text, seed.value)
}

const userMessageWithKey = (id, keyValue) => ({
  id,
  role: 'user',
  metadata: { wanxiangshu_prompt_key: keyValue },
})

test('WHAT[DISPATCH-PROTOCOL-013] DP_013_construction_waits_for_durability_activation_before_explicit_recovery', async () => {
  const base = mkdtempSync(join(tmpdir(), 'wxs-dispatch-activation-'))
  try {
    const first = await journal.JournalSurface_bootWithWriterId(
      base,
      'writer-dispatch-activation-1',
      'rt-dispatch-activation-1',
      4242,
      '2026-01-01T00:00:00Z',
    )
    assert.equal(first.ok, true, first.ok ? '' : JSON.stringify(first.error))

    let key
    try {
      const captured = []
      const sent = await sendAgentOwnerRoot(
        capturingPort(captured),
        first.journal,
        'ses_dispatch_activation',
        'recover only after durable activation',
      )
      assert.equal(sent.ok, true, sent.ok ? '' : sent.error)
      assert.equal(captured.length, 1)
      key = sent.key
    } finally {
      journal.JournalSurface_dispose(first.journal)
    }

    const activated = await journal.JournalSurface_bootWithWriterId(
      base,
      'writer-dispatch-activation-2',
      'rt-dispatch-activation-2',
      4243,
      BOOT_AFTER_CLAIM,
    )
    assert.equal(activated.ok, true, activated.ok ? '' : JSON.stringify(activated.error))
    try {
      assert.equal(
        dispatch.pendingClaimCount(activated.journal, 'ses_dispatch_activation'),
        1,
        'constructing the registered dispatch surface must not read Host evidence or start recovery',
      )

      const outcomes = await recovery.reconcile(activated.journal, [
        userMessageWithKey('msg_dispatch_activation', key),
      ])
      assert.deepEqual(outcomes, [
        {
          session: 'ses_dispatch_activation',
          promptKey: key,
          outcome: 'Proven',
          physicalMessageId: 'msg_dispatch_activation',
          hasReceipt: null,
          reason: null,
        },
      ])
      assert.equal(
        dispatch.pendingClaimCount(activated.journal, 'ses_dispatch_activation'),
        0,
        'only explicit recovery after successful durable activation may establish PhysicalAccepted',
      )
    } finally {
      journal.JournalSurface_dispose(activated.journal)
    }
  } finally {
    rmSync(base, { recursive: true, force: true })
  }
})
