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

test('WHAT[dispatch-protocol-010] authority root public profile exposes identity without a model field', () => {
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
      const accepted = await dispatch.acceptAgentOwnerRoot(opened.journal, 'ses_006', ownerRoot.key, 'msg-actual-root')
      assert.equal(accepted.ok, true, accepted.error)
      const continuation = await dispatch.sendContinuation(
        capturingPort(),
        opened.journal,
        'ses_006',
        'retry the fixed participant',
        'ProviderRetryAttempt',
        accepted.profile,
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
test('WHAT[dispatch-protocol-010] DP_010_send_agent_owner_root_is_strictly_model_free', async () => {
  const base = mkdtempSync(join(tmpdir(), 'wxs-dp010-model-free-'))
  try {
    const opened = await journal.JournalSurface_bootWithWriterId(base, 'writer-dp010-model-free', 'rt-dp010-model-free', 4242, '2026-01-01T00:00:00Z')
    assert.equal(opened.ok, true, opened.ok ? '' : JSON.stringify(opened.error))
    try {
      const owner = await acceptOwner(opened.journal, 'ses_dp010_owner')
      const seed = authority.issueInheritedIdentitySeed('engineer', owner).value
      const sentOptions = []
      const port = {
        SubscribeTerminal: () => ({ Dispose: () => {} }),
        SendPrompt: async (sessionId, text, options) => {
          sentOptions.push({
            session: sessionId,
            text,
            agent: options.Agent ?? null,
            model: options.Model ?? null,
          })
          return dispatch.admittedWithReceipt('accepted-dp010')
        },
      }
      const sent = await dispatch.sendAgentOwnerRoot(port, opened.journal, 'ses_dp010', 'strictly model free', seed)
      assert.equal(sent.ok, true, sent.ok ? '' : sent.error)
      assert.deepEqual(
        sentOptions,
        [{ session: 'ses_dp010', text: 'strictly model free', agent: 'engineer', model: null }],
        'the Root send path must construct its Host options with Model = None',
      )
    } finally {
      journal.JournalSurface_dispose(opened.journal)
    }
  } finally {
    rmSync(base, { recursive: true, force: true })
  }
})
test('WHAT[dispatch-protocol-010] DP_010_send_without_session_agent_cache_succeeds', async () => {
  const base = mkdtempSync(join(tmpdir(), 'wxs-dp010-no-cache-'))
  try {
    const opened = await journal.JournalSurface_bootWithWriterId(base, 'writer-dp010-no-cache', 'rt-dp010-no-cache', 4242, '2026-01-01T00:00:00Z')
    assert.equal(opened.ok, true, opened.ok ? '' : JSON.stringify(opened.error))
    try {
      // No session agent cache is installed anywhere: the dispatch path must
      // not require one, and the Host send must project the validated seed.
      const owner = await acceptOwner(opened.journal, 'ses_dp010_nocache_owner')
      const seed = authority.issueInheritedIdentitySeed('engineer', owner).value
      const sent = await dispatch.sendAgentOwnerRoot(
        capturingPort(),
        opened.journal,
        'ses_dp010_nocache',
        'send without a session agent cache',
        seed,
      )
      assert.equal(sent.ok, true, sent.ok ? '' : sent.error)
      const observation = sent.observation
      assert.equal(observation.agent, 'engineer')
      assert.equal(observation.model, null)
    } finally {
      journal.JournalSurface_dispose(opened.journal)
    }
  } finally {
    rmSync(base, { recursive: true, force: true })
  }
})
}

{
const assert = (await import('node:assert/strict')).default
const authority = await import('../../../dist/Interaction/Authority/RuntimeSurface.js')
const dispatch = await import('../../../dist/Interaction/Dispatch/DispatchSurface.js')
const { withJournal, acceptOwner, hostPort } = await import('./support/authority.mjs')

for (const [participant, tools] of [
  ['manager', [['*', false], ['js-predictor', true]]],
  ['blogger', undefined],
]) {
  test(`WHAT[dispatch-protocol-010] managed ${participant} assignments retain the accepted run and tool boundary across journal reopen`, async () => {
    await withJournal(`assignment-${participant}`, async (handle, reopen) => {
      const seed = authority.issueInheritedIdentitySeed(participant, await acceptOwner(handle))
      assert.equal(seed.ok, true, seed.error)
      const port = hostPort(async () => dispatch.admittedWithReceipt('accepted-assignment'))
      const first = await dispatch.sendManagedAssignment(port, handle, 'child', 'Investigate the current work.', seed.value, tools)
      assert.equal(first.ok, true, first.error)
      const accepted = await dispatch.acceptAgentOwnerRoot(handle, 'child', first.key, 'assignment-root')
      assert.equal(accepted.ok, true, accepted.error)
      handle = await reopen()

      const second = await dispatch.sendManagedAssignment(port, handle, 'child', 'Investigate the next work.', { kind: 'InvalidSeed' }, tools)
      assert.equal(second.ok, true, second.error)
      const continued = await dispatch.acceptManagedPromptClaim(handle, 'child', 'assignment-next', second.key, participant)
      assert.equal(continued.ok, true, JSON.stringify(continued.error))
      const profile = dispatch.projectionObservation(handle, 'child').activeLogicalRun
      assert.equal(profile.logicalRun, accepted.profile.logicalRun)
      assert.equal(profile.authorityRoot, 'assignment-root')
      assert.equal(profile.participantIdentity.participant, participant)
      assert.notEqual(first.key, second.key)
      for (const sent of [first, second]) {
        assert.equal(sent.observation.agent, participant)
        assert.equal(sent.observation.model, null)
        assert.deepEqual(sent.observation.tools, tools ?? null)
      }
    })
  })
}
test('WHAT[dispatch-protocol-010] synthetic send producers leave model selection to admission at the wire', async () => {
  await withJournal('dp010-producers', async (handle) => {
    const owner = await acceptOwner(handle)
    const seed = authority.issueInheritedIdentitySeed('engineer', owner).value
    const profiles = new Map()
    for (const session of ['ses_p_4', 'ses_p_5', 'ses_p_6']) {
      const root = await dispatch.sendAgentOwnerRoot(hostPort(async () => dispatch.admittedWithReceipt('receipt-root')), handle, session, 'root send', seed)
      assert.equal(root.ok, true, root.error)
      const accepted = await dispatch.acceptAgentOwnerRoot(handle, session, root.key, `msg-producers-root-${session}`)
      assert.equal(accepted.ok, true, accepted.error)
      profiles.set(session, accepted.profile)
    }

    const capturing = () => {
      const seen = []
      const port = hostPort(async (session, text, options) => {
        seen.push({ session, text, agent: options?.agent ?? null, model: options?.model ?? null })
        return dispatch.admittedWithReceipt('receipt-producers')
      })
      return { port, seen }
    }

    const cases = [
      ['sendAgentOwnerRoot', capturing(), (p) => dispatch.sendAgentOwnerRoot(p, handle, 'ses_p_1', 'text', seed)],
      ['sendAgentOwnerRootAwait', capturing(), (p) => dispatch.sendAgentOwnerRootAwait(p, handle, 'ses_p_2', 'text', seed)],
      ['sendManagedAssignment', capturing(), (p) => dispatch.sendManagedAssignment(p, handle, 'ses_p_3', 'text', seed, ['read', 'grep'])],
      ['sendContinuation', capturing(), (p) => dispatch.sendContinuation(p, handle, 'ses_p_4', 'text', 'ProviderRetryAttempt', profiles.get('ses_p_4'), 'Await')],
      ['sendGateNudgesConcurrently', capturing(), async (p) => (await dispatch.sendGateNudgesConcurrently(p, handle, 'ses_p_5', 'text', 'ProviderRetryAttempt', 'JoinGate', 'run-terminal', profiles.get('ses_p_5')))[0]],
      ['sendIdleContinuation', capturing(), (p) => dispatch.sendIdleContinuation(p, handle, 'ses_p_6', 'text', 'ProviderRetryAttempt', profiles.get('ses_p_6'), true)],
    ]

    for (const [name, capture, run] of cases) {
      const result = await run(capture.port)
      const failed = result.ok === false || result.outcome === 'Failed'
      assert.equal(failed, false, name + ': ' + (result.error ?? result.outcome))
      assert.equal(capture.seen.length, 1, name + ' sent one prompt')
      assert.equal(capture.seen[0].model, null, name + ' must leave model selection to admission')
    }
  })
})

test.todo('WHAT[dispatch-protocol-010] compiler rejects adding physical model authority to the opaque root (GAP-136: F# type-level proof pending — the wire model-free test above is retained but does not prove compile-time rejection)')
}
