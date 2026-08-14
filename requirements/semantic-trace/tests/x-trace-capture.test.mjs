// COMPANION-003 / COMPANION-012 — 唯一 semantic capture mapper。
//
// 所有 prompt/assistant/reasoning/tool part 进入 XTrace 只经 XTraceCapture 的
// 一个 mapper；Activity 是 transport bookkeeping，被丢弃。

import { test } from 'node:test'
import assert from 'node:assert/strict'
import { xTraceCapture } from '../../../tests/unit/support/domain.mjs'

test('COMPANION_012_text_maps_to_semantic_text', () => {
  const mapped = xTraceCapture.map(xTraceCapture.text('hello world'))
  assert.equal(mapped.tag, 'SemanticText')
  assert.equal(mapped.part.fields[0], 'hello world')
})

test('COMPANION_012_reasoning_maps_to_semantic_reasoning', () => {
  const mapped = xTraceCapture.map(xTraceCapture.reasoning('considering'))
  assert.equal(mapped.tag, 'SemanticReasoning')
  assert.equal(mapped.part.fields[0], 'considering')
})

test('COMPANION_012_tool_call_drops_the_call_id', () => {
  const mapped = xTraceCapture.map(xTraceCapture.toolCall('call-1', 'read', '{"path":"a"}'))
  assert.equal(mapped.tag, 'SemanticToolCall')
  assert.deepEqual(mapped.part.fields, ['read', '{"path":"a"}'])
})

test('COMPANION_012_tool_result_drops_the_call_id', () => {
  const mapped = xTraceCapture.map(xTraceCapture.toolResult('call-1', 'output'))
  assert.equal(mapped.tag, 'SemanticToolResult')
  assert.deepEqual(mapped.part.fields, ['output'])
})

test('COMPANION_012_activity_is_dropped_not_mapped', () => {
  assert.equal(xTraceCapture.map(xTraceCapture.activity('step-start')), undefined)
})
