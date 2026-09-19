import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { mkdtempSync, rmSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const { default: test } = await import("node:test");
const authority = await import("../../../dist/Interaction/Authority/RuntimeSurface.js");
const dispatch = await import("../../../dist/Interaction/Dispatch/DispatchSurface.js");
const journal = await import("../../../dist/Persistence/Journal/Surface.js");

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

test('WHAT[dispatch-protocol-012] recovered turn binding restores durable participant and role when process-local role is absent', async () => {
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
}

{
const { default: assert } = await import("node:assert/strict");
const { mkdtempSync, rmSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const { default: test } = await import("node:test");
const authority = await import("../../../dist/Interaction/Authority/RuntimeSurface.js");
const dispatch = await import("../../../dist/Interaction/Dispatch/DispatchSurface.js");
const journal = await import("../../../dist/Persistence/Journal/Surface.js");

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

test('WHAT[dispatch-protocol-012] DP_012_physical_acceptance_hands_exact_claim_identity_to_managed_execution', async () => {
  const base = mkdtempSync(join(tmpdir(), 'wxs-dispatch-handoff-'))
  try {
    const opened = await journal.JournalSurface_bootWithWriterId(
      base,
      'writer-dispatch-handoff',
      'rt-dispatch-handoff',
      4242,
      '2026-01-01T00:00:00Z',
    )
    assert.equal(opened.ok, true, opened.ok ? '' : JSON.stringify(opened.error))
    try {
      const owner = await acceptOwner(opened.journal, 'ses_dispatch_handoff_owner')
      const inherited = authority.issueInheritedIdentitySeed('engineer', owner)
      assert.equal(inherited.ok, true, inherited.ok ? '' : inherited.error)

      const sent = await dispatch.sendAgentOwnerRoot(
        capturingPort(),
        opened.journal,
        'ses_dispatch_handoff',
        'handoff exact accepted identity',
        inherited.value,
      )
      assert.equal(sent.ok, true, sent.ok ? '' : sent.error)

      const claimed = dispatch.projectionObservation(opened.journal, 'ses_dispatch_handoff').pendingClaims
      assert.equal(claimed.length, 1)
      assert.equal(claimed[0].promptKey, sent.key)
      assert.deepEqual(claimed[0].identitySeed, inherited.value)
      assert.deepEqual(claimed[0].identitySeed.participantIdentity, {
        origin: 'InheritedFromOwner',
        participant: 'engineer',
        persona: 'Lead',
        personaCatalogVersion: 1,
        role: 'engineer',
      })

      const wrongClaim = await dispatch.acceptManagedPromptClaim(
        opened.journal,
        'ses_dispatch_handoff',
        'msg_dispatch_handoff',
        `${sent.key}-wrong`,
        'engineer',
      )
      assert.equal(wrongClaim.ok, false, 'managed acceptance must reject a non-exact PromptKey')
      assert.equal(dispatch.pendingClaimCount(opened.journal, 'ses_dispatch_handoff'), 1)

      const accepted = await dispatch.acceptManagedPromptClaim(
        opened.journal,
        'ses_dispatch_handoff',
        'msg_dispatch_handoff',
        sent.key,
        'engineer',
      )
      assert.deepEqual(accepted, {
        ok: true,
        error: null,
        sessionId: 'ses_dispatch_handoff',
        physicalUserMessageId: 'msg_dispatch_handoff',
        origin: 'AgentOwnerRoot',
        participant: 'engineer',
        role: 'engineer',
      }, 'the durable managed-execution witness must carry the exact physical identity and participant+role')
      assert.equal(dispatch.pendingClaimCount(opened.journal, 'ses_dispatch_handoff'), 0)
    } finally {
      journal.JournalSurface_dispose(opened.journal)
    }
  } finally {
    rmSync(base, { recursive: true, force: true })
  }
})
}
