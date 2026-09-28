import assert from 'node:assert/strict'
import test from 'node:test'
import * as repair from '../../../dist/Enforcer/RepairSurface.js'

const assistantStep = (id, parts, { completed = true } = {}) => [
  {
    info: {
      id,
      role: 'assistant',
      ...(completed ? { time: { completed: 2 } } : { time: { created: 1 } }),
    },
    parts,
  },
]

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

test('WHAT[provider-attempt-recovery-012] PAR_012_an_interrupted_tool_call_is_not_a_confirmed_failure', () => {
  // Host 标记(interrupted=true)是判据:该残留被识别为 abort 清理,不是工具失败。
  const evidence = repair.classifyBlogAttempt(interruptedBlog('asst-killed', 'blog-hang'))
  assert.equal(evidence.aborted, true)

  // 同一条消息绝不双重计数:interrupted 判定优先,不会同时被当成工具错误。
  assert.equal(evidence.errored, false)
})

test('WHAT[provider-attempt-recovery-012] ordinary tool error remains distinct from interrupted cleanup residue', () => {
  // 工具错误分类本身不授予 provider 重试权限，也不证明预算推进。
  const evidence = repair.classifyBlogAttempt(erroredBlog('asst-tool-error', 'blog-crash'))
  assert.equal(evidence.errored, true)
  assert.equal(evidence.aborted, false)
})

test.todo('WHAT[provider-attempt-recovery-012] actual aborted Host tool residue leaves provider budget unchanged through reconciliation (GAP-139)')
