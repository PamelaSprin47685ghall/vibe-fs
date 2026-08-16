// requirements/provider-attempt-recovery/tests/abort-residue.test.mjs
//
// PAR-012: Host abort / cleanup 残留不计入推进。Host 因 abort 清理把在途工具调用
// 标记为失败(status=error 且 metadata.interrupted=true)不是已确认的 provider
// attempt 失败:不得推进任何 cursor,也不得消耗自动恢复预算。判据只看 Host 标记
// (Session/EnforcerRepair.fs 的 interrupted 判定),不看错误散文。status=error 且
// 无 interrupted(工具本身失败)才推进。
//
// 本文件直接绑定生产 EnforcerRepair 判定(hasAbortedBlogAttempt /
// hasErroredBlogAttempt),证明「interrupted 标志是推进与否的唯一判据」这一
// 领域边界;完整 owner-cycle 行为由 behavior-diagnosis 的 LOOP_006/ENFORCER_065
// 交叉验证。

import assert from 'node:assert/strict'
import test from 'node:test'

import { toList } from '../../verification-system/tests/support/domain.mjs'

const { hasAbortedBlogAttempt, hasErroredBlogAttempt } = await import('../../../dist/Enforcer/Repair.js')

/** Host-raw assistant step; only Host sets time.completed when the run ends. */
const assistantStep = (id, parts, { completed = true } = {}) =>
  toList([
    {
      info: {
        id,
        role: 'assistant',
        ...(completed ? { time: { completed: Date.now() } } : { time: { created: Date.now() } }),
      },
      parts,
    },
  ])

/** Host abort cleanup: status=error + metadata.interrupted=true (processor.ts:589). */
const interruptedBlog = (id, callId) =>
  assistantStep(
    id,
    [
      {
        type: 'tool',
        tool: 'chronicle',
        callID: callId,
        state: {
          status: 'error',
          error: 'Tool execution aborted',
          input: { text: 'was writing' },
          metadata: { interrupted: true },
          time: { start: 1, end: 2 },
        },
      },
    ],
    { completed: true },
  )

/** Tool execution error: status=error with no interrupted metadata. */
const erroredBlog = (id, callId) =>
  assistantStep(
    id,
    [
      {
        type: 'tool',
        tool: 'chronicle',
        callID: callId,
        state: {
          status: 'error',
          error: 'blog tool crashed',
          input: { text: 'was writing' },
          time: { start: 1, end: 2 },
        },
      },
    ],
    { completed: true },
  )

test('WHAT[PAR-012] PAR_012_an_interrupted_tool_call_is_not_a_confirmed_failure', () => {
  // Host 标记(interrupted=true)是判据:该残留被识别为 abort 清理,不是工具失败。
  assert.equal(hasAbortedBlogAttempt(interruptedBlog('asst-killed', 'blog-hang')), true)

  // 同一条消息绝不双重计数:interrupted 判定优先,不会同时被当成工具错误。
  assert.equal(hasErroredBlogAttempt(interruptedBlog('asst-killed', 'blog-hang')), false)
})

test('WHAT[PAR-012] PAR_012_a_tool_error_without_interrupted_is_the_confirmed_failure', () => {
  // status=error 且无 interrupted → 工具本身失败,才计入已确认失败。
  assert.equal(hasErroredBlogAttempt(erroredBlog('asst-tool-error', 'blog-crash')), true)
  assert.equal(hasAbortedBlogAttempt(erroredBlog('asst-tool-error', 'blog-crash')), false)
})
