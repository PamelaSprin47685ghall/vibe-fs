// Split from tests/unit/host/pair-thought-transform.test.mjs (cutover Wave 2a);
// owner: provider-projection. Cursor wire 渲染半边（PROVIDER-PROJECTION-010）：
// representation 不反向创造 authority/state —— Cursor provider 的 wire 不带
// synthetic 冒充消息（deepEqual raw），guidance 以 NUL+BOM 字节骑在真实
// completed/error tool result 内，且不修改 Host transcript 输入。
// anchor/replay 机制断言（PPT_* 与 restore）归 prefix-stability；
// PAIR_HINT marker 正文 craft 归 cognitive-environment。

import assert from 'node:assert/strict'
import test from 'node:test'

import { toList, listItems, resultOf } from '../../verification-system/tests/support/domain.mjs'

const {
  tryInject,
  isPairProgrammingThought,
  skipAutoInjectedRequested,
  text,
  stableCallId,
} = await import('../../../dist/Infrastructure/OpenCode/Host/PairProgrammingThoughtTransform.js')

const inject = async (session, raw, markerText = text) => {
  const result = resultOf(await tryInject(undefined, session, markerText, toList(raw)))
  assert.equal(result.ok, true, `HOST-013 transform must commit the pair: ${result.error ?? ''}`)
  return listItems(result.value)
}

const userMsg = (id, body = 'hello') => ({
  info: { id, role: 'user' },
  parts: [{ type: 'text', text: body }],
})

const pairMessages = (messages) => messages.filter((m) => isPairProgrammingThought(m))

test('C_PH_cursor_keeps_durable_occurrence_without_synthetic_message', async () => {
  const previous = process.env.WANXIANGSHU_SKIP_AUTO_INJECTED
  try {
    delete process.env.WANXIANGSHU_SKIP_AUTO_INJECTED
    assert.equal(skipAutoInjectedRequested(undefined), false)
    assert.equal(skipAutoInjectedRequested('cursor'), false)
    assert.equal(skipAutoInjectedRequested('anthropic'), false)

    const session = 'ses_cursor_no_synthetic'
    const raw = [{
      info: { id: 'u1', role: 'user', model: { providerID: 'cursor', modelID: 'composer' } },
      parts: [{ type: 'text', text: 'steer' }],
    }]
    const cursor = await inject(session, raw)
    assert.equal(pairMessages(cursor).length, 0)
    assert.deepEqual(cursor, raw)

    const ordinary = await inject(session, [userMsg('u1', 'steer')])
    assert.equal(pairMessages(ordinary).length, 1, 'Cursor still durably records the provider-independent occurrence')
  } finally {
    if (previous === undefined) delete process.env.WANXIANGSHU_SKIP_AUTO_INJECTED
    else process.env.WANXIANGSHU_SKIP_AUTO_INJECTED = previous
  }
})

test('C_PH_cursor_appends_NUL_BOM_guidance_inside_real_completed_tool_result', async () => {
  const raw = [
    {
      info: { id: 'u1', role: 'user', model: { providerID: 'cursor', modelID: 'default' } },
      parts: [{ type: 'text', text: 'read it' }],
    },
    {
      info: { id: 'c1', role: 'assistant' },
      parts: [{
        type: 'tool',
        tool: 'read',
        callID: 'call_read',
        state: { status: 'pending', input: {}, time: { start: 0 } },
      }],
    },
    {
      info: { id: 'r1', role: 'assistant', providerID: 'cursor', modelID: 'default' },
      parts: [{
        type: 'tool',
        tool: 'read',
        callID: 'call_read',
        state: { status: 'completed', input: { filePath: 'AGENTS.md' }, output: 'success' },
      }],
    },
  ]
  const out = await inject('ses_cursor_completed_tool', raw)
  assert.equal(pairMessages(out).length, 0)
  assert.equal(out.length, raw.length)
  assert.equal(out.at(-1).info.id, 'r1')
  assert.equal(out.at(-1).parts[0].state.status, 'completed')
  assert.equal(out.at(-1).parts[0].state.output, `success\0\uFEFF${text}`)
  assert.equal(raw.at(-1).parts[0].state.output, 'success', 'provider projection must not mutate Host transcript input')

  const replayed = await inject('ses_cursor_completed_tool', raw)
  assert.deepEqual(replayed, out, 'Cursor replay must reproduce identical NUL+BOM guidance bytes')
})

test('C_PH_cursor_appends_NUL_BOM_guidance_inside_real_error_tool_result', async () => {
  const raw = [
    {
      info: { id: 'u1', role: 'user', model: { providerID: 'cursor', modelID: 'default' } },
      parts: [{ type: 'text', text: 'read it' }],
    },
    {
      info: { id: 'c1', role: 'assistant' },
      parts: [{
        type: 'tool',
        tool: 'read',
        callID: 'call_read',
        state: { status: 'pending', input: {}, time: { start: 0 } },
      }],
    },
    {
      info: { id: 'r1', role: 'assistant', providerID: 'cursor', modelID: 'default' },
      parts: [{
        type: 'tool',
        tool: 'read',
        callID: 'call_read',
        state: { status: 'error', input: { filePath: 'missing' }, error: 'not found' },
      }],
    },
  ]
  const out = await inject('ses_cursor_error_tool', raw)
  assert.equal(pairMessages(out).length, 0)
  assert.equal(out.at(-1).parts[0].state.status, 'error')
  assert.equal(out.at(-1).parts[0].state.error, `not found\0\uFEFF${text}`)
  assert.equal(raw.at(-1).parts[0].state.error, 'not found')
})
