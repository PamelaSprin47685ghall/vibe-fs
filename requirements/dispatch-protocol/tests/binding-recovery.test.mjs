// DISPATCH-PROTOCOL package proof — turn binding recovery restores the durable
// participant+role (WHAT[DISPATCH-PROTOCOL-012]).
//
// The durable authority profile is the same profile Binding.fromProjection reads
// when it projects `Role` for the active run. Recovery is observed through the
// compiled dispatch/authority surfaces: a missing process-local role never
// shadows the durable participant+role, explicit Host agent evidence must match
// the durable participant, and continuations preserve the participant.

import assert from 'node:assert/strict'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import * as authority from '../../../dist/Interaction/Authority/RuntimeSurface.js'
import * as dispatch from '../../../dist/Interaction/Dispatch/DispatchSurface.js'
import * as journal from '../../../dist/Persistence/Journal/Surface.js'

const hash = (value) => `H(${value})`

const managerSelection = {
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

const capturingPort = () => ({
  SubscribeTerminal: () => ({ Dispose: () => {} }),
  SendPrompt: async () => dispatch.admittedWithReceipt('accepted-binding'),
})

test('WHAT[DISPATCH-PROTOCOL-012] recovered turn binding restores durable participant and role when process-local role is absent', async () => {
  const base = mkdtempSync(join(tmpdir(), 'wxs-binding-recovery-'))
  try {
    const opened = await journal.JournalSurface_bootWithWriterId(base, 'writer-binding', 'rt-binding', 4242, '2026-01-01T00:00:00Z')
    assert.equal(opened.ok, true, opened.ok ? '' : JSON.stringify(opened.error))
    try {
      const session = 'ses_binding'
      const accepted = await dispatch.acceptHumanRootSelection(
        opened.journal,
        session,
        'msg-binding-root',
        managerSelection,
      )
      assert.equal(accepted.ok, true, accepted.ok ? '' : accepted.error)

      // The durable authority profile carries the fixed participant+role.
      const durable = dispatch.projectionObservation(opened.journal, session).activeLogicalRun
      assert.deepEqual(durable.participantIdentity, {
        origin: 'ResolvedAtRoot',
        participant: 'manager',
        persona: 'Lead',
        personaCatalogVersion: 1,
        role: 'manager',
      })

      // A process-local binding with a missing role decodes to no explicit
      // agent; the absence must not shadow the durable participant+role.
      const missing = dispatch.decodeIngress({}, {})
      assert.equal(missing.explicitAgent, null)
      assert.deepEqual(
        dispatch.projectionObservation(opened.journal, session).activeLogicalRun.participantIdentity,
        durable.participantIdentity,
        'absent process-local role falls back to the durable participant+role',
      )

      // Explicit Host agent evidence consistent with the durable participant
      // preserves it; the witness carries participant+role, never an agent alias.
      const matched = await dispatch.acceptManagedExternal(opened.journal, session, 'msg-binding-ext', 'manager')
      assert.deepEqual(
        { ok: matched.ok, participant: matched.participant, role: matched.role },
        { ok: true, participant: 'manager', role: 'manager' },
      )

      // A mismatched explicit agent is rejected and the durable binding is
      // unchanged: fresh physical evidence never changes the participant.
      const mismatched = await dispatch.acceptManagedExternal(opened.journal, session, 'msg-binding-other', 'coder')
      assert.equal(mismatched.ok, false)
      assert.deepEqual(
        dispatch.projectionObservation(opened.journal, session).activeLogicalRun.participantIdentity,
        durable.participantIdentity,
      )

      // A continuation on the durable profile preserves the participant: the
      // Host send carries agent = participant and the claim keeps participant+role.
      const profile = authority.createAuthorityRoot(hash, 'rt-binding', session, 'HumanRoot', 'msg-binding-root', managerSelection)
      assert.equal(profile.ok, true, profile.ok ? '' : profile.error)
      const sent = await dispatch.sendContinuation(
        capturingPort(),
        opened.journal,
        session,
        'manager continuation',
        'ManagerGuard',
        profile.value,
        'Await',
      )
      assert.equal(sent.ok, true, sent.ok ? '' : sent.error)
      assert.equal(sent.observation.agent, 'manager', 'continuation Host send carries agent = participant')
      assert.equal(sent.observation.model, null)
      const claims = dispatch.projectionObservation(opened.journal, session).pendingClaims
      const continuation = claims.find((claim) => claim.promptKey === sent.key)
      assert.deepEqual(
        { participant: continuation.participant, role: continuation.role },
        { participant: 'manager', role: 'manager' },
        'the continuation claim preserves the durable participant',
      )
    } finally {
      journal.JournalSurface_dispose(opened.journal)
    }
  } finally {
    rmSync(base, { recursive: true, force: true })
  }
})
