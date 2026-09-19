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

test('WHAT[dispatch-protocol-002] DP_002_submit_records_the_receipt_without_resolving_the_claim', () => {
  const root = profileOf()
  const key = 'pk_s'
  const claim = authority.claimContinuation(key, SESSION, 'ManagerGuard', root, 'pd-1')

  let projection = authority.registerAuthority(root, authority.empty)
  projection = authority.registerClaim(claim, projection)
  assert.equal(findClaim(projection, key).receipt, null)

  const submitted = authority.submitClaim(key, 'accepted-9f', projection)
  const stored = findClaim(submitted, key)

  assert.deepEqual(
    {
      pending: submitted.pendingClaims.length,
      receipt: stored.receipt,
    },
    { pending: 1, receipt: 'accepted-9f' },
    'Submitted keeps claim pending: only real chat.message resolves it',
  )
})
test('WHAT[dispatch-protocol-002] DP_002_abandon_removes_the_claim_and_leaves_the_active_run_alone', () => {
  const root = profileOf()
  const key = 'pk_x'
  let projection = authority.registerAuthority(root, authority.empty)
  projection = authority.registerClaim(
    authority.claimContinuation(key, SESSION, 'BusyAgentNudge', root, 'pd-n'),
    projection,
  )

  const after = authority.abandonClaim(key, projection)

  assert.equal(after.pendingClaims.length, 0)
  assert.equal(after.activeLogicalRun.logicalRun, root.logicalRun)
})
test('WHAT[dispatch-protocol-002] DP_002_claim_records_payload_digest_and_participant', () => {
  const claim = authority.claimAgentOwnerRoot(
    'pk_o',
    SESSION,
    'pd-owner',
    inheritedSeed('manager', 'msg-claim-owner'),
  )
  assert.equal(claim.ok, true, claim.ok ? '' : claim.error)
  assert.deepEqual(
    {
      origin: claim.value.origin,
      payloadDigest: claim.value.payloadDigest,
      receipt: claim.value.receipt,
    },
    { origin: 'AuthorityRoot', payloadDigest: 'pd-owner', receipt: null },
  )
  assert.deepEqual(
    claim.value.identitySeed.participantIdentity,
    {
      origin: 'InheritedFromOwner',
      participant: 'manager',
      persona: 'Lead',
      personaCatalogVersion: 1,
      role: 'manager',
    },
    'the owner-root claim carries the fixed participant+role, never an effective agent',
  )
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const dispatch = await import("../../../dist/Interaction/Dispatch/DispatchSurface.js");

const inheritedIdentitySeed = (session) => ({
  kind: 'InheritedFromOwner',
  ownerSession: `${session}-owner`,
  ownerLogicalRun: `run-${session}-owner`,
  ownerAuthorityRoot: `root-${session}-owner`,
  participantIdentity: {
    participant: 'engineer',
    role: 'engineer',
    selectedTier: 'deep',
    persona: 'Lead',
    personaCatalogVersion: 1,
    origin: 'InheritedFromOwner',
  },
})
const claim = (session, key, seq) => ({
  kind: 'claim',
  seq,
  runtime: 'rt-claims',
  session,
  promptKey: key,
  continuationKind: 'ManagerGuard',
  logicalRun: `run-${seq}`,
  authorityRoot: `root-${seq}`,
  identitySeed: inheritedIdentitySeed(session),
  payloadDigest: `pd-${seq}`,
})
const started = (seq, runtime) => ({ kind: 'runtime-start', seq, runtime })
const findClaim = (claims, key) => claims.find((value) => value.promptKey === key)

test('WHAT[dispatch-protocol-002] PROMPT_011_RuntimeStarted_advances_a_workspace_watermark_not_every_session', () => {
  const folded = dispatch.foldRuntimeStartWatermark([
    claim('ses_a', 'pk_a', 1),
    claim('ses_b', 'pk_b', 2),
    started(3, 'rt-1'),
    started(4, 'rt-2'),
    claim('ses_a', 'pk_late', 5),
    started(6, 'rt-3'),
  ])
  assert.equal(folded.ok, true, folded.ok ? '' : JSON.stringify(folded.error))

  const projections = folded.value
  assert.equal(projections.runtimeStartCount, 3)

  const earlyA = findClaim(projections.claims, 'pk_a')
  const earlyB = findClaim(projections.claims, 'pk_b')
  const lateA = findClaim(projections.claims, 'pk_late')

  assert.equal(earlyA.claimedAtRuntimeStartCount, 0)
  assert.equal(earlyB.claimedAtRuntimeStartCount, 0)
  assert.equal(lateA.claimedAtRuntimeStartCount, 2)
  assert.deepEqual(dispatch.runtimeStartPolicy(), {
    claimStamp: 'workspace-runtime-start-count',
    advancesWorkspaceWatermark: true,
    restartRecoveryAuthority: false,
  })
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

test('WHAT[dispatch-protocol-002] HOST_004_stale_idle_repair_is_abandoned_at_the_final_physical_send_boundary', async () => {
  const base = mkdtempSync(join(tmpdir(), 'wxs-idle-send-race-'))
  try {
    const opened = await journal.JournalSurface_bootWithWriterId(base, 'writer-idle-race', 'rt-idle-race', 4242, '2026-01-01T00:00:00Z')
    assert.equal(opened.ok, true, opened.ok ? '' : JSON.stringify(opened.error))
    try {
      const session = 'ses_idle_race'
      const accepted = await dispatch.acceptHumanRoot(opened.journal, session, 'msg-root', 'blogger')
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
        'Superseded',
      )

      assert.equal(outcome.outcome, 'Superseded', 'stale final admission must become Superseded, not a send failure')
      assert.equal(outcome.error, 'Superseded', 'the exact typed quiescence failure must survive physical admission')
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
}
