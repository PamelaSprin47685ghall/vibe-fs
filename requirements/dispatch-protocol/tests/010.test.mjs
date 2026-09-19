import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const authority = await import("../../../dist/Interaction/Authority/RuntimeSurface.js");
const dispatch = await import("../../../dist/Interaction/Dispatch/DispatchSurface.js");

const H = (input) => `H(${input})`
const RUNTIME = 'rt_1'
const SESSION = 'ses_a'
const findClaim = (projection, key) => projection.pendingClaims.find((claim) => claim.promptKey === key)
const promptOrigin = (kind) => authority.originForContinuation(kind)
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
const inheritedSeed = (agent, physical) => {
  const owner = authority.createAuthorityRoot(
    H,
    RUNTIME,
    SESSION,
    'HumanRoot',
    physical,
    rootSelection('manager'),
  )
  assert.equal(owner.ok, true, owner.error)
  const inherited = authority.issueInheritedIdentitySeed(agent, owner.value)
  assert.equal(inherited.ok, true, inherited.error)
  return inherited.value
}
const profileOf = () => {
  const built = authority.createAuthorityRoot(
    H,
    RUNTIME,
    SESSION,
    'HumanRoot',
    'msg_u1',
    rootSelection('engineer'),
  )
  assert.equal(built.ok, true, built.ok ? '' : built.error)
  return built.value
}

test('WHAT[dispatch-protocol-010] DP_010_authority_root_profile_cannot_express_a_model', () => {
  const profile = profileOf()
  assert.deepEqual(
    { ...profile, model: profile.model },
    {
      session: 'ses_a',
      logicalRun: 'H(rt_1\nses_a\nmsg_u1)',
      authorityRoot: 'msg_u1',
      authorityKind: 'HumanRoot',
      identitySeed: profile.identitySeed,
      participantIdentity: profile.participantIdentity,
      model: undefined,
    },
  )
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

test('WHAT[dispatch-protocol-010] PROMPT_006_unknown_authority_kind_fails_closed', async () => {
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
test('WHAT[dispatch-protocol-010] PROMPT_006_send_payload_carries_participant_and_no_model', async () => {
  const base = mkdtempSync(join(tmpdir(), 'wxs-send-format-'))
  try {
    const opened = await journal.JournalSurface_bootWithWriterId(base, 'writer-send', 'rt-send', 4242, '2026-01-01T00:00:00Z')
    assert.equal(opened.ok, true, opened.ok ? '' : JSON.stringify(opened.error))
    try {
      const owner = await acceptOwner(opened.journal, 'ses_006_owner')
      const seed = authority.issueInheritedIdentitySeed('engineer', owner).value
      const ownerRoot = await dispatch.sendAgentOwnerRoot(
        capturingPort(),
        opened.journal,
        'ses_006',
        'dispatch this',
        seed,
      )
      const continuation = await dispatch.sendContinuation(
        capturingPort(),
        opened.journal,
        'ses_006',
        'retry the fixed participant',
        'ProviderRetryAttempt',
        profileFor(),
        'Await',
      )
      const captured = [observation(ownerRoot), observation(continuation)]

      assert.deepEqual(
        captured.map((value) => ({ session: value.session, text: value.text })),
        [
          { session: 'ses_006', text: 'dispatch this' },
          { session: 'ses_006', text: 'retry the fixed participant' },
        ],
      )

      assert.deepEqual(
        { agent: captured[0].agent, model: captured[0].model },
        { agent: 'engineer', model: null },
        'SendAgentOwnerRoot must carry Agent = fixed participant and Model = None',
      )
      assert.deepEqual(
        { agent: captured[1].agent, model: captured[1].model },
        { agent: 'engineer', model: null },
        'SendContinuation must carry Agent = fixed participant and Model = None',
      )

      assert.equal(captured[0].directory, null, 'no directory was given')
    } finally {
      journal.JournalSurface_dispose(opened.journal)
    }
  } finally {
    rmSync(base, { recursive: true, force: true })
  }
})
}
