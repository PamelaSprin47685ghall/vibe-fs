import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const dispatch = await import("../../../dist/Interaction/Dispatch/DispatchSurface.js");

const physicalIdentity = (input, output) => dispatch.decodePhysicalUserMessageId(input, output)

test('WHAT[DISPATCH-PROTOCOL-004] ingress_accepts_the_exact_nonblank_Host_identity', () => {
  assert.equal(physicalIdentity({ messageID: 'msg-input' }, {}), 'msg-input')
  assert.equal(physicalIdentity({}, { message: { id: 'msg-output' } }), 'msg-output')
  assert.equal(
    physicalIdentity({ messageID: 'msg-shared' }, { message: { id: 'msg-shared' } }),
    'msg-shared',
  )
})
test('WHAT[DISPATCH-PROTOCOL-004] ingress_rejects_missing_or_blank_Host_identity', () => {
  assert.equal(physicalIdentity({}, {}), null)
  assert.equal(physicalIdentity({ messageID: '   ' }, { message: { id: '   ' } }), null)
  assert.equal(physicalIdentity({ messageID: '   ' }, { message: { id: 'msg-valid' } }), 'msg-valid')
})
test('WHAT[DISPATCH-PROTOCOL-004] ingress_rejects_conflicting_Host_identity_carriers', () => {
  assert.equal(
    physicalIdentity({ messageID: 'msg-input' }, { message: { id: 'msg-output' } }),
    null,
  )
  assert.equal(
    physicalIdentity({ messageID: 'msg-exact' }, { message: { id: ' msg-exact ' } }),
    null,
  )
})
test('WHAT[DISPATCH-PROTOCOL-004] ingress_ignores_non_contract_identity_decoys', () => {
  assert.equal(physicalIdentity({}, { id: 'msg-output' }), null)
  assert.equal(physicalIdentity({}, { info: { id: 'msg-info' } }), null)
  assert.equal(physicalIdentity({ messageId: 'msg-wrong-case' }, {}), null)
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

test('WHAT[DISPATCH-PROTOCOL-004] DP_004_physical_acceptance_is_proven_only_by_physical_message', async () => {
  const base = mkdtempSync(join(tmpdir(), 'wxs-dp004-'))
  try {
    // 启动 1：发送 AgentOwnerRoot（Detached），Host 只回 receipt（accepted-*）。
    // accepted-* 永远不够：claim 保持 pending，未解决。
    const first = await journal.JournalSurface_bootWithWriterId(base, 'writer-dp004-1', 'rt_1', 4242, '2026-01-01T00:00:00Z')
    assert.equal(first.ok, true, first.ok ? '' : JSON.stringify(first.error))
    try {
      const captured = []
      const sent = await sendAgentOwnerRoot(
        capturingPort(captured),
        first.journal,
        'ses_004',
        'crash before acceptance',
      )
      assert.equal(sent.ok, true, sent.ok ? '' : sent.error)
      assert.ok(sent.key, 'Detached 仍返回 PromptKey')
      const key = sent.key
      assert.equal(captured.length, 1, '发送只发生一次')
      assert.equal(
        dispatch.pendingClaimCount(first.journal, 'ses_004'),
        1,
        'accepted-* 收据不解决 claim —— 物理证据尚未建立',
      )

      // 启动 2：快照里出现 role=user 且携带同一 PromptKey 的物理消息 → Proven。
      const second = await journal.JournalSurface_bootWithWriterId(base, 'writer-dp004-2', 'rt_2', 4243, BOOT_AFTER_CLAIM)
      assert.equal(second.ok, true, second.ok ? '' : JSON.stringify(second.error))
      try {
        const matched = await recovery.reconcile(second.journal, [userMessageWithKey('msg_physical_004', key)])
        assert.equal(matched.length, 1)
        assert.equal(matched[0].outcome, 'Proven')
        assert.equal(
          dispatch.pendingClaimCount(second.journal, 'ses_004'),
          0,
          '找到物理证据 → 补写 PhysicalAccepted，claim 解决',
        )
        assert.equal(captured.length, 1, '恢复只证明，从不重发')
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
}
