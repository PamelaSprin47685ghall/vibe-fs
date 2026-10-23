import test from 'node:test'
import assert from 'node:assert/strict'
import {
  filterScopeMessages,
  appendBudgetReminder,
  appendUserReminder,
  budgetSuffix,
  turnBudget,
  isScopeInitialized,
  onTurnStart,
  onNewPrompt,
  resetScopeState,
  clearSessionState,
} from '../../../dist/Context/Scope/Surface.js'

test('WHAT[context-compression-033] barrier purge with realistic OpenCode message shapes', () => {
  // OpenCode: messages have `role: "user" | "assistant" | "tool"` and `parts` array
  const messages = [
    // 0: First operator prompt
    {
      role: 'user',
      content: 'first prompt',
      parts: [{ type: 'text', text: 'first prompt' }],
    },
    // 1: Assistant work before reset 1 (should be purged)
    {
      role: 'assistant',
      parts: [
        { type: 'text', text: 'thinking about files' },
        { type: 'tool-call', id: 'call_glob_1', tool: 'glob', args: '{}' },
      ],
    },
    // 2: Tool result for call_glob_1 (should be purged)
    {
      role: 'tool',
      toolCallId: 'call_glob_1',
      parts: [{ type: 'tool-result', id: 'call_glob_1', result: 'file.txt' }],
    },
    // 3: Assistant calls resetScope #1 (thinking should be stripped, reset call kept)
    {
      role: 'assistant',
      parts: [
        { type: 'reasoning', text: 'analyzed earlier work' },
        { type: 'tool-call', id: 'call_reset_1', tool: 'resetScope', args: '{"newScopeObjective":"phase 2","newScopeTurnBudget":10}' },
      ],
    },
    // 4: Tool result for resetScope #1 (should be kept)
    {
      role: 'tool',
      toolCallId: 'call_reset_1',
      parts: [{ type: 'tool-result', id: 'call_reset_1', result: 'Scope reset. Objective: phase 2' }],
    },
    // 5: Mid-run operator steer (must be kept)
    {
      role: 'user',
      content: 'operator steer: change direction',
      parts: [{ type: 'text', text: 'operator steer: change direction' }],
    },
    // 6: Assistant work between reset 1 and reset 2 (should be purged)
    {
      role: 'assistant',
      parts: [
        { type: 'tool-call', id: 'call_read_1', tool: 'read', args: '{"path":"file.txt"}' },
      ],
    },
    // 7: Tool result for call_read_1 (should be purged)
    {
      role: 'tool',
      toolCallId: 'call_read_1',
      parts: [{ type: 'tool-result', id: 'call_read_1', result: 'content' }],
    },
    // 8: Barrier assistant message: tool before barrier (call_before), resetScope #2 (barrier), tool after barrier (call_after)
    {
      role: 'assistant',
      parts: [
        { type: 'tool-call', id: 'call_before', tool: 'edit', args: '{"path":"file.txt"}' },
        { type: 'tool-call', id: 'call_reset_2', tool: 'resetScope', args: '{"newScopeObjective":"phase 3","newScopeTurnBudget":5}' },
        { type: 'tool-call', id: 'call_after', tool: 'write', args: '{"path":"out.txt"}' },
      ],
    },
    // 9: Tool result for call_before (must be purged)
    {
      role: 'tool',
      toolCallId: 'call_before',
      parts: [{ type: 'tool-result', id: 'call_before', result: 'edit done' }],
    },
    // 10: Tool result for call_reset_2 (must be kept)
    {
      role: 'tool',
      toolCallId: 'call_reset_2',
      parts: [{ type: 'tool-result', id: 'call_reset_2', result: 'Scope reset. Objective: phase 3' }],
    },
    // 11: Tool result for call_after (must be kept)
    {
      role: 'tool',
      toolCallId: 'call_after',
      parts: [{ type: 'tool-result', id: 'call_after', result: 'write done' }],
    },
  ]

  const filtered = filterScopeMessages(messages)

  // Expected surviving messages:
  // 0: First operator prompt (kept)
  // 1: Reset call 1 (thinking stripped)
  // 2: Reset result 1
  // 3: Operator steer (kept)
  // 4: Barrier assistant message (call_before dropped; call_reset_2 and call_after kept)
  // 5: Reset result 2
  // 6: Result for call_after
  assert.equal(filtered.length, 7)

  // Message 0: first operator prompt
  assert.equal(filtered[0].role, 'user')
  assert.equal(filtered[0].content, 'first prompt')

  // Message 1: reset 1 assistant message with thinking stripped
  assert.equal(filtered[1].role, 'assistant')
  assert.equal(filtered[1].parts.length, 1)
  assert.equal(filtered[1].parts[0].id, 'call_reset_1')

  // Message 2: reset 1 result
  assert.equal(filtered[2].role, 'tool')
  assert.equal(filtered[2].toolCallId, 'call_reset_1')

  // Message 3: operator steer preserved
  assert.equal(filtered[3].role, 'user')
  assert.equal(filtered[3].content, 'operator steer: change direction')

  // Message 4: barrier assistant message — call_before dropped, call_reset_2 and call_after kept
  assert.equal(filtered[4].role, 'assistant')
  assert.equal(filtered[4].parts.length, 2)
  assert.equal(filtered[4].parts[0].id, 'call_reset_2')
  assert.equal(filtered[4].parts[1].id, 'call_after')

  // Message 5: reset 2 result
  assert.equal(filtered[5].role, 'tool')
  assert.equal(filtered[5].toolCallId, 'call_reset_2')

  // Message 6: call_after result
  assert.equal(filtered[6].role, 'tool')
  assert.equal(filtered[6].toolCallId, 'call_after')
})

test('WHAT[context-compression-033] barrier purge with realistic OMP message shapes', () => {
  // OMP: messages have `role: "user" | "assistant" | "toolResult"` and `content` block array
  const messages = [
    { role: 'user', content: [{ type: 'text', text: 'prompt 1' }] },
    {
      role: 'assistant',
      content: [
        { type: 'toolCall', id: 'omp_1', name: 'read', args: {} },
      ],
    },
    { role: 'toolResult', toolCallId: 'omp_1', content: 'read output' },
    {
      role: 'assistant',
      content: [
        { type: 'toolCall', id: 'omp_reset', name: 'resetScope', args: { newScopeObjective: 'omp phase', newScopeTurnBudget: 5 } },
      ],
    },
    { role: 'toolResult', toolCallId: 'omp_reset', content: 'Scope reset. Objective: omp phase' },
  ]

  const filtered = filterScopeMessages(messages)
  assert.equal(filtered.length, 3)
  assert.equal(filtered[0].role, 'user')
  assert.equal(filtered[1].role, 'assistant')
  assert.equal(filtered[1].content[0].id, 'omp_reset')
  assert.equal(filtered[2].role, 'toolResult')
  assert.equal(filtered[2].toolCallId, 'omp_reset')
})

test('WHAT[context-compression-034] regression: resetScope preserves prompt identity; budget decrements across turns; new prompt zeroes budget', () => {
  const sessionId = 'ses_regression_prompt_id'
  clearSessionState(sessionId)

  const prompt1 = 'implement feature X'
  const prompt1Key = `1000:${prompt1}`
  const prompt1Ts = 1000

  // Turn 1: user prompt arrives before reset
  onNewPrompt(sessionId, prompt1Key, prompt1Ts, false)
  onTurnStart(sessionId)
  assert.equal(isScopeInitialized(sessionId), false)
  assert.equal(turnBudget(sessionId), 0)

  // Turn 1 completes with resetScope(budget=5)
  resetScopeState(sessionId, 5)
  assert.equal(isScopeInitialized(sessionId), true)
  assert.equal(turnBudget(sessionId), 5)

  // Turn 2: next request arrives with the SAME operator prompt
  // onNewPrompt must preserve prompt identity and NOT zero the budget
  onNewPrompt(sessionId, prompt1Key, prompt1Ts, true)
  assert.equal(turnBudget(sessionId), 5, 'budget must be preserved across same prompt')
  onTurnStart(sessionId)
  assert.equal(turnBudget(sessionId), 4, 'budget decrements on turn_start')

  // Turn 3: another turn under the same prompt
  onNewPrompt(sessionId, prompt1Key, prompt1Ts, true)
  onTurnStart(sessionId)
  assert.equal(turnBudget(sessionId), 3)

  // Turn 4: a genuinely NEW operator prompt arrives (e.g. steer or next task)
  const prompt2 = 'now do task Y'
  const prompt2Key = `2000:${prompt2}`
  const prompt2Ts = 2000
  onNewPrompt(sessionId, prompt2Key, prompt2Ts, false)
  assert.equal(turnBudget(sessionId), 0, 'genuinely new prompt zeroes budget until next reset')
  assert.equal(isScopeInitialized(sessionId), false, 'scopeInitialized resets until reset lands after new prompt')

  // Reset lands for prompt 2 with budget=8
  resetScopeState(sessionId, 8)
  assert.equal(isScopeInitialized(sessionId), true)
  assert.equal(turnBudget(sessionId), 8)

  clearSessionState(sessionId)
})

test('WHAT[context-compression-034] result suffix does not double-append when already suffixed', () => {
  const baseText = 'Scope reset. Objective: optimize query'
  const budget = 5

  // First append adds the suffix
  const once = appendBudgetReminder(baseText, budget)
  assert.equal(once, `${baseText}\0\uFEFF[Remaining turnBudget: 5]`)

  // Second append on already-suffixed text MUST NOT duplicate suffix
  const twice = appendBudgetReminder(once, budget)
  assert.equal(twice, once, 'appendBudgetReminder must not double-append to string')

  // User reminder suffix does not duplicate
  const reminderOnce = appendUserReminder('error output')
  assert.ok(reminderOnce.includes('[System: Estimate the turnBudget'))
  const reminderTwice = appendUserReminder(reminderOnce)
  assert.equal(reminderTwice, reminderOnce, 'appendUserReminder must not double-append')

  // Block content does not duplicate
  const blocks = [{ type: 'text', text: baseText }]
  const blockOnce = appendBudgetReminder(blocks, budget)
  const blockTwice = appendBudgetReminder(blockOnce, budget)
  assert.deepEqual(blockTwice, blockOnce, 'appendBudgetReminder must not double-append to block array')
})
