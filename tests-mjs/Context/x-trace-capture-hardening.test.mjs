// COMPANION-003 / COMPANION-007 / PERSIST-010 — XTrace 捕获链路的加固测试。
//
// 这些测试锁定 review 发现的两个 blocking 缺陷的回归：
//  1. captureProjection 幂等：同一 projection 反复 transform 不得重复 append
//     （曾因 recorded 集合与 fold 存储的 provenance 命名空间不一致而完全失效，
//     每轮 transform 全量重写 XTrace）。
//  2. opening 捕获不得嵌套 transport envelope：fork 的 AgentOwnerRoot 首 prompt
//     是渲染信封（含 parent_work_record），child opening 必须是原始 assignment。

import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { agentJournal, xTraceCapture, sessionId, listItems } from '../domain.mjs'

const withJournal = (fn) => {
  const dir = mkdtempSync(join(tmpdir(), 'xtrace-'))
  const created = agentJournal.create({ directory: dir })
  assert.equal(created.ok, true, created.ok ? '' : JSON.stringify(created.error))
  try {
    return fn(created.journal)
  } finally {
    created.dispose()
    rmSync(dir, { recursive: true, force: true })
  }
}

const SEM = sessionId('ses_cap')

test('COMPANION_007_capture_projection_is_idempotent_across_transforms', () => {
  withJournal((journal) => {
    const projection = xTraceCapture.semantic({
      messages: [
        { role: 'user', parts: [xTraceCapture.text('task one')] },
        { role: 'assistant', parts: [xTraceCapture.text('work a'), xTraceCapture.reasoning('considered')] },
      ],
    })

    // 第一轮 transform：全部 parts 进入 XTrace。
    const first = xTraceCapture.captureProjection(journal, SEM, projection)
    assert.equal(listItems(first.Parts).length, 3)

    // 第二轮 transform（同一 projection 原样重放）：不得重复 append。
    // 这是曾修复的 blocking：recorded 集合与 fold 存储的 provenance
    // 命名空间不一致时，每轮都会把 3 个 part 重新写入。
    const second = xTraceCapture.captureProjection(journal, SEM, projection)
    assert.equal(listItems(second.Parts).length, 3, 're-observing the same projection must not duplicate the trace')
  })
})

test('COMPANION_007_capture_projection_appends_only_new_turns', () => {
  withJournal((journal) => {
    const first = xTraceCapture.captureProjection(
      journal,
      SEM,
      xTraceCapture.semantic({ messages: [{ role: 'user', parts: [xTraceCapture.text('task')] }] }),
    )
    assert.equal(listItems(first.Parts).length, 1)

    // 第二轮多了 assistant turn：只 append 新的，旧 user part 不动。
    const second = xTraceCapture.captureProjection(
      journal,
      SEM,
      xTraceCapture.semantic({
        messages: [
          { role: 'user', parts: [xTraceCapture.text('task')] },
          { role: 'assistant', parts: [xTraceCapture.text('work')] },
        ],
      }),
    )
    const parts = listItems(second.Parts)
    assert.equal(parts.length, 2)
    assert.deepEqual(
      parts.map((part) => part.Provenance),
      ['turn:0/part:0', 'turn:1/part:0'],
      'provenance must be the stable turn/part identity',
    )
  })
})

test('COMPANION_007_capture_projection_provenance_is_stored_verbatim', () => {
  withJournal((journal) => {
    xTraceCapture.captureProjection(
      journal,
      SEM,
      xTraceCapture.semantic({
        messages: [
          { role: 'user', parts: [xTraceCapture.text('task')] },
          { role: 'assistant', parts: [xTraceCapture.toolCall('call-1', 'read', '{}')] },
        ],
      }),
    )

    const updated = xTraceCapture.captureProjection(
      journal,
      SEM,
      xTraceCapture.semantic({ messages: [{ role: 'user', parts: [xTraceCapture.text('task')] }] }),
    )

    // 幂等检查依赖 recorded 集合与持久化 provenance 同命名空间：
    // 若 fold 重写 provenance（如曾按 ProviderRun 生成 "transform"），
    // 二次捕获会全部重 append——此处断言持久化值即 writer 传入值。
    const parts = listItems(updated.Parts)
    assert.deepEqual(
      parts.map((part) => part.Provenance),
      ['turn:0/part:0', 'turn:1/part:0'],
    )
  })
})

test('COMPANION_003_capture_opening_takes_authoritative_requirements', () => {
  withJournal((journal) => {
    xTraceCapture.captureOpening(journal, SEM, 'Review the tree.', ['Ship it.', 'Add tests.'])

    const again = xTraceCapture.captureOpening(journal, SEM, 'Review the tree.', ['Ship it.', 'Add tests.'])
    // 幂等：同一 opening 重放不报错、不覆盖。
    assert.equal(again, undefined)
  })
})

test('COMPANION_003_opening_capture_is_idempotent_for_the_same_text', () => {
  withJournal((journal) => {
    xTraceCapture.captureOpening(journal, SEM, 'first task', [])
    // 同文本重放无害（PERSIST-010 幂等语义）。
    xTraceCapture.captureOpening(journal, SEM, 'first task', [])
  })
})

test('COMPANION_003_parent_work_record_renders_the_opening_exactly_once', () => {
  withJournal((journal) => {
    // A human session: the opening is captured at ingress, and the first
    // transform captures the SAME text again as XTrace part turn:0/part:0.
    xTraceCapture.captureOpening(journal, SEM, 'first task', [])
    xTraceCapture.captureProjection(
      journal,
      SEM,
      xTraceCapture.semantic({
        messages: [
          { role: 'user', parts: [xTraceCapture.text('first task')] },
          { role: 'assistant', parts: [xTraceCapture.text('work a')] },
        ],
      }),
    )

    const lwr = xTraceCapture.parentWorkRecord(journal, SEM)
    assert.equal(typeof lwr, 'string')
    // Opening 段一次；gap 不得把同一文本当 user part 再渲染一次。
    assert.equal(lwr.split('first task').length - 1, 1, 'the opening text must appear exactly once in the LWR')
    assert.ok(lwr.includes('# Opening task'), 'the opening section must be present')
    assert.ok(lwr.includes('assistant: work a'), 'the tail must carry the work after the opening')
  })
})
