import test from 'node:test'

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

test('WHAT[dispatch-protocol-008] DP_008_unproven_outcome_stays_pending_never_resends', async () => {
  const base = mkdtempSync(join(tmpdir(), 'wxs-dp008-'))
  try {
    // 启动 1：发送 AgentOwnerRoot（Detached），Host 只回 receipt —— claim 挂起。
    const first = await journal.JournalSurface_bootWithWriterId(base, 'writer-dp008-1', 'rt_1', 4242, '2026-01-01T00:00:00Z')
    assert.equal(first.ok, true, first.ok ? '' : JSON.stringify(first.error))
    try {
      const captured = []
      const sent = await sendAgentOwnerRoot(
        capturingPort(captured),
        first.journal,
        'ses_008',
        'crash before acceptance',
      )
      assert.equal(sent.ok, true, sent.ok ? '' : sent.error)
      assert.ok(sent.key, 'Detached 仍返回 PromptKey')
      assert.equal(captured.length, 1, '发送只发生一次')

      // 启动 2（崩溃后重开同目录）：快照找不到匹配物理消息 → StillPending，
      // 保持 Pending，绝不重发（SendPrompt 不再被调用）。
      const second = await journal.JournalSurface_bootWithWriterId(base, 'writer-dp008-2', 'rt_2', 4243, BOOT_AFTER_CLAIM)
      assert.equal(second.ok, true, second.ok ? '' : JSON.stringify(second.error))
      try {
        const noMatch = await recovery.reconcile(second.journal, [])
        assert.equal(noMatch.length, 1, '恰好一条 pending claim 被恢复')
        assert.equal(noMatch[0].outcome, 'StillPending')
        assert.equal(captured.length, 1, '未证明物理落地 → 绝不自动重发')
        assert.equal(
          dispatch.pendingClaimCount(second.journal, 'ses_008'),
          1,
          '未找到时 claim 保持 Pending',
        )
      } finally {
        journal.JournalSurface_dispose(second.journal)
      }
    } finally {
      journal.JournalSurface_dispose(first.journal)
    }
  } finally {
    rmSync(base, { recursive: true, force: true })
  }
})
test('WHAT[dispatch-protocol-008] DP_008_snapshot_unreadable_is_no_proof_and_keeps_the_claim_pending', async () => {
  const unreadableBase = mkdtempSync(join(tmpdir(), 'wxs-dp008-unreadable-'))
  try {
    const unreadableFirst = await journal.JournalSurface_bootWithWriterId(unreadableBase, 'writer-dp008-unreadable-1', 'rt_1', 4242, '2026-01-01T00:00:00Z')
    assert.equal(unreadableFirst.ok, true, unreadableFirst.ok ? '' : JSON.stringify(unreadableFirst.error))
    try {
      const unreadableCaptured = []
      const unreadableSent = await sendAgentOwnerRoot(
        capturingPort(unreadableCaptured),
        unreadableFirst.journal,
        'ses_008_unreadable',
        'crash before acceptance',
      )
      assert.equal(unreadableSent.ok, true, unreadableSent.ok ? '' : unreadableSent.error)
      assert.equal(dispatch.pendingClaimCount(unreadableFirst.journal, 'ses_008_unreadable'), 1)

      // snapshot-unreadable ambiguity state: the Host snapshot port fails, so
      // production reconcile must report Unreadable — never Proven — while the
      // claim stays pending and nothing is resent.
      const unreadableOutcomes = await recovery.reconcileWithUnreadableSnapshot(
        unreadableFirst.journal,
        'snapshot unreadable: GetMessages failed',
      )
      assert.equal(unreadableOutcomes.length, 1)
      assert.equal(unreadableOutcomes[0].outcome, 'Unreadable')
      assert.equal(unreadableOutcomes[0].session, 'ses_008_unreadable')
      assert.match(String(unreadableOutcomes[0].reason), /unreadable/)
      assert.equal(
        dispatch.pendingClaimCount(unreadableFirst.journal, 'ses_008_unreadable'),
        1,
        'the claim is neither marked Proven nor deleted',
      )
      assert.equal(unreadableCaptured.length, 1, 'unreadable snapshot must not trigger a resend')
    } finally {
      journal.JournalSurface_dispose(unreadableFirst.journal)
    }
  } finally {
    rmSync(unreadableBase, { recursive: true, force: true })
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

test('WHAT[dispatch-protocol-008] DP_008_concurrent_exact_gate_nudges_share_one_claim_and_send', async () => {
  const base = mkdtempSync(join(tmpdir(), 'wxs-gate-single-flight-'))
  try {
    const opened = await journal.JournalSurface_bootWithWriterId(
      base,
      'writer-gate-single-flight',
      'rt-gate-single-flight',
      4242,
      '2026-01-01T00:00:00Z',
    )
    assert.equal(opened.ok, true, opened.ok ? '' : JSON.stringify(opened.error))
    try {
      const sends = []
      const port = {
        SubscribeTerminal: () => ({ Dispose: () => {} }),
        SendPrompt: async (session, text, options) => {
          sends.push({ session, text, options })
          return dispatch.admittedWithReceipt('accepted-gate')
        },
      }
      const session = 'ses_gate'
      const results = await dispatch.sendGateNudgesConcurrently(
        port,
        opened.journal,
        session,
        'continue this exact terminal',
        'ManagerGuard',
        'ManagerAction',
        'run_terminal',
        profileFor('rt-gate-single-flight', session, 'msg_gate', 'manager'),
      )

      assert.deepEqual(results.map((result) => result.ok), [true, true])
      assert.equal(results[0].key, results[1].key, 'both observers await the same logical dispatch')
      assert.equal(sends.length, 1, 'one exact terminal occasion reaches Host once')
      assert.equal(dispatch.projectionObservation(opened.journal, session).pendingClaims.length, 1)
    } finally {
      journal.JournalSurface_dispose(opened.journal)
    }
  } finally {
    rmSync(base, { recursive: true, force: true })
  }
})
}
