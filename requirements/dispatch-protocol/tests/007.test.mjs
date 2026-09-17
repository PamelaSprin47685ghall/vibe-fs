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

test('WHAT[DISPATCH-PROTOCOL-007] DP_007_runtime_start_stamp_is_audit_only_not_restart_recovery_authority', () => {
  const root = profileOf()
  const key = 'pk_r'
  const projection = authority.registerClaim(
    authority.claimContinuation(key, SESSION, 'ManagerGuard', root, 'pd-r'),
    authority.registerAuthority(root, authority.empty),
  )


  const claim = findClaim(projection, key)
  assert.equal(claim.claimedAtRuntimeStartCount, 0)
  assert.deepEqual(dispatch.runtimeStartPolicy(), {
    claimStamp: 'workspace-runtime-start-count',
    advancesWorkspaceWatermark: true,
    restartRecoveryAuthority: false,
  })
})
}

{
const { default: assert } = await import("node:assert/strict");
const { execFileSync } = await import("node:child_process");
const { mkdtempSync, readFileSync, rmSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const { default: test } = await import("node:test");
const authority = await import("../../../dist/Interaction/Authority/RuntimeSurface.js");
const dispatch = await import("../../../dist/Interaction/Dispatch/DispatchSurface.js");
const contract = await import("../../../dist/OpenCode/Host/OpenCodeContract.js");
const journal = await import("../../../dist/Persistence/Journal/Surface.js");

const hash = (value) => `H(${value})`
const capturingPort = (captured, outcome = () => dispatch.admittedWithReceipt('accepted-007')) => ({
  SubscribeTerminal: () => ({ Dispose: () => {} }),
  SendPrompt: async (session, text, options) => {
    captured.push({ session, text, options })
    return outcome()
  },
})
const personas = {
  coder: 'Coder',
  manager: 'Lead',
  devops: 'Operator',
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
const profileFor = (session, runtime = 'rt-007c') => {
  const owner = authority.createAuthorityRoot(
    hash,
    runtime,
    `${session}_owner`,
    'HumanRoot',
    `msg-${session}-owner`,
    rootSelection('manager'),
  )
  assert.equal(owner.ok, true, owner.error)
  const seed = authority.issueInheritedIdentitySeed('coder', owner.value)
  assert.equal(seed.ok, true, seed.error)
  const built = authority.createAuthorityRoot(hash, runtime, session, 'AgentOwnerRoot', `msg-root-${session}`, seed.value)
  assert.equal(built.ok, true, built.ok ? '' : JSON.stringify(built.error))
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
const Verdict = contract.DetachedSendVerdict
const refused = (reason) => new Verdict(1, [reason])
const outcomeUnknown = (reason) => new Verdict(2, [reason])
const verdictPort = (captured, outcome) => ({
  SubscribeTerminal: () => ({ Dispose: () => {} }),
  SendPrompt: async (session, text, options) => {
    captured.push({ session, text, options })
    return outcome()
  },
})
const waitForSettledClaims = async (handle, session, count, message) => {
  const deadline = Date.now() + 1500
  for (;;) {
    if (dispatch.pendingClaimCount(handle, session) === count) return
    if (Date.now() >= deadline) {
      assert.equal(dispatch.pendingClaimCount(handle, session), count, message)
    }
    await new Promise((resolve) => setImmediate(resolve))
  }
}
const journalLines = (base, writerId) =>
  readFileSync(join(base, '.git', 'wanxiang', 'events', `${writerId}.ndjson`), 'utf8')
    .trim()
    .split('\n')
const openGitJournal = async (base, writerId, runtimeId) => {
  execFileSync('git', ['init', '--quiet', base])
  const opened = await journal.JournalSurface_bootWithWriterId(
    join(base, '.git'),
    writerId,
    runtimeId,
    4242,
    '2026-01-01T00:00:00Z',
  )
  assert.equal(opened.ok, true, opened.ok ? '' : JSON.stringify(opened.error))
  return opened
}
const sendDetachedRoot = async (port, handle, session, text, seed) => {
  const sent = await dispatch.sendAgentOwnerRoot(port, handle, session, text, seed)
  assert.equal(sent.ok, true, sent.ok ? '' : sent.error)
  return sent
}

test('WHAT[DISPATCH-PROTOCOL-007] PROMPT_007_detached_refused_abandons_send_failed_without_resend', async () => {
  const base = mkdtempSync(join(tmpdir(), 'wxs-prompt-007-refused-'))
  const writerId = 'writer-007-refused'
  const opened = await openGitJournal(base, writerId, 'rt-007-refused')
  try {
    const owner = await acceptOwner(opened.journal, 'ses_007_refused_owner')
    const seed = authority.issueInheritedIdentitySeed('coder', owner).value
    const captured = []
    const sent = await sendDetachedRoot(
      verdictPort(captured, () => dispatch.retryable('host refused before accept')),
      opened.journal,
      'ses_007_refused',
      'detached refused send',
      seed,
    )
    assert.equal(typeof captured[0].options.DetachedListener, 'function', 'Detached must leave its verdict listener on the send options')
    await waitForSettledClaims(opened.journal, 'ses_007_refused', 0, 'Refused must abandon the exact claim')
    assert.equal(captured.length, 1, 'Refused must never re-emit the physical send')
    const abandoned = journalLines(base, writerId).filter((line) => line.includes('PluginPromptAbandoned'))
    assert.equal(abandoned.length, 1, 'Refused must write exactly one durable Abandoned fact')
    assert.ok(abandoned[0].includes(sent.key), 'the Abandoned fact must name the exact PromptKey')
    assert.ok(abandoned[0].includes('SendFailed'), 'the Abandoned fact must carry the SendFailed reason')
    assert.ok(abandoned[0].includes('host refused before accept'), 'the Abandoned fact must carry the refusal evidence')
  } finally {
    journal.JournalSurface_dispose(opened.journal)
    rmSync(base, { recursive: true, force: true })
  }
})
test('WHAT[DISPATCH-PROTOCOL-007] PROMPT_007_detached_outcome_unknown_keeps_claim_pending_never_resends', async () => {
  const base = mkdtempSync(join(tmpdir(), 'wxs-prompt-007-unknown-'))
  const writerId = 'writer-007-unknown'
  const opened = await openGitJournal(base, writerId, 'rt-007-unknown')
  try {
    const owner = await acceptOwner(opened.journal, 'ses_007_unknown_owner')
    const seed = authority.issueInheritedIdentitySeed('coder', owner).value
    const captured = []
    await sendDetachedRoot(
      verdictPort(captured, () => dispatch.acceptanceUnknown('response lost after enqueue')),
      opened.journal,
      'ses_007_unknown',
      'detached unknown send',
      seed,
    )
    await waitForSettledClaims(opened.journal, 'ses_007_unknown', 1, 'OutcomeUnknown must keep the claim Pending')
    assert.equal(captured.length, 1, 'OutcomeUnknown must never auto-resend the physical send')
    assert.equal(
      journalLines(base, writerId).filter((line) => line.includes('PluginPromptAbandoned')).length,
      0,
      'OutcomeUnknown must never write an Abandoned fact',
    )
    const pending = dispatch.projectionObservation(opened.journal, 'ses_007_unknown').pendingClaims
    assert.equal(pending.length, 1, 'pending evidence must stay observable for later Host reconciliation')
    assert.ok(String(pending[0].receipt).length > 0, 'the pending claim keeps its durable Submitted receipt')
  } finally {
    journal.JournalSurface_dispose(opened.journal)
    rmSync(base, { recursive: true, force: true })
  }
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { mkdtempSync, rmSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const journal = await import("../../../dist/Persistence/Journal/Surface.js");
const authority = await import("../../../dist/Interaction/Authority/RuntimeSurface.js");
const dispatch = await import("../../../dist/Interaction/Dispatch/DispatchSurface.js");
const joinGuard = await import("../../../dist/Interaction/Dispatch/JoinGuardSurface.js");

const managerRootSelection = {
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
const appendAuthorityRoot = async (handle, session) => {
  const owner = await dispatch.acceptHumanRootSelection(
    handle,
    `${session}-owner`,
    `msg-${session}-owner`,
    managerRootSelection,
  )
  assert.equal(owner.ok, true, owner.ok ? '' : owner.error)
  const inherited = authority.issueInheritedIdentitySeed('coder', owner.profile)
  assert.equal(inherited.ok, true, inherited.ok ? '' : inherited.error)
  return dispatch.appendAuthorityRoot(handle, session, inherited.value)
}
const capturingPort = (captured, behaviour = {}) => ({
  SubscribeTerminal: () => ({ Dispose: () => {} }),
  SendPrompt: async (session, text, options) => {
    captured.push({ session, text, options })
    if (behaviour.failFirst && captured.length === 1) {
      return dispatch.retryable('port refused')
    }
    return dispatch.admittedWithReceipt('accepted-jg')
  },
})

test('WHAT[DISPATCH-PROTOCOL-007] JNGD_nudge_releases_the_key_when_send_fails_and_retries', async () => {
  const sid = 'ses_jg2'
  const dir = mkdtempSync(join(tmpdir(), 'wxs-jngd-'))
  const opened = await journal.JournalSurface_bootWithWriterId(dir, 'writer-jg2', 'rt-jg2', 4242, '2026-01-01T00:00:00Z')
  assert.equal(opened.ok, true, opened.ok ? '' : JSON.stringify(opened.error))
  const appended = await appendAuthorityRoot(opened.journal, sid)
  assert.equal(appended.ok, true, appended.ok ? '' : JSON.stringify(appended.error))
  try {
    const captured = []
    const reservations = joinGuard.newReservations()
    const port = capturingPort(captured, { failFirst: true })

    const first = await joinGuard.nudge(port, opened.journal, reservations, sid, 'run-jg-1', null)
    assert.equal(first.outcome, 'NotSent', 'a definite pre-acceptance refusal must surface as NotSent')

    const second = await joinGuard.nudge(port, opened.journal, reservations, sid, 'run-jg-1', null)
    assert.equal(second.outcome, 'Sent', 'the key must be released after a failed send')
    assert.equal(captured.length, 2)
  } finally {
    try { journal.JournalSurface_dispose(opened.journal) } catch {}
    rmSync(dir, { recursive: true, force: true })
  }
})
test('WHAT[DISPATCH-PROTOCOL-007] JNGD_join_gate_dedupes_same_terminal_but_rearms_for_fresh_terminal', async () => {
  const sid = 'ses_jg_repeat'
  const dir = mkdtempSync(join(tmpdir(), 'wxs-jngd-repeat-'))
  const opened = await journal.JournalSurface_bootWithWriterId(dir, 'writer-jg-repeat', 'rt-jg-repeat', 4242, '2026-01-01T00:00:00Z')
  assert.equal(opened.ok, true, opened.ok ? '' : JSON.stringify(opened.error))
  assert.equal((await appendAuthorityRoot(opened.journal, sid)).ok, true)
  try {
    const captured = []
    const reservations = joinGuard.newReservations()
    const port = capturingPort(captured)

    assert.equal((await joinGuard.nudge(port, opened.journal, reservations, sid, 'run-jg-a', null)).outcome, 'Sent')
    assert.equal(
      (await joinGuard.nudge(port, opened.journal, reservations, sid, 'run-jg-a', null)).outcome,
      'AlreadyOutstanding',
      'duplicate observation of one terminal must not double-send',
    )
    assert.equal(
      (await joinGuard.nudge(port, opened.journal, reservations, sid, 'run-jg-b', null)).outcome,
      'Sent',
      'outstanding work after a fresh terminal must receive another JoinGuard reminder',
    )
    assert.equal(captured.length, 2)
  } finally {
    try { journal.JournalSurface_dispose(opened.journal) } catch {}
    rmSync(dir, { recursive: true, force: true })
  }
})
}

{
const { default: assert } = await import("node:assert/strict");
const { mkdtempSync, rmSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const { default: test } = await import("node:test");
const journal = await import("../../../dist/Persistence/Journal/Surface.js");
const authority = await import("../../../dist/Interaction/Authority/RuntimeSurface.js");
const dispatch = await import("../../../dist/Interaction/Dispatch/DispatchSurface.js");
const recovery = await import("../../../dist/Interaction/Dispatch/RecoverySurface.js");

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

test('WHAT[DISPATCH-PROTOCOL-007] DP_007_restarts_never_auto_abandon_an_unresolved_broken_tool', async () => {
  const base = mkdtempSync(join(tmpdir(), 'wxs-dp011b-'))
  try {
    const first = await journal.JournalSurface_bootWithWriterId(base, 'writer-dp011b-1', 'rt_1', 4242, '2020-01-01T00:00:00Z')
    assert.equal(first.ok, true, first.ok ? '' : JSON.stringify(first.error))
    try {
      const captured = []
      const sent = await sendAgentOwnerRoot(
        capturingPort(captured),
        first.journal,
        'ses_011b',
        'never lands',
      )
      assert.equal(sent.ok, true, sent.ok ? '' : sent.error)
      assert.equal(captured.length, 1)

      // Repeated process restarts are not recovery authority. No physical message
      // means StillPending forever unless an explicit later workflow proves it.
      for (let start = 2; start <= 3; start += 1) {
        const reopened = await journal.JournalSurface_bootWithWriterId(
          base,
          `writer-dp011b-${start}`,
          `rt_${start}`,
          4240 + start,
          BOOT_AFTER_CLAIM,
        )
        assert.equal(reopened.ok, true, reopened.ok ? '' : JSON.stringify(reopened.error))
        const outcomes = await recovery.reconcile(reopened.journal, [])
        assert.equal(outcomes.length, 1)
        assert.equal(outcomes[0].outcome, 'StillPending', `启动 ${start}：未超预算，保持 Pending`)
        assert.equal(captured.length, 1, '绝不重发')
        journal.JournalSurface_dispose(reopened.journal)
      }

      // Even a fourth restart cannot manufacture Abandoned/GaveUp.
      const fourth = await journal.JournalSurface_bootWithWriterId(base, 'writer-dp011b-4', 'rt_4', 4244, BOOT_AFTER_CLAIM)
      assert.equal(fourth.ok, true, fourth.ok ? '' : JSON.stringify(fourth.error))
      try {
        const unresolved = await recovery.reconcile(fourth.journal, [])
        assert.equal(unresolved.length, 1)
        assert.equal(unresolved[0].outcome, 'StillPending')
        assert.equal(
          dispatch.pendingClaimCount(fourth.journal, 'ses_011b'),
          1,
          'restart does not rewrite the broken tool into an abandonment terminal',
        )
        assert.equal(captured.length, 1, '全程一次发送：unknown outcome 永不复制逻辑效果')
      } finally {
        journal.JournalSurface_dispose(fourth.journal)
      }
    } finally {
      journal.JournalSurface_dispose(first.journal)
    }
  } finally {
    rmSync(base, { recursive: true, force: true })
  }
})
}
