// requirements/capability-enforcement/tests/auto-injected-tool.test.mjs — HOST-013: placeholder is not a registered tool.
//
// Placeholder `-` (and legacy `auto-injected`) has no ToolSpec definition and
// is not exposed as a real tool to LLM. Active model calls are rewritten in transform
// to completed tool results with an explicit reprimand.

import assert from 'node:assert/strict'
import test from 'node:test'
import { toList, listItems } from '../../verification-system/tests/support/domain.mjs'

const { ToolRegistry_rolePredicate: rolePredicate, ToolRegistry_all: allTools } =
  await import('../../../dist/Infrastructure/OpenCode/Tools/ToolRegistry.js')
const {
  toolName,
  reprimandText,
  sanitizeActiveToolCalls,
  tryInject,
} = await import('../../../dist/Infrastructure/OpenCode/Host/PairProgrammingThoughtTransform.js')
const { Role } = await import('../../../dist/Kernel/Roles.js')

test('AUTOINJ_tool_definition_is_removed_and_name_is_hyphen', () => {
  assert.equal(toolName, '-')
  const pred = rolePredicate('-', undefined, 'ses-auto')
  assert.equal(pred(Role.Coder), false, 'placeholder - is not a registered role tool')
  assert.equal(pred(Role.Manager), false)
  assert.equal(pred(Role.Blogger), false)
})

test('AUTOINJ_active_call_is_rewritten_from_failed_to_completed_with_reprimand', () => {
  const activeCallMsg = {
    role: 'assistant',
    info: { id: 'asst-active-call' },
    parts: [
      {
        type: 'tool',
        tool: '-',
        callID: 'call-1',
        state: {
          status: 'error',
          error: 'Tool - not found',
        },
      },
    ],
  }

  const sanitized = listItems(sanitizeActiveToolCalls(undefined, toList([activeCallMsg])))
  assert.equal(sanitized.length, 1)
  const part = sanitized[0].parts[0]
  assert.equal(part.state.status, 'completed', 'failed tool result must be rewritten to completed')
  assert.equal(part.state.error, undefined, 'error field must be cleared')
  assert.match(part.state.output, /DENIED.*not an executable tool/, 'result must contain scolding text')
})

test('AUTOINJ_tryInject_rewrites_active_call_while_preserving_synthetic_injection', async () => {
  const activeCallMsg = {
    role: 'assistant',
    info: { id: 'asst-1' },
    parts: [
      {
        type: 'tool',
        tool: '-',
        callID: 'call-active',
        state: {
          status: 'error',
          error: 'Tool - not found',
        },
      },
    ],
  }
  const userMsg = {
    role: 'user',
    info: { id: 'user-1' },
    parts: [{ type: 'text', text: 'hello' }],
  }

  const result = await tryInject(undefined, 'ses-test', 'guideline-text', toList([activeCallMsg, userMsg]))
  assert.equal(result.tag, 0, 'tryInject must succeed')
  const messages = listItems(result.fields[0])

  // Active call is rewritten to completed
  const rewrittenActive = messages.find((m) => m.info?.id === 'asst-1')
  assert.ok(rewrittenActive)
  assert.equal(rewrittenActive.parts[0].state.status, 'completed')
  assert.match(rewrittenActive.parts[0].state.output, /DENIED/)

  // Injected synthetic marker is still present with its own text
  const synthetic = messages.find((m) => m.info?.source === 'pair-programming-auto-injected')
  assert.ok(synthetic)
  assert.equal(synthetic.parts[0].tool, '-')
  assert.equal(synthetic.parts[0].state.status, 'completed')
  assert.equal(synthetic.parts[0].state.output, 'guideline-text')
})
