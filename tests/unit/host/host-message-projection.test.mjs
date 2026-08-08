// HMP: HostMessageProjection — HOST-016 empty content safeguard.

import assert from 'node:assert/strict'
import test from 'node:test'
import { toList, listItems } from '../support/domain.mjs'

const {
  sanitizeMessage,
  sanitizeMessages,
} = await import('../../../dist/Infrastructure/OpenCode/Host/HostMessageProjection.js')

test('HOST_016_assistant_message_with_only_reasoning_gets_text_part_from_reasoning', () => {
  const raw = {
    info: { id: 'asst_1', role: 'assistant' },
    parts: [{ type: 'reasoning', text: 'Step-by-step thinking content' }],
  }
  const sanitized = sanitizeMessage(raw)
  assert.equal(sanitized.parts.length, 2)
  assert.equal(sanitized.parts[0].type, 'reasoning')
  assert.equal(sanitized.parts[1].type, 'text')
  assert.equal(sanitized.parts[1].text, 'Step-by-step thinking content')
})

test('HOST_016_assistant_message_with_thinking_type_gets_text_part', () => {
  const raw = {
    info: { id: 'asst_2', role: 'assistant' },
    parts: [{ type: 'thinking', thinking: 'Deep reasoning text' }],
  }
  const sanitized = sanitizeMessage(raw)
  assert.equal(sanitized.parts.length, 2)
  assert.equal(sanitized.parts[1].type, 'text')
  assert.equal(sanitized.parts[1].text, 'Deep reasoning text')
})

test('HOST_016_assistant_message_with_empty_parts_gets_ellipsis_fallback', () => {
  const raw = {
    info: { id: 'asst_3', role: 'assistant' },
    parts: [],
  }
  const sanitized = sanitizeMessage(raw)
  assert.equal(sanitized.parts.length, 1)
  assert.equal(sanitized.parts[0].type, 'text')
  assert.equal(sanitized.parts[0].text, '...')
})

test('HOST_016_user_message_with_empty_parts_gets_hash_fallback', () => {
  const raw = {
    info: { id: 'user_1', role: 'user' },
    parts: [],
  }
  const sanitized = sanitizeMessage(raw)
  assert.equal(sanitized.parts.length, 1)
  assert.equal(sanitized.parts[0].type, 'text')
  assert.equal(sanitized.parts[0].text, '#')
})

test('HOST_016_message_with_existing_text_is_untouched', () => {
  const raw = {
    info: { id: 'asst_4', role: 'assistant' },
    parts: [{ type: 'text', text: 'Formal answer' }],
  }
  const sanitized = sanitizeMessage(raw)
  assert.equal(sanitized.parts.length, 1)
  assert.equal(sanitized.parts[0].text, 'Formal answer')
})

test('HOST_016_assistant_message_with_tool_call_is_untouched', () => {
  const raw = {
    info: { id: 'asst_5', role: 'assistant' },
    parts: [{ type: 'tool', tool: 'guideline', callID: 'g1' }],
  }
  const sanitized = sanitizeMessage(raw)
  assert.equal(sanitized.parts.length, 1)
  assert.equal(sanitized.parts[0].type, 'tool')
})

test('HOST_016_sanitizeMessages_processes_whole_array', () => {
  const raw = [
    { info: { id: 'u1', role: 'user' }, parts: [{ type: 'text', text: 'Hi' }] },
    { info: { id: 'a1', role: 'assistant' }, parts: [{ type: 'reasoning', text: 'Thinking' }] },
  ]
  const list = sanitizeMessages(toList(raw))
  const out = listItems(list)
  assert.equal(out.length, 2)
  assert.equal(out[0].parts.length, 1)
  assert.equal(out[1].parts.length, 2)
  assert.equal(out[1].parts[1].text, 'Thinking')
})
