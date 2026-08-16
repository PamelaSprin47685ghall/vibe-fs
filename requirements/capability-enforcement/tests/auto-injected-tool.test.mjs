// HOST-013: the hyphen marker is a transcript identity, never a registered
// provider tool. The registry decision and marker constants cross through their
// owner surfaces; transform behavior runs through the real plugin hook.

import assert from 'node:assert/strict'
import test from 'node:test'

import {
  canonicalText,
  markerSource,
  markerToolName,
} from '../../../dist/OpenCode/Host/PairProgrammingThoughtSurface.js'
import { rolePredicate } from '../../../dist/OpenCode/Tools/ToolRegistrySurface.js'
import { withPlugin } from '../../verification-system/tests/support/plugin-fixture.mjs'

const withSession = (messages, sessionID = 'ses-auto-injected') =>
  messages.map((message, index) => ({
    ...message,
    info: {
      ...(message.info ?? {}),
      id: message.info?.id ?? `msg-${index}`,
      role: message.info?.role ?? message.role ?? 'user',
      sessionID,
    },
  }))

test('WHAT[ENF-006] AUTOINJ_tool_definition_is_removed_and_name_is_hyphen', async () => {
  assert.equal(markerToolName, '-')
  assert.equal(rolePredicate('-', 'coder'), false, 'placeholder - is not a registered role tool')
  assert.equal(rolePredicate('-', 'manager'), false)
  assert.equal(rolePredicate('-', 'blogger'), false)

  await withPlugin(async (hooks) => {
    assert.equal(hooks.tool.auto-injected, undefined, 'auto-injected must not be in hooks.tool')
    assert.equal(hooks.tool['-'], undefined, '- must not be in hooks.tool')
  })
})

test('WHAT[ENF-006] AUTOINJ_active_call_is_rewritten_from_failed_to_completed_with_reprimand', async () => {
  await withPlugin(async (hooks) => {
    const transformed = {
      messages: withSession([
        {
          role: 'assistant',
          info: { id: 'asst-active-call' },
          parts: [
            {
              type: 'tool',
              tool: '-',
              callID: 'call-1',
              state: { status: 'error', error: 'Tool - not found' },
            },
          ],
        },
      ]),
    }
    await hooks['experimental.chat.messages.transform']({}, transformed)
    const rewritten = transformed.messages.find((message) => message.info?.id === 'asst-active-call')
    assert.ok(rewritten)
    const part = rewritten.parts[0]
    assert.equal(part.state.status, 'completed', 'failed tool result must be rewritten to completed')
    assert.equal(part.state.error, undefined, 'error field must be cleared')
    assert.match(part.state.output, /DENIED.*not an executable tool/, 'result must contain scolding text')
  })
})

test('WHAT[ENF-006] AUTOINJ_tryInject_rewrites_active_call_while_preserving_synthetic_injection', async () => {
  await withPlugin(async (hooks) => {
    const transformed = {
      messages: withSession([
        {
          role: 'assistant',
          info: { id: 'asst-1' },
          parts: [
            {
              type: 'tool',
              tool: '-',
              callID: 'call-active',
              state: { status: 'error', error: 'Tool - not found' },
            },
          ],
        },
        { role: 'user', info: { id: 'user-1' }, parts: [{ type: 'text', text: 'hello' }] },
      ]),
    }

    await hooks['experimental.chat.messages.transform']({}, transformed)
    const rewrittenActive = transformed.messages.find((message) => message.info?.id === 'asst-1')
    assert.ok(rewrittenActive)
    assert.equal(rewrittenActive.parts[0].state.status, 'completed')
    assert.match(rewrittenActive.parts[0].state.output, /DENIED/)

    const synthetic = transformed.messages.find(
      (message) => message.info?.source === markerSource,
    )
    assert.ok(synthetic)
    assert.equal(synthetic.parts[0].tool, '-')
    assert.equal(synthetic.parts[0].state.status, 'completed')
    assert.equal(synthetic.parts[0].state.output, canonicalText)
  })
})
