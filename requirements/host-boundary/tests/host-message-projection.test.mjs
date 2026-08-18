import assert from 'node:assert/strict'
import test from 'node:test'
import * as HostMessageProjection from '../../../dist/OpenCode/Host/HostMessageProjection.js'

const sanitizeMessage = HostMessageProjection.sanitizeMessage
const sanitizeMessages = (messages) => messages.map(HostMessageProjection.sanitizeMessage)

test('WHAT[HOST-BOUNDARY-011] HOST_016_assistant_message_with_only_reasoning_gets_semantically_empty_dot_text', () => {
  const raw = { info: { id: 'asst_1', role: 'assistant' }, parts: [{ type: 'reasoning', text: 'Step-by-step thinking content' }] }
  const result = sanitizeMessage(raw)
  assert.equal(result.parts.at(-1).type, 'text')
  assert.equal(result.parts.at(-1).text, '.')
})

test('WHAT[HOST-BOUNDARY-011] HOST_016_assistant_message_with_thinking_type_gets_semantically_empty_dot_text', () => {
  const result = sanitizeMessage({ info: { role: 'assistant' }, parts: [{ type: 'thinking', thinking: 'Deep reasoning text' }] })
  assert.equal(result.parts.at(-1).text, '.')
})

test('WHAT[HOST-BOUNDARY-011] HOST_016_assistant_message_with_empty_parts_gets_ellipsis_fallback', () => {
  const result = sanitizeMessage({ info: { role: 'assistant' }, parts: [] })
  assert.equal(result.parts.at(-1).text, '...')
})

test('WHAT[HOST-BOUNDARY-011] HOST_016_user_message_with_empty_parts_gets_hash_fallback', () => {
  const result = sanitizeMessage({ info: { role: 'user' }, parts: [] })
  assert.equal(result.parts.at(-1).text, '#')
})

test('WHAT[HOST-BOUNDARY-011] HOST_016_message_with_existing_text_is_untouched', () => {
  const raw = { info: { role: 'assistant' }, parts: [{ type: 'text', text: 'Formal answer' }] }
  assert.deepEqual(sanitizeMessage(raw), raw)
})

test('WHAT[HOST-BOUNDARY-011] HOST_016_assistant_message_with_tool_call_is_untouched', () => {
  const raw = { info: { role: 'assistant' }, parts: [{ type: 'tool', tool: 'auto-injected', callID: 'g1' }] }
  assert.deepEqual(sanitizeMessage(raw), raw)
})

test('WHAT[HOST-BOUNDARY-011] HOST_016_sanitizeMessages_processes_whole_array', () => {
  const raw = [
    { info: { role: 'user' }, parts: [{ type: 'text', text: 'Hi' }] },
    { info: { role: 'assistant' }, parts: [{ type: 'reasoning', text: 'Thinking' }] },
  ]
  const result = sanitizeMessages(raw)
  assert.equal(result[0].parts[0].text, 'Hi')
  assert.equal(result[1].parts.at(-1).text, '.')
})
