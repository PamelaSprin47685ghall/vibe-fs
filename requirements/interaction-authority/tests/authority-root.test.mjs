// INTERACTION-AUTHORITY package proof — Root 半边。
//
// PhysicalUserMessage ≠ AuthorityTurn；只有 typed provenance 能创建 LogicalRun；
// Root 独占权；同一 occasion 不得重复获得 authority 型资源（禁自激励）。
//
// 运行：node --test requirements/interaction-authority/tests/authority-root.test.mjs

import assert from 'node:assert/strict'
import test from 'node:test'
import {
  authority,
  authorityRun,
  caseOf,
  continuationKind,
  idValue,
  isAdmissionShaped,
  mapCount,
  mapTryFind,
  physicalUser,
  promoteToAuthorityRoot,
  promptKey,
  promptOrigin,
  providerRun,
  rootKind,
  runtimeId,
  sessionId,
  transportReceipt,
} from '../../verification-system/tests/support/domain.mjs'

// Visible stand-in for sha256：被测属性是哪些字段进 digest，不是 digest 函数本身。
const H = (input) => `H(${input})`

const RUNTIME = runtimeId('rt_1')
const SESSION = sessionId('ses_a')
const PHYSICAL = physicalUser('msg_u1')

const rootFor = (agent = 'fast-coder', physical = PHYSICAL, kind = rootKind.human) =>
  authorityRun.createAuthorityRoot(H, RUNTIME, SESSION, kind, physical, agent)

const profileOf = (...args) => {
  const built = rootFor(...args)
  assert.equal(built.ok, true, built.ok ? '' : `createAuthorityRoot rejected: ${built.error}`)
  return built.value
}

/** 整个 profile 转成可比较纯文本，字段改名即失败。 */
const readProfile = (profile) => ({
  session: idValue.session(profile.SessionId),
  logicalRun: idValue.logicalRun(profile.LogicalRunId),
  authorityRoot: idValue.authorityRoot(profile.AuthorityRootUserMessageId),
  authorityKind: caseOf(profile.AuthorityKind),
  selectedAgent: profile.SelectedAgent,
  peerAgent: profile.PeerAgent,
  canonicalRole: authority.roleLabel(profile.CanonicalRole),
  selectedTier: authority.tierLabel(profile.SelectedTier),
})

// ── INTERACTION-AUTHORITY-001/002：PhysicalUserMessage ≠ AuthorityTurn ──────

test('IA_001_authority_root_id_is_reachable_only_by_promoting_a_physical_message', () => {
  // promoteToAuthorityRoot 是唯一 crossing；profile 只记录提升后的 root。
  assert.equal(idValue.authorityRoot(promoteToAuthorityRoot(PHYSICAL)), 'msg_u1')
  assert.equal(readProfile(profileOf()).authorityRoot, 'msg_u1')
})

test('IA_001_002_no_function_from_transport_receipt_to_authority_root', () => {
  // `accepted-*` 是 transport 收据形态（dispatch 事实），但收据永远到不了 root：
  // facade 暴露生产代码的全部 crossing，缺 promoteReceipt 本身就是断言。
  assert.equal(isAdmissionShaped(transportReceipt('accepted-1a2b')), true)
  assert.equal(isAdmissionShaped(transportReceipt('msg_real')), false)
  assert.equal(typeof promoteToAuthorityRoot, 'function')
  assert.equal(Object.keys({ ...authorityRun }).includes('promoteReceipt'), false)
})

// ── INTERACTION-AUTHORITY-003：Root 独占权 ──────────────────────────────────

test('IA_003_root_fixes_profile_and_derives_peer_role_tier_from_selected_agent_alone', () => {
  assert.deepEqual(readProfile(profileOf('fast-coder')), {
    session: 'ses_a',
    logicalRun: 'H(rt_1\nses_a\nmsg_u1)',
    authorityRoot: 'msg_u1',
    authorityKind: 'HumanRoot',
    selectedAgent: 'fast-coder',
    peerAgent: 'deep-coder',
    canonicalRole: 'coder',
    selectedTier: 'Fast',
  })

  // 对称：选 deep 则 fast 成为 peer，role 不变（AGENT-010：role 不跟 tier 走）。
  assert.deepEqual(readProfile(profileOf('deep-coder')), {
    session: 'ses_a',
    logicalRun: 'H(rt_1\nses_a\nmsg_u1)',
    authorityRoot: 'msg_u1',
    authorityKind: 'HumanRoot',
    selectedAgent: 'deep-coder',
    peerAgent: 'fast-coder',
    canonicalRole: 'coder',
    selectedTier: 'Deep',
  })
})

test('IA_003_new_root_replaces_the_profile_and_clears_everything_run_scoped', () => {
  const first = profileOf('fast-coder')
  let projection = authorityRun.registerAuthority(first, authority.empty)

  // 塞满 run-scoped 状态，使重置可观察而非空洞。
  const key = promptKey('pk_1')
  const claim = authorityRun.claimContinuation(
    key,
    SESSION,
    continuationKind.of('ManagerGuard'),
    first,
    'fast-coder',
    'pd-guard',
  )
  projection = authorityRun.registerClaim(claim, projection)
  projection = authorityRun.acceptClaim(key, physicalUser('msg_c1'), projection)
  assert.deepEqual(
    {
      sequences: mapCount(projection.ClaimSequences),
      acceptedContinuations: mapCount(projection.AcceptedContinuationIds),
    },
    { sequences: 1, acceptedContinuations: 1 },
  )

  const second = profileOf('deep-reviewer', physicalUser('msg_u2'))
  const after = authorityRun.registerAuthority(second, projection)

  assert.deepEqual(readProfile(after.ActiveLogicalRun), readProfile(second))
  assert.deepEqual(readProfile(after.LastAuthorityProfile), readProfile(second))
  assert.deepEqual(
    {
      pending: mapCount(after.PendingClaims),
      sequences: mapCount(after.ClaimSequences),
      acceptedContinuations: mapCount(after.AcceptedContinuationIds),
    },
    { pending: 0, sequences: 0, acceptedContinuations: 0 },
    'PROMPT-002：新 root 重置 continuation、repair 预算与 claim 序列',
  )
})

// ── INTERACTION-AUTHORITY-006：HumanRoot 必须显式 managed agent ─────────────

test('IA_006_bare_legacy_names_are_refused_with_typed_rejection', () => {
  // 裸 role 名没有 tier，无法确定 peer；准入会让 fallback pair 悬空。typed 拒绝让
  // 调用方可按原因分支，不解析散文。精确 legacy 名单 = 迁移 ratchet（HOW）。
  for (const bare of ['coder', 'manager', 'reviewer', 'orchestrator']) {
    const built = rootFor(bare)
    assert.equal(built.ok, false, `'${bare}' must not produce a profile`)
  }
  assert.equal(caseOf(authority.parseAgentName('coder').error), 'LegacyAgentName')
  assert.equal(caseOf(authority.parseAgentName('nonsense').error), 'Malformed')
  assert.equal(caseOf(authority.parseAgentName('unknown-role').error), 'UnknownManagedAgent')
})

test('IA_006_agent_owner_root_claims_reject_bare_legacy_names_too', () => {
  const claim = authorityRun.claimAgentOwnerRoot(promptKey('pk_b'), SESSION, 'pd', 'manager')
  assert.equal(claim.ok, false)
})

// ── INTERACTION-AUTHORITY-010：禁自激励 / repair 预算 ────────────────────────

test('IA_010_one_terminal_provider_run_earns_exactly_one_repair', () => {
  const root = profileOf()
  const terminal = providerRun('run_term')
  let projection = authorityRun.registerAuthority(root, authority.empty)

  const alreadyClaimed = () =>
    authority.repairAlreadyClaimed(SESSION, root.LogicalRunId, terminal, 'empty', projection)

  assert.equal(alreadyClaimed(), false)

  const repair = authorityRun.claimContinuation(
    promptKey('pk_rep'),
    SESSION,
    continuationKind.of('InteractionRepair'),
    root,
    'fast-coder',
    authority.repairPayloadDigest(terminal, 'empty'),
  )
  projection = authorityRun.registerClaim(repair, projection)
  assert.equal(alreadyClaimed(), true, '预算由 ClaimSequences 派生，跨 restart 存活')

  // 不同 terminal 是不同 occasion；同一 terminal 不同 repair kind 也是。
  assert.equal(
    authority.repairAlreadyClaimed(SESSION, root.LogicalRunId, providerRun('run_other'), 'empty', projection),
    false,
  )
  assert.equal(authority.repairAlreadyClaimed(SESSION, root.LogicalRunId, terminal, 'xml_only', projection), false)

  // abandon 后不得再 claim 同一 occasion：repair 不会因此获得第二次预算。
  projection = authorityRun.abandonClaim(promptKey('pk_rep'), projection)
  assert.equal(alreadyClaimed(), true)
})

// ── INTERACTION-AUTHORITY-003 边界：AgentOwnerRoot claim 尚无 run ───────────

test('IA_003_agent_owner_root_claim_has_no_run_until_physical_acceptance', () => {
  const claim = authorityRun.claimAgentOwnerRoot(promptKey('pk_owner'), SESSION, 'pd-owner', 'fast-manager')
  assert.equal(claim.ok, true, claim.ok ? '' : claim.error)

  assert.deepEqual(
    {
      origin: caseOf(claim.value.Origin),
      label: authority.originLabel(claim.value.Origin),
      hasRun: claim.value.LogicalRunId !== undefined,
      hasRoot: claim.value.AuthorityRootUserMessageId !== undefined,
      effectiveAgent: claim.value.EffectiveAgent,
    },
    { origin: 'AuthorityRoot', label: 'AgentOwnerRoot', hasRun: false, hasRoot: false, effectiveAgent: 'fast-manager' },
  )

  // 接受后 root 不进入 continuation 映射（INTERACTION-AUTHORITY-016）。
  let projection = authorityRun.registerClaim(claim.value, authority.empty)
  const physical = physicalUser('msg_owner')
  projection = authorityRun.acceptClaim(promptKey('pk_owner'), physical, projection)
  assert.deepEqual(
    {
      pending: mapCount(projection.PendingClaims),
      acceptedContinuations: mapCount(projection.AcceptedContinuationIds),
    },
    { pending: 0, acceptedContinuations: 0 },
  )
  assert.equal(authorityRun.resolveKnownOrigin(physical, undefined, false, projection), 'UnknownOrigin')
})

// ── INTERACTION-AUTHORITY-016：continuation 用 promptOrigin 构造可解析 ──────

test('IA_005_needhelp_kinds_are_continuations_not_roots', () => {
  for (const name of ['NeedHelpEscalation', 'NeedHelpAdvice']) {
    const origin = promptOrigin.continuation(continuationKind.of(name))
    assert.equal(caseOf(origin), 'Continuation')
    assert.equal(authority.originLabel(origin), name)
  }
  assert.equal(authority.tryParseContinuationKind('HumanRoot'), undefined)
})

// ── INTERACTION-AUTHORITY-017：root 必须能成为 continuation 的延续来源 ──────

test('IA_003_root_becomes_the_continuation_source_for_later_defaults', () => {
  const root = profileOf('fast-coder')
  const before = authorityRun.registerAuthority(root, authority.empty)

  const claim = authorityRun.claimContinuation(
    promptKey('pk_c'),
    SESSION,
    continuationKind.of('BusyAgentNudge'),
    root,
    'deep-coder',
    'pd-n',
  )
  const after = authorityRun.registerClaim(claim, before)
  assert.deepEqual(readProfile(after.ActiveLogicalRun), readProfile(root))
  assert.deepEqual(readProfile(after.LastAuthorityProfile), readProfile(root))
})
