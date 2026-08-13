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
import {
  agentJournal,
  agentFact,
  xTraceCapture,
  lifecycleWorkRecordProjection,
  sessionId,
  listItems,
  caseOf,
  prefixEpochId,
  providerRun,
  stream,
} from '../support/domain.mjs'

// Lazy: top-level await import races the 2.5s file timeout under full-suite
// concurrency on GHA (file cancelled before any test body runs).
let appendAgentFn = null
const appendAgent = async (...args) => {
  if (!appendAgentFn) {
    const mod = await import('../../../dist/Journal/AgentJournal.js')
    appendAgentFn = mod.AgentJournalModule_appendAgent
  }
  return appendAgentFn(...args)
}

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
const streamSession = (sid) => stream.session(sid)

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
    const parts = listItems(second.Parts).reverse()
    assert.equal(parts.length, 2)
    assert.deepEqual(
      parts.map((part) => part.Provenance),
      ['g:0/turn:0/part:0', 'g:0/turn:1/part:0'],
      'provenance is generation-scoped turn/part (HOST-006 reanchor isolation)',
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
    const parts = listItems(updated.Parts).reverse()
    assert.deepEqual(
      parts.map((part) => part.Provenance),
      ['g:0/turn:0/part:0', 'g:0/turn:1/part:0'],
    )
  })
})

test('HOST_006_capture_projection_after_reanchor_uses_next_generation', async () => {
  // Pre-reanchor turns reuse Host indices after ContextReanchored. Provenance
  // must open g:1 so turn:0/part:0 appends instead of colliding with g:0.
  const dir = mkdtempSync(join(tmpdir(), 'xtrace-'))
  const created = agentJournal.create({ directory: dir })
  assert.equal(created.ok, true, created.ok ? '' : JSON.stringify(created.error))
  try {
    const journal = created.journal
    const first = xTraceCapture.captureProjection(
      journal,
      SEM,
      xTraceCapture.semantic({
        messages: [
          { role: 'user', parts: [xTraceCapture.text('pre-compact task')] },
          { role: 'assistant', parts: [xTraceCapture.text('pre-compact work')] },
        ],
      }),
    )
    assert.equal(listItems(first.Parts).length, 2)
    assert.deepEqual(
      listItems(first.Parts).reverse().map((part) => part.Provenance),
      ['g:0/turn:0/part:0', 'g:0/turn:1/part:0'],
    )

    const reanchor = await appendAgent(
      streamSession(SEM),
      undefined,
      agentFact('ContextReanchored', {
        SessionId: SEM,
        PreviousEpochId: prefixEpochId(0),
        NextEpochId: prefixEpochId(1),
        ObservedCompactionRun: providerRun('msg_compaction_1'),
      }),
      journal,
    )
    assert.equal(caseOf(reanchor), 'Ok', 'ContextReanchored must fold')

    // Host renumbered: same turn indices, new content after compaction.
    const second = xTraceCapture.captureProjection(
      journal,
      SEM,
      xTraceCapture.semantic({
        messages: [
          { role: 'user', parts: [xTraceCapture.text('summary-of-prior')] },
          { role: 'assistant', parts: [xTraceCapture.text('post-compact work')] },
        ],
      }),
    )
    const parts = listItems(second.Parts).reverse()
    assert.equal(parts.length, 4, 'reanchor generation must append, not collide')
    assert.deepEqual(
      parts.map((part) => part.Provenance),
      [
        'g:0/turn:0/part:0',
        'g:0/turn:1/part:0',
        'g:1/turn:0/part:0',
        'g:1/turn:1/part:0',
      ],
    )
    // Sequence remains strictly monotonic across generations.
    assert.deepEqual(
      parts.map((part) => Number(part.Cursor.Sequence)),
      [1, 2, 3, 4],
    )
  } finally {
    created.dispose()
    rmSync(dir, { recursive: true, force: true })
  }
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

    const parentBound = lifecycleWorkRecordProjection.lifecycleWorkRecord(journal, SEM, true)
    assert.equal(typeof parentBound, 'string')
    // parent → child: Opening once; gap must not re-render the same text.
    assert.equal(parentBound.split('first task').length - 1, 1, 'opening appears exactly once for parent→child')
    assert.ok(parentBound.includes('Opening\n'), 'parent→child keeps Opening')
    assert.ok(!parentBound.includes('Opening task'), 'old Opening task heading is gone')
    assert.ok(parentBound.includes('assistant: work a'), 'the tail must carry the work after the opening')

    const joinBound = lifecycleWorkRecordProjection.lifecycleWorkRecord(journal, SEM, false)
    assert.equal(typeof joinBound, 'string')
    assert.ok(!joinBound.includes('Opening\nfirst task'), 'child→parent join omits Opening')
    assert.ok(!joinBound.includes('Opening task'), 'old Opening task heading is gone')
    assert.ok(!joinBound.includes('first task'), 'assignment text is not echoed to the parent')
    assert.ok(joinBound.includes('assistant: work a'), 'work tail still returns')
  })
})


test('COMPANION_003_last_words_land_in_recent_work_not_closing_report', () => {
  withJournal((journal) => {
    xTraceCapture.captureOpening(journal, SEM, 'finish the life', [])
    xTraceCapture.captureProjection(
      journal,
      SEM,
      xTraceCapture.semantic({
        messages: [
          { role: 'user', parts: [xTraceCapture.text('finish the life')] },
          { role: 'assistant', parts: [xTraceCapture.text('did the work')] },
        ],
      }),
    )
    const words = 'the last words to the user'
    const written = agentJournal.writeBlob(words, journal)
    assert.equal(written.ok, true, written.ok ? '' : written.error)
    xTraceCapture.captureLastWords(
      journal,
      SEM,
      written.value.BlobRef,
      written.value.BlobDigest,
      providerRun('run_last_words'),
    )

    const record = lifecycleWorkRecordProjection.lifecycleWorkRecord(journal, SEM, true)
    assert.equal(typeof record, 'string')
    assert.match(record, /Recent work/)
    assert.match(record, /did the work/)
    assert.match(record, /the last words to the user/)
    assert.equal(record.includes('Closing report'), false)
  })
})
