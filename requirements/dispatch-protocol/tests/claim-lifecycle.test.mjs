// DISPATCH-PROTOCOL package proof — claim 生命周期与确定性 dispatch 身份。
//
// PROMPT-005 四态（Claimed → Submitted → PhysicalAccepted / Abandoned）；
// transport receipt ≠ 物理消息身份；PromptKey 确定性幂等身份；
// ClaimSequence 单调（同 payload 的两次独立 logical act 可区分）；
// recovery budget 由 runtime start 派生而非写入。
//
// 运行：node --test requirements/dispatch-protocol/tests/claim-lifecycle.test.mjs

import assert from 'node:assert/strict'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import {
  agentJournal,
  authority,
  authorityRun,
  caseOf,
  continuationKind,
  idValue,
  isAdmissionShaped,
  isSome,
  mapCount,
  mapTryFind,
  physicalUser,
  promptDispatcher,
  promptKey,
  promptOrigin,
  rootKind,
  runtimeId,
  sessionId,
  transportReceipt,
  PromptDispatcherSendModule,
} from '../../verification-system/tests/support/domain.mjs'

const H = (input) => `H(${input})`

const RUNTIME = runtimeId('rt_1')
const SESSION = sessionId('ses_a')

const profileOf = () => {
  const built = authorityRun.createAuthorityRoot(
    H,
    RUNTIME,
    SESSION,
    rootKind.human,
    physicalUser('msg_u1'),
    'fast-coder',
  )
  assert.equal(built.ok, true, built.ok ? '' : built.error)
  return built.value
}

// ── DISPATCH-PROTOCOL-002/003：Submitted 记收据但不解决 claim ───────────────

test('WHAT[DISPATCH-PROTOCOL-002] DP_002_submit_records_the_receipt_without_resolving_the_claim', () => {
  const root = profileOf()
  const key = promptKey('pk_s')
  const claim = authorityRun.claimContinuation(
    key,
    SESSION,
    continuationKind.of('ManagerGuard'),
    root,
    'fast-coder',
    'pd-1',
  )

  let projection = authorityRun.registerAuthority(root, authority.empty)
  projection = authorityRun.registerClaim(claim, projection)
  assert.equal(isSome(mapTryFind(key, projection.PendingClaims).Receipt), false)

  const submitted = authorityRun.submitClaim(key, transportReceipt('accepted-9f'), projection)
  const stored = mapTryFind(key, submitted.PendingClaims)

  assert.deepEqual(
    {
      pending: mapCount(submitted.PendingClaims),
      receipt: idValue.transportReceipt(stored.Receipt),
    },
    { pending: 1, receipt: 'accepted-9f' },
    'Submitted 保持 claim pending：只有真实 chat.message 才解决它',
  )
})

// ── DISPATCH-PROTOCOL-002：abandon 移除 claim，不改 active run ──────────────

test('WHAT[DISPATCH-PROTOCOL-002] DP_002_abandon_removes_the_claim_and_leaves_the_active_run_alone', () => {
  const root = profileOf()
  const key = promptKey('pk_x')
  let projection = authorityRun.registerAuthority(root, authority.empty)
  projection = authorityRun.registerClaim(
    authorityRun.claimContinuation(key, SESSION, continuationKind.of('BusyAgentNudge'), root, 'fast-coder', 'pd-n'),
    projection,
  )

  const after = authorityRun.abandonClaim(key, projection)

  assert.equal(mapCount(after.PendingClaims), 0)
  assert.equal(idValue.logicalRun(after.ActiveLogicalRun.LogicalRunId), idValue.logicalRun(root.LogicalRunId))
})

// ── DISPATCH-PROTOCOL-006：abandon 后序号保持已消费（同 payload 再发得新 key）──

test('WHAT[DISPATCH-PROTOCOL-006] DP_006_abandon_keeps_the_claim_sequence_consumed', () => {
  // 序号保持已消费：复用会让被 abandon 的 dispatch 与它的 retry 派生同一 PromptKey。
  const root = profileOf()
  const key = promptKey('pk_x')
  let projection = authorityRun.registerAuthority(root, authority.empty)
  projection = authorityRun.registerClaim(
    authorityRun.claimContinuation(key, SESSION, continuationKind.of('BusyAgentNudge'), root, 'fast-coder', 'pd-n'),
    projection,
  )

  const after = authorityRun.abandonClaim(key, projection)

  assert.equal(mapCount(after.ClaimSequences), 1)
})

// ── DISPATCH-PROTOCOL-004/005：physical acceptance 只由真实物理证据建立 ─────

test('WHAT[DISPATCH-PROTOCOL-003] DP_003_receipt_shape_distinguishes_admission_from_physical_identity', () => {
  // `accepted-*` 是 admission 形态（Host fire-and-forget 返回的收据）；
  // `msg_*` 是真实物理消息 id。两者同存于 TransportReceipt 类型，但只有后者
  // 能成为 PhysicalAccepted 的证据。
  const admission = transportReceipt('accepted-1a2b')
  const physical = transportReceipt('msg_real')
  assert.equal(idValue.transportReceipt(admission), 'accepted-1a2b')
  assert.equal(idValue.transportReceipt(physical), 'msg_real')
  // admission 形态可判别（DISPATCH-PROTOCOL-003）：`accepted-*` 前缀是
  // Host 回执形态谓词，`msg_*` 不是 admission。
  assert.equal(isAdmissionShaped(admission), true, 'accepted-* 是 admission 形态')
  assert.equal(isAdmissionShaped(physical), false, 'msg_* 不是 admission 形态')
})

// ── DISPATCH-PROTOCOL-005/006：PromptKey 确定性幂等身份 ─────────────────────

test('WHAT[DISPATCH-PROTOCOL-005] DP_005_prompt_key_is_deterministic_and_moves_with_every_component', () => {
  const root = profileOf()
  const base = {
    session: SESSION,
    run: root.LogicalRunId,
    authorityRootId: root.AuthorityRootUserMessageId,
    origin: promptOrigin.continuation(continuationKind.of('ManagerGuard')),
    agent: 'fast-coder',
    payload: 'pd-1',
    sequence: 1,
  }

  const derive = (o) =>
    idValue.promptKey(
      authority.derivePromptKey(H, o.session, o.run, o.authorityRootId, o.origin, o.agent, o.payload, o.sequence),
    )

  assert.equal(
    derive(base),
    `H(${['ses_a', 'H(rt_1\nses_a\nmsg_u1)', 'msg_u1', 'ManagerGuard', 'fast-coder', 'pd-1', '1'].join('\u001f')})`,
  )
  assert.equal(derive(base), derive(base), '同一 logical dispatch 在任何进程派生同一 key')

  const variants = {
    session: { ...base, session: sessionId('ses_b') },
    origin: { ...base, origin: promptOrigin.continuation(continuationKind.of('ReviewerGuard')) },
    agent: { ...base, agent: 'deep-coder' },
    payload: { ...base, payload: 'pd-2' },
    sequence: { ...base, sequence: 2 },
  }

  for (const name of ['session', 'origin', 'agent', 'payload', 'sequence']) {
    assert.notEqual(derive(variants[name]), derive(base), `${name} must participate in the PromptKey`)
  }
})

test('WHAT[DISPATCH-PROTOCOL-005] DP_005_claim_scope_names_exactly_session_run_origin_and_payload', () => {
  // scope 是 join 串（非 hash），四组件可读；第五个组件会改变
  // 「哪些 dispatch 算同一 logical act 重复」。
  const root = profileOf()
  const scope = authority.claimScopeDigest(
    SESSION,
    root.LogicalRunId,
    promptOrigin.continuation(continuationKind.of('ManagerGuard')),
    'pd-guard',
  )

  assert.equal(scope, ['ses_a', 'H(rt_1\nses_a\nmsg_u1)', 'ManagerGuard', 'pd-guard'].join('\u001f'))

  // 缺席 run 用显式 marker 而非空段：「尚无 run」不能与「run id 为空」碰撞。
  assert.equal(
    authority.claimScopeDigest(SESSION, undefined, promptOrigin.hostInternal, 'pd-guard'),
    ['ses_a', '\u0000absent', 'HostInternal', 'pd-guard'].join('\u001f'),
  )
})

test('WHAT[DISPATCH-PROTOCOL-006] DP_006_claim_sequence_advances_on_registration_not_on_resolution', () => {
  const root = profileOf()
  const scope = authority.claimScopeDigest(
    SESSION,
    root.LogicalRunId,
    promptOrigin.continuation(continuationKind.of('ReviewerGuard')),
    'pd-same',
  )

  let projection = authorityRun.registerAuthority(root, authority.empty)
  assert.equal(authority.nextClaimSequence(scope, projection), 1)

  const claimAt = (n) =>
    authorityRun.claimContinuation(
      promptKey(`pk_${n}`),
      SESSION,
      continuationKind.of('ReviewerGuard'),
      root,
      'fast-coder',
      'pd-same',
    )

  projection = authorityRun.registerClaim(claimAt(1), projection)
  assert.equal(authority.nextClaimSequence(scope, projection), 2)

  // abandon 第一个后同 payload 再 claim：序号不回滚，否则两次 dispatch 派生同一 PromptKey。
  projection = authorityRun.abandonClaim(promptKey('pk_1'), projection)
  assert.equal(authority.nextClaimSequence(scope, projection), 2)

  projection = authorityRun.registerClaim(claimAt(2), projection)
  assert.equal(authority.nextClaimSequence(scope, projection), 3)
})

// ── DISPATCH-PROTOCOL-007：runtime-start stamp 只作历史审计，不是 recovery budget ────

test('WHAT[DISPATCH-PROTOCOL-007] DP_007_runtime_start_stamp_is_audit_only_not_restart_recovery_authority', () => {
  const root = profileOf()
  const key = promptKey('pk_r')
  const projection = authorityRun.registerClaim(
    authorityRun.claimContinuation(key, SESSION, continuationKind.of('ManagerGuard'), root, 'fast-coder', 'pd-r'),
    authorityRun.registerAuthority(root, authority.empty),
  )
  const claim = mapTryFind(key, projection.PendingClaims)

  assert.equal(claim.ClaimedAtRuntimeStartCount, 0)
  assert.equal('recoveryAttemptBudget' in authority, false)
  assert.equal('recoveryAttempts' in authority, false)
  assert.equal('recoveryBudgetSpent' in authority, false)
})

// ── DISPATCH-PROTOCOL-010：root profile 无 model 字段（Model=None 不可表达）─

test('WHAT[DISPATCH-PROTOCOL-010] DP_010_authority_root_profile_cannot_express_a_model', () => {
  // 「Model = None always」不是运行时检查——profile 没有该字段，覆盖 model 不可表示。
  assert.deepEqual(Object.keys(profileOf()), [
    'SessionId',
    'LogicalRunId',
    'AuthorityRootUserMessageId',
    'AuthorityKind',
    'SelectedAgent',
    'PeerAgent',
    'CanonicalRole',
    'SelectedTier',
  ])
})

// ── DISPATCH-PROTOCOL-002：root claim 携带 payload digest（恢复可区分）──────

test('WHAT[DISPATCH-PROTOCOL-002] DP_002_claim_records_payload_digest_and_effective_agent', () => {
  const claim = authorityRun.claimAgentOwnerRoot(promptKey('pk_o'), SESSION, 'pd-owner', 'fast-manager')
  assert.equal(claim.ok, true, claim.ok ? '' : claim.error)
  assert.deepEqual(
    {
      origin: caseOf(claim.value.Origin),
      payloadDigest: claim.value.PayloadDigest,
      effectiveAgent: claim.value.EffectiveAgent,
      receipt: claim.value.Receipt,
    },
    { origin: 'AuthorityRoot', payloadDigest: 'pd-owner', effectiveAgent: 'fast-manager', receipt: undefined },
  )
})

// ── DISPATCH-PROTOCOL-001：PromptDispatcher 是唯一写入口 ──────────────────────
// 结构性机器面：所有 user-shaped message 发送成员（root、continuation、repair、
// busy nudge 等）都必须是 `PromptDispatcher.Runtime` 的成员；不存在独立旁路
// （`postPromptFireAndForget` / keyless sender）导出。

test('WHAT[DISPATCH-PROTOCOL-001] DP_001_every_send_member_lives_on_the_prompt_dispatcher_runtime', () => {
  const sendNames = Object.keys(PromptDispatcherSendModule).filter(
    (k) => typeof PromptDispatcherSendModule[k] === 'function',
  )
  assert.ok(sendNames.length >= 6, `send surface must exist, got ${sendNames.length}`)
  for (const name of sendNames) {
    assert.match(
      name,
      /Runtime__Runtime_/,
      `${name} must be a PromptDispatcher.Runtime member — no second writer may exist`,
    )
  }
  // 插件 user-shaped message 的类别都经同一 dispatcher 成员（PROMPT-002/003/006 类别）。
  for (const member of ['SendAgentOwnerRoot', 'SendContinuation', 'SendInteractionRepair', 'SendManagerIdleEncouragement']) {
    assert.ok(
      sendNames.some((n) => n.endsWith(`_${member}`)),
      `${member} must exist on the PromptDispatcher.Runtime send surface`,
    )
  }
  // 禁止独立 fire-and-forget 旁路（PROMPT-007）：只能经 AwaitMode.Detached。
  assert.equal(
    sendNames.some((n) => /FireAndForget|postPrompt/.test(n)),
    false,
    'no standalone postPromptFireAndForget bypass may exist',
  )
})
