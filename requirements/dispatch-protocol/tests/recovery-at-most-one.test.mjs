// DISPATCH-PROTOCOL package proof — PROMPT-011 未决发送恢复：at-most-one。
//
// 崩溃后靠 PromptKey 在 Host 尾部定位真实物理落地：找到 → 补写 PhysicalAccepted
// （Proven）；未找到 → 保持 Pending 且绝不重发（StillPending）；预算耗尽 →
// Abandoned(UnresolvedAfterRecovery)（GaveUp）。恢复只证明或放弃，从不 resend。
//
// 日期约定：create 用远过去的 startedAt；后续 createFromBoot 用远未来的
// startedAt（2099）。store 的 envelope 按 ObservedAt 排序重放，claim 的
// ObservedAt 是写入时刻的真实时钟，因此 claim 恒折叠在 init RuntimeStarted 之后、
// 各次 boot 的 RuntimeStarted 之前 —— stamp=1 稳定，attempts 只随 boot 数增长，
// 无墙钟延迟依赖。
//
// 运行：node --test requirements/dispatch-protocol/tests/recovery-at-most-one.test.mjs

import assert from 'node:assert/strict'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import {
  agentJournal,
  caseOf,
  idValue,
  isSome,
  lib,
  listItems,
  okResult,
  prod,
  promptDispatcher,
  sessionId,
  toList,
  transportReceipt,
} from '../../verification-system/tests/support/domain.mjs'

const Option = await lib('Option.js')
const { reconcile } = await prod('Interaction/Dispatch/Recovery')
const { SessionMessage } = await prod('Infrastructure/OpenCode/Host/SessionSnapshotPort')

const BOOT_AFTER_CLAIM = '2099-01-01T00:00:00Z'

const capturingPort = (captured) => ({
  SubscribeTerminal: () => ({ Dispose: () => {} }),
  SendPrompt: async (session, text, options) => {
    captured.push({ text, options })
    return promptDispatcher.admittedWithReceipt(transportReceipt('accepted-011'))
  },
})

/** role=user 消息：PromptKey 落在 metadata（PROMPT-011 的物理落地证据）。 */
const userMessageWithKey = (id, keyValue) =>
  new SessionMessage(
    id,
    'user',
    undefined,
    undefined,
    undefined,
    undefined,
    undefined,
    false,
    false,
    Option.some(keyValue),
    [],
    undefined,
  )

const snapshotPort = (messages) => ({ GetMessages: async (sid) => okResult(toList(messages)) })

test('DP_011_recovery_never_resends_and_proves_acceptance_from_physical_message', async () => {
  const base = mkdtempSync(join(tmpdir(), 'wxs-dp011-'))
  try {
    // 启动 1：发送 AgentOwnerRoot（Detached），Host 只回 receipt —— claim 挂起。
    const first = await agentJournal.create({ directory: base, runtime: 'rt_1', startedAt: '2026-01-01T00:00:00Z' })
    assert.equal(first.ok, true, first.ok ? '' : JSON.stringify(first.error))
    try {
      const runtime = promptDispatcher.forJournal(first.journal)
      const captured = []
      const sent = await promptDispatcher.sendAgentOwnerRoot(runtime, capturingPort(captured), {
        session: 'ses_011',
        text: 'crash before acceptance',
        agent: 'fast-coder',
      })
      assert.equal(sent.ok, true, sent.ok ? '' : sent.error)
      assert.ok(isSome(sent.key), 'Detached 仍返回 PromptKey')
      const key = sent.key
      assert.equal(captured.length, 1, '发送只发生一次')

      // 启动 2（崩溃后重开同目录）：快照找不到匹配物理消息 → StillPending，
      // 保持 Pending，绝不重发（SendPrompt 不再被调用）。
      const second = await agentJournal.createFromBoot({
        directory: base,
        runtime: 'rt_2',
        startedAt: BOOT_AFTER_CLAIM,
      })
      assert.equal(second.ok, true, second.ok ? '' : JSON.stringify(second.error))
      try {
        const secondRuntime = promptDispatcher.forJournal(second.journal)
        const noMatch = listItems(await reconcile(second.journal, snapshotPort([])))
        assert.equal(noMatch.length, 1, '恰好一条 pending claim 被恢复')
        assert.equal(caseOf(noMatch[0].Outcome), 'StillPending')
        assert.equal(captured.length, 1, '未证明物理落地 → 绝不自动重发')
        assert.equal(
          promptDispatcher.pendingClaimCount(secondRuntime, 'ses_011'),
          1,
          '未找到时 claim 保持 Pending',
        )
      } finally {
        second.dispose()
      }

      // 启动 3：快照里出现 role=user 且携带同一 PromptKey 的物理消息 → Proven。
      const third = await agentJournal.createFromBoot({
        directory: base,
        runtime: 'rt_3',
        startedAt: BOOT_AFTER_CLAIM,
      })
      assert.equal(third.ok, true, third.ok ? '' : JSON.stringify(third.error))
      try {
        const thirdRuntime = promptDispatcher.forJournal(third.journal)
        const matched = listItems(
          await reconcile(third.journal, snapshotPort([userMessageWithKey('msg_physical_011', key)])),
        )
        assert.equal(matched.length, 1)
        assert.equal(caseOf(matched[0].Outcome), 'Proven')
        assert.equal(
          promptDispatcher.pendingClaimCount(thirdRuntime, 'ses_011'),
          0,
          '找到物理证据 → 补写 PhysicalAccepted，claim 解决',
        )
        assert.equal(captured.length, 1, '恢复只证明，从不重发')
      } finally {
        third.dispose()
      }
    } finally {
      first.dispose()
    }
  } finally {
    rmSync(base, { recursive: true, force: true })
  }
})

test('DP_011_budget_exhausted_abandons_unresolved_claim_instead_of_resending', async () => {
  const base = mkdtempSync(join(tmpdir(), 'wxs-dp011b-'))
  try {
    const first = await agentJournal.create({ directory: base, runtime: 'rt_1', startedAt: '2020-01-01T00:00:00Z' })
    assert.equal(first.ok, true, first.ok ? '' : JSON.stringify(first.error))
    try {
      const runtime = promptDispatcher.forJournal(first.journal)
      const captured = []
      const sent = await promptDispatcher.sendAgentOwnerRoot(runtime, capturingPort(captured), {
        session: 'ses_011b',
        text: 'never lands',
        agent: 'fast-coder',
      })
      assert.equal(sent.ok, true, sent.ok ? '' : sent.error)
      assert.equal(captured.length, 1)

      // 重启 2、3：attempts = RuntimeStartCount - stamp 仍 < RecoveryAttemptBudget=3，
      // 未找到物理消息 → StillPending，claim 保持。
      for (let start = 2; start <= 3; start += 1) {
        const reopened = await agentJournal.createFromBoot({
          directory: base,
          runtime: `rt_${start}`,
          startedAt: BOOT_AFTER_CLAIM,
        })
        assert.equal(reopened.ok, true, reopened.ok ? '' : JSON.stringify(reopened.error))
        const outcomes = listItems(await reconcile(reopened.journal, snapshotPort([])))
        assert.equal(outcomes.length, 1)
        assert.equal(caseOf(outcomes[0].Outcome), 'StillPending', `启动 ${start}：未超预算，保持 Pending`)
        assert.equal(captured.length, 1, '绝不重发')
        reopened.dispose()
      }

      // 重启 4：attempts = 4-1 = 3 ≥ RecoveryAttemptBudget=3 → GaveUp
      // （Abandoned(UnresolvedAfterRecovery)）。
      const fourth = await agentJournal.createFromBoot({
        directory: base,
        runtime: 'rt_4',
        startedAt: BOOT_AFTER_CLAIM,
      })
      assert.equal(fourth.ok, true, fourth.ok ? '' : JSON.stringify(fourth.error))
      try {
        const fourthRuntime = promptDispatcher.forJournal(fourth.journal)
        const gaveUp = listItems(await reconcile(fourth.journal, snapshotPort([])))
        assert.equal(gaveUp.length, 1)
        assert.equal(caseOf(gaveUp[0].Outcome), 'GaveUp')
        assert.equal(
          promptDispatcher.pendingClaimCount(fourthRuntime, 'ses_011b'),
          0,
          '预算耗尽 → Abandoned，claim 从 Pending 移除',
        )
        assert.equal(captured.length, 1, '全程一次发送：unknown outcome 永不复制逻辑效果')
      } finally {
        fourth.dispose()
      }
    } finally {
      first.dispose()
    }
  } finally {
    rmSync(base, { recursive: true, force: true })
  }
})
