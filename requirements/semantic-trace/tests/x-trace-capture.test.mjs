// COMPANION-003 / COMPANION-012 — 唯一 semantic capture mapper。
//
// 所有 prompt/assistant/reasoning/tool part 进入 XTrace 只经 XTraceSurface 的
// 一个 mapper；Activity 是 transport bookkeeping，被丢弃。

import { test } from 'node:test'
import assert from 'node:assert/strict'
import * as xTrace from '../../../dist/Context/Trace/XTraceSurface.js'

test('WHAT[SEMANTIC-TRACE-002] COMPANION_012_text_maps_to_semantic_text', () => {
  const mapped = xTrace.mapPart(xTrace.textPart('hello world'))
  assert.equal(mapped.kind, 'SemanticText')
  assert.equal(mapped.part.kind, 'text')
  assert.equal(mapped.part.text, 'hello world')
})

test('WHAT[SEMANTIC-TRACE-002] COMPANION_012_reasoning_maps_to_semantic_reasoning', () => {
  const mapped = xTrace.mapPart(xTrace.reasoningPart('considering'))
  assert.equal(mapped.kind, 'SemanticReasoning')
  assert.equal(mapped.part.kind, 'reasoning')
  assert.equal(mapped.part.text, 'considering')
})

test('WHAT[SEMANTIC-TRACE-002] COMPANION_012_tool_call_drops_the_call_id', () => {
  const mapped = xTrace.mapPart(xTrace.toolCallPart('call-1', 'read', '{"path":"a"}'))
  assert.equal(mapped.kind, 'SemanticToolCall')
  assert.deepEqual(mapped.part, { kind: 'tool-call', name: 'read', args: '{"path":"a"}' })
})

test('WHAT[SEMANTIC-TRACE-002] COMPANION_012_tool_result_drops_the_call_id', () => {
  const mapped = xTrace.mapPart(xTrace.toolResultPart('call-1', 'output'))
  assert.equal(mapped.kind, 'SemanticToolResult')
  assert.deepEqual(mapped.part, { kind: 'tool-result', result: 'output' })
})

test('WHAT[SEMANTIC-TRACE-002] COMPANION_012_activity_is_dropped_not_mapped', () => {
  assert.equal(xTrace.mapPart(xTrace.activityPart('step-start')), undefined)
})
