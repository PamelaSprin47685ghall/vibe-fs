// INTERACTION-AUTHORITY package proof — Continuation / 来源解析半边。
//
// Continuation 只延长已存在 LogicalRun；来源解析顺序（accepted → claimed →
// compaction → AgentOwnerRoot → UnknownOrigin）；纯函数永不推断 HumanRoot；
// ingress 是唯一可授予 HumanRoot 的边界。
//
// 运行：node --test requirements/interaction-authority/tests/continuation-origin.test.mjs

import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import test from 'node:test'
import { fileURLToPath } from 'node:url'
import {
  authority,
  authorityRun,
  caseOf,
  continuationKind,
  idValue,
  mapCount,
  mapTryFind,
  physicalUser,
  promptKey,
  promptOrigin,
  rootKind,
  runtimeId,
  sessionId,
} from '../../verification-system/tests/support/domain.mjs'

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

const readProfile = (profile) => ({
  session: idValue.session(profile.SessionId),
  logicalRun: idValue.logicalRun(profile.LogicalRunId),
  authorityRoot: idValue.authorityRoot(profile.AuthorityRootUserMessageId),
  authorityKind: caseOf(profile.AuthorityKind),
  selectedAgent: profile.SelectedAgent,
  peerAgent: profile.PeerAgent,
})

// ── INTERACTION-AUTHORITY-004：continuation 继承 run/root，永不改写 profile ─

test('IA_004_a_continuation_never_replaces_the_authority_root', () => {
  const root = profileOf('fast-coder')
  const before = authorityRun.registerAuthority(root, authority.empty)

  const claim = authorityRun.claimContinuation(
    promptKey('pk_c'),
    SESSION,
    continuationKind.of('ProviderRetryAttempt'),
    root,
    'deep-coder',
    'pd-retry',
  )

  assert.deepEqual(
    {
      origin: caseOf(claim.Origin),
      logicalRun: idValue.logicalRun(claim.LogicalRunId),
      authorityRoot: idValue.authorityRoot(claim.AuthorityRootUserMessageId),
      effectiveAgent: claim.EffectiveAgent,
    },
    {
      origin: 'Continuation',
      logicalRun: 'H(rt_1\nses_a\nmsg_u1)',
      authorityRoot: 'msg_u1',
      effectiveAgent: 'deep-coder',
    },
    'continuation 继承 run 与 root，只携带 cursor 选的 agent',
  )

  const after = authorityRun.registerClaim(claim, before)
  assert.deepEqual(readProfile(after.ActiveLogicalRun), readProfile(root))
  assert.deepEqual(readProfile(after.LastAuthorityProfile), readProfile(root))
})

// ── INTERACTION-AUTHORITY-005：所有 continuation kind 可表示且无一是 root ──

test('IA_005_every_continuation_kind_is_representable_and_none_is_a_root', () => {
  const kinds = [
    'InteractionRepair',
    'JoinGuard',
    'ManagerGuard',
    'ReviewerGuard',
    'ReviewConfirmation',
    'BusyAgentNudge',
    'ProviderRetryAttempt',
    'NeedHelpEscalation',
    'NeedHelpAdvice',
    'ManagerWorkActivation',
    'ManagerIdleEncouragement',
    'FinalityRejected',
    'FinalitySteer',
  ]

  for (const name of kinds) {
    const origin = promptOrigin.continuation(continuationKind.of(name))
    assert.equal(caseOf(origin), 'Continuation')
    assert.equal(authority.originLabel(origin), name)
  }

  // 反方向：root 名称不可解析为 continuation；未知名称返回 None（fail-closed）。
  assert.equal(authority.tryParseContinuationKind('AuthorityRoot'), undefined)
  assert.throws(() => continuationKind.of('HumanRoot'), /unknown ContinuationKind/)
})

// ── INTERACTION-AUTHORITY-008/009：来源解析顺序 + 纯函数永不推断 HumanRoot ──

test('IA_008_resolution_order_is_accepted_then_claimed_then_compaction_then_root', () => {
  const root = profileOf('fast-coder', PHYSICAL, rootKind.agentOwner)
  let projection = authorityRun.registerAuthority(root, authority.empty)

  const claimedKey = promptKey('pk_claimed')
  projection = authorityRun.registerClaim(
    authorityRun.claimContinuation(
      claimedKey,
      SESSION,
      continuationKind.of('ReviewConfirmation'),
      root,
      'fast-coder',
      'pd-c',
    ),
    projection,
  )

  const acceptedKey = promptKey('pk_accepted')
  const acceptedPhysical = physicalUser('msg_accepted')
  projection = authorityRun.registerClaim(
    authorityRun.claimContinuation(
      acceptedKey,
      SESSION,
      continuationKind.of('BusyAgentNudge'),
      root,
      'fast-coder',
      'pd-a',
    ),
    projection,
  )
  projection = authorityRun.acceptClaim(acceptedKey, acceptedPhysical, projection)

  const unseen = physicalUser('msg_unseen')

  assert.deepEqual(
    {
      accepted: authorityRun.resolveKnownOrigin(acceptedPhysical, undefined, false, projection),
      claimed: authorityRun.resolveKnownOrigin(unseen, claimedKey, false, projection),
      compaction: authorityRun.resolveKnownOrigin(unseen, undefined, true, projection),
      registeredRoot: authorityRun.resolveKnownOrigin(unseen, promptKey('pk_unknown'), false, projection),
      nothing: authorityRun.resolveKnownOrigin(unseen, undefined, false, projection),
    },
    {
      accepted: 'Continuation',
      claimed: 'Continuation',
      compaction: 'HostInternal',
      registeredRoot: 'AuthorityRoot',
      nothing: 'UnknownOrigin',
    },
  )
})

test('IA_008_an_accepted_id_outranks_host_compaction', () => {
  // 顺序是语义：插件自己发出并见到 accepted 的消息必须是 continuation，
  // 即使同一 turn Host 也报告 compaction。
  const root = profileOf()
  let projection = authorityRun.registerAuthority(root, authority.empty)

  const key = promptKey('pk_both')
  const physical = physicalUser('msg_both')
  projection = authorityRun.registerClaim(
    authorityRun.claimContinuation(key, SESSION, continuationKind.of('ManagerGuard'), root, 'fast-coder', 'pd-b'),
    projection,
  )
  projection = authorityRun.acceptClaim(key, physical, projection)

  assert.equal(authorityRun.resolveKnownOrigin(physical, undefined, true, projection), 'Continuation')
})

test('IA_009_a_human_root_is_never_inferred_by_a_pure_function', () => {
  // resolveKnownOrigin 无法观测「外部接受 + 显式 agent」，永不返回 HumanRoot；
  // 已激活的 HumanRoot 也不会让后来的未知消息像 root。
  const humanRoot = profileOf('fast-coder', PHYSICAL, rootKind.human)
  const projection = authorityRun.registerAuthority(humanRoot, authority.empty)

  assert.equal(caseOf(projection.ActiveLogicalRun.AuthorityKind), 'HumanRoot')
  assert.equal(
    authorityRun.resolveKnownOrigin(physicalUser('msg_new'), promptKey('pk_any'), false, projection),
    'UnknownOrigin',
    'active HumanRoot 不得让后来未知消息看起来像 root',
  )
})

// ── INTERACTION-AUTHORITY-009/015：ingress 结构锁（source-read）────────────

test('IA_009_ingress_does_not_promote_UnknownOrigin_to_HumanRoot_while_run_active', () => {
  const repoRoot = join(dirname(fileURLToPath(import.meta.url)), '../../..')
  const ingress = readFileSync(join(repoRoot, 'src/Wanxiangshu/Application/Prompting/PromptIngress.fs'), 'utf8')

  assert.match(ingress, /ActiveProfile sessionId/, 'HumanRoot 提升必须 gate 在 ActiveLogicalRun 缺席')
  assert.match(ingress, /Some agent, None when isValidAgent agent/, 'HumanRoot 仅当显式 agent 有效且无 active run')
  assert.match(ingress, /\| _ -> PromptAuthority\.PromptOrigin\.UnknownOrigin/, '非首个 prompt 的 UnknownOrigin 保持 fail-closed')
  assert.doesNotMatch(
    ingress,
    /match message\.ExplicitAgent with[\s\S]{0,120}Some agent when isValidAgent agent[\s\S]{0,80}HumanRoot/,
    '禁止旧 fail-open：仅凭 ExplicitAgent 抬权、不看 ActiveProfile',
  )
  assert.match(ingress, /ExplicitAgent, runtime\.ActiveProfile/, '提升配对 ExplicitAgent 与 ActiveProfile（None = 仅首条）')
})

// ── INTERACTION-AUTHORITY-016：accepted root claim 不入 continuation map ────

test('IA_016_accepting_an_authority_root_claim_does_not_enter_the_continuation_map', () => {
  const key = promptKey('pk_owner')
  const claim = authorityRun.claimAgentOwnerRoot(key, SESSION, 'pd-owner', 'fast-manager')
  assert.equal(claim.ok, true, claim.ok ? '' : claim.error)

  let projection = authorityRun.registerClaim(claim.value, authority.empty)
  const physical = physicalUser('msg_owner')
  projection = authorityRun.acceptClaim(key, physical, projection)

  assert.deepEqual(
    {
      pending: mapCount(projection.PendingClaims),
      acceptedContinuations: mapCount(projection.AcceptedContinuationIds),
    },
    { pending: 0, acceptedContinuations: 0 },
    'root 不是 continuation，也不得被记录成 continuation',
  )
  assert.equal(authorityRun.resolveKnownOrigin(physical, undefined, false, projection), 'UnknownOrigin')
})

// ── INTERACTION-AUTHORITY-017：continuation 只接续 active run ──────────────

test('IA_017_unclaimed_continuation_without_active_run_stays_unknown', () => {
  // 无 active run 时，claimed PromptKey 也落 UnknownOrigin（fail-closed）——
  // 绝不凭 key 猜测一个不存在的 continuation。
  const projection = authorityRun.registerAuthority(profileOf(), authority.empty)
  const unknownKey = promptKey('pk_never_claimed')
  assert.equal(
    authorityRun.resolveKnownOrigin(physicalUser('msg_x'), unknownKey, false, projection),
    'UnknownOrigin',
  )
})
