// DISPATCH-PROTOCOL package proof — PROMPT-011 未决发送恢复：at-most-one。
//
// 崩溃后靠 PromptKey 在 Host 尾部定位真实物理落地：找到 → 补写 PhysicalAccepted
// （Proven）；未找到 → 保持 Pending 且绝不重发（StillPending）；预算耗尽 →
// Restart reconciliation only proves physical acceptance or leaves the old claim pending. It never resends or auto-abandons a broken tool.
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

import * as journal from '../../../dist/Persistence/Journal/Surface.js'
import * as dispatch from '../../../dist/Interaction/Dispatch/DispatchSurface.js'
import * as recovery from '../../../dist/Interaction/Dispatch/RecoverySurface.js'

const BOOT_AFTER_CLAIM = '2099-01-01T00:00:00Z'

const capturingPort = (captured) => ({
  SubscribeTerminal: () => ({ Dispose: () => {} }),
  SendPrompt: async (session, text, options) => {
    captured.push({ text, options })
    return dispatch.admittedWithReceipt('accepted-011')
  },
})

/** role=user 消息：PromptKey 落在 metadata（PROMPT-011 的物理落地证据）。 */
const userMessageWithKey = (id, keyValue) => ({
  id,
  role: 'user',
  metadata: { wanxiangshu_prompt_key: keyValue },
})

test('WHAT[DISPATCH-PROTOCOL-008] DP_008_unproven_outcome_stays_pending_never_resends', async () => {
  const base = mkdtempSync(join(tmpdir(), 'wxs-dp008-'))
  try {
    // 启动 1：发送 AgentOwnerRoot（Detached），Host 只回 receipt —— claim 挂起。
    const first = await journal.JournalSurface_bootWithWriterId(base, 'writer-dp008-1', 'rt_1', 4242, '2026-01-01T00:00:00Z')
    assert.equal(first.ok, true, first.ok ? '' : JSON.stringify(first.error))
    try {
      const captured = []
      const sent = await dispatch.sendAgentOwnerRoot(
        capturingPort(captured),
        first.journal,
        'ses_008',
        'crash before acceptance',
        'fast-coder',
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

test('WHAT[DISPATCH-PROTOCOL-004] DP_004_physical_acceptance_is_proven_only_by_physical_message', async () => {
  const base = mkdtempSync(join(tmpdir(), 'wxs-dp004-'))
  try {
    // 启动 1：发送 AgentOwnerRoot（Detached），Host 只回 receipt（accepted-*）。
    // accepted-* 永远不够：claim 保持 pending，未解决。
    const first = await journal.JournalSurface_bootWithWriterId(base, 'writer-dp004-1', 'rt_1', 4242, '2026-01-01T00:00:00Z')
    assert.equal(first.ok, true, first.ok ? '' : JSON.stringify(first.error))
    try {
      const captured = []
      const sent = await dispatch.sendAgentOwnerRoot(
        capturingPort(captured),
        first.journal,
        'ses_004',
        'crash before acceptance',
        'fast-coder',
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

test('WHAT[DISPATCH-PROTOCOL-007] DP_007_restarts_never_auto_abandon_an_unresolved_broken_tool', async () => {
  const base = mkdtempSync(join(tmpdir(), 'wxs-dp011b-'))
  try {
    const first = await journal.JournalSurface_bootWithWriterId(base, 'writer-dp011b-1', 'rt_1', 4242, '2020-01-01T00:00:00Z')
    assert.equal(first.ok, true, first.ok ? '' : JSON.stringify(first.error))
    try {
      const captured = []
      const sent = await dispatch.sendAgentOwnerRoot(
        capturingPort(captured),
        first.journal,
        'ses_011b',
        'never lands',
        'fast-coder',
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
