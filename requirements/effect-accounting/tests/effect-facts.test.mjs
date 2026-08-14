// Effect-accounting package-owned law tests (PERSIST-009, storage.md 写入口纪律):
// Requested/Claimed 与 Accepted/Created/Published 是不同 typed 事实；
// reconciliation 先核对物理 effect identity（PublishClaimed 三分支）；
// 结局未知不伪装成已提交；0.5.1 通用 DurableEffectRequested/Accepted 已拒绝。

import assert from 'node:assert/strict'
import test from 'node:test'
import {
  caseOf,
  commitHash,
  envelope,
  fact,
  fold,
  journal,
  managerJobId,
  orchestratorProjection,
  reviewBarrierId,
  sessionId,
  stream,
  targetRef,
  worktreeIdentity,
  worktreePath,
} from '../../verification-system/tests/support/domain.mjs'

const JOB = managerJobId('job_ea')
const MANAGER = stream.session('ses_ea')
const WT = worktreeIdentity('wt_ea')
const WT_PATH = worktreePath('/tmp/wt_ea')

const createFact = () =>
  fact('ManagerJobCreated', {
    ManagerJobId: JOB,
    ManagerSessionId: sessionId('ses_ea'),
    ManagerAgent: 'fast-manager',
    Byname: 'Road',
    WorktreeIdentity: WT,
    WorktreePath: WT_PATH,
    TargetRef: targetRef('refs/heads/main'),
    TargetBranchFrozen: 'refs/heads/main',
  })

const rebasedFact = () =>
  fact('RebasedCandidateReady', {
    ManagerJobId: JOB,
    RebasedCommit: commitHash('r1'),
    TargetHeadSnapshot: commitHash('h1'),
    PostRebaseReviewBarrierId: reviewBarrierId('bar_2'),
  })

const publishClaimedFact = () =>
  fact('PublishClaimed', {
    ManagerJobId: JOB,
    TargetRef: targetRef('refs/heads/main'),
    ExpectedHead: commitHash('h1'),
  })

const requestedFact = () =>
  fact('WorktreeCreateRequested', {
    ManagerJobId: JOB,
    WorktreeIdentity: WT,
    WorktreePath: WT_PATH,
  })

const createdFact = () =>
  fact('WorktreeCreated', {
    ManagerJobId: JOB,
    WorktreeIdentity: WT,
    WorktreePath: WT_PATH,
  })

const foldFacts = (facts) => {
  const result = fold.apply(
    fold.empty,
    facts.map((value, index) => envelope({ seq: index + 1, stream: MANAGER, fact: value })),
  )
  assert.equal(result.ok, true, result.ok ? '' : JSON.stringify(result.error))
  return result.value
}

const jobOf = (projection) => orchestratorProjection.tryFind(JOB, fold.orchestrator(projection))

test('worktree_requested_created_are_distinct_typed_states_not_one_bool', () => {
  // PERSIST-009: Requested（意图）与 Created（物理已发生）是两个 typed 事实；
  // Accepted/Created 存在后不得折回 Requested。
  let proj = orchestratorProjection.empty
  assert.equal(orchestratorProjection.worktreeEffectOf(WT, proj), undefined)

  proj = orchestratorProjection.requestWorktree(WT, WT_PATH, JOB, proj)
  assert.equal(orchestratorProjection.worktreeEffectOf(WT, proj), 'Requested')

  proj = orchestratorProjection.acceptWorktree(WT, WT_PATH, JOB, proj)
  assert.equal(orchestratorProjection.worktreeEffectOf(WT, proj), 'Created')

  // CommitUnknown retry 可能重放 Requested：fold/helper 必须拒绝 Accepted → Requested 回归。
  proj = orchestratorProjection.requestWorktree(WT, WT_PATH, JOB, proj)
  assert.equal(orchestratorProjection.worktreeEffectOf(WT, proj), 'Created')

  // fold 路径与 helper 一致。
  const viaFold = foldFacts([createFact(), requestedFact(), createdFact()])
  assert.equal(orchestratorProjection.worktreeEffectOf(WT, fold.orchestrator(viaFold)), 'Created')
})

test('publish_claimed_recovery_three_branch_order_is_fixed', () => {
  // reconciliation 先查物理 effect identity：三分支固定顺序
  // （已发布 → 目标未变 → 其它）。ORCH-007 / PERSIST-009。
  const projection = foldFacts([createFact(), rebasedFact(), publishClaimedFact()])
  const job = jobOf(projection)
  assert.equal(orchestratorProjection.progressOf(job), 'PublishClaimed')

  // currentHead = claim.RebasedCommit → 发布已经发生，只缺事实 → BackfillPublished。
  assert.equal(orchestratorProjection.recoveryAction(commitHash('r1'), job), 'BackfillPublished')
  // currentHead = claim.ExpectedHead → 目标未变，可以重试 ff。
  assert.equal(orchestratorProjection.recoveryAction(commitHash('h1'), job), 'AttemptPublish')
  // 其它 head → 目标已动，post-rebase witness 作废，重做 rebase+review。
  assert.equal(orchestratorProjection.recoveryAction(commitHash('zzz'), job), 'RebaseAndReviewAgain')
  // 无法观察物理 head → fail closed，绝不猜测。
  assert.equal(orchestratorProjection.recoveryAction(undefined, job), 'FailClosed')
})

test('publish_claim_without_durable_rebase_witness_is_rejected', () => {
  // PublishClaimed 必须基于已 committed 的 RebasedCandidateReady；
  // 凭空 claim（内存猜测）被 fold 拒绝。
  const result = fold.apply(
    fold.empty,
    [createFact(), publishClaimedFact()].map((value, index) =>
      envelope({ seq: index + 1, stream: MANAGER, fact: value }),
    ),
  )
  assert.equal(result.ok, false)
  assert.match(String(result.error), /publish claimed for a job with no rebased candidate/i)
})

test('typed_effect_facts_replace_the_generic_durable_effect_union', () => {
  // 每个 effect 的 Request/Accepted 是 typed fact（WorktreeCreateRequested /
  // WorktreeCreated / PublishClaimed / Published…），不是通用 bool/status 字段。
  const encoded = journal.serializeFact(requestedFact())
  assert.match(encoded, /"WorktreeCreateRequested"/)
  assert.match(journal.serializeFact(createdFact()), /"WorktreeCreated"/)
  assert.match(journal.serializeFact(publishClaimedFact()), /"PublishClaimed"/)

  // 0.5.1 的通用 DurableEffectRequested/Accepted 已被 typed facts 取代；
  // 历史 marker 必须拒绝并给出迁移信息，不得静默双读。
  for (const marker of ['"DurableEffectRequested"', '"DurableEffectAccepted"']) {
    assert.equal(journal.containsLegacyFallbackFields(`{"RuntimeFact":${marker}}`), true, marker)
    const decoded = journal.deserializeFact(`{"AgentFact":{"Orchestrator":${marker}}}`)
    assert.equal(decoded.ok, false)
    assert.equal(decoded.error, journal.pre050MigrationMessage)
  }
})
