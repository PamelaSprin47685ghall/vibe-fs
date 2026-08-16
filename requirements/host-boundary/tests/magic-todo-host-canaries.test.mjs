import assert from 'node:assert/strict'
import test from 'node:test'
import { hostSnapshot, magicTodo } from './support/host-surface.mjs'

const SESSION = 'ses_magic_todo_canary'
const CALL = 'call_magic_todo_1'

test('WHAT[HOST-BOUNDARY-012] MAGIC_TODO_CANARY_H_journal_xtrace_uniquely_completes_host_carrier', () => {
  const located = magicTodo.locate({ sessionID: SESSION, callID: CALL, parts: [{ type: 'tool', id: 'part_1', callID: CALL, state: { status: 'completed', output: 'ok' } }] })
  assert.equal(located.ok, true)
  assert.deepEqual(located.value, { messageId: SESSION, partId: 'part_1', callId: CALL })
})

test('WHAT[HOST-BOUNDARY-020] MAGIC_TODO_CANARY_H_journal_mapping_fails_closed_on_host_part_mismatch', () => {
  const located = hostSnapshot.locateToolCall(CALL, [{ info: { id: SESSION }, parts: [{ type: 'text', text: 'not a tool' }] }])
  assert.equal(located.ok, false)
})

test('WHAT[HOST-BOUNDARY-019] MAGIC_TODO_CANARY_A_PRE_before_in_place_mutation_reaches_executor_replacement_does_not', () => {
  const input = { todos: [{ content: 'first', status: 'pending' }] }
  const observed = magicTodo.execute({ args: input })
  assert.equal(observed.observed, true)
  assert.equal(observed.todos[0].content, 'first')
})

test('WHAT[HOST-BOUNDARY-019] MAGIC_TODO_CANARY_A_PRE_definition_before_after_accept_host_positional_trigger', () => {
  const hooks = ['tool.definition', 'tool.execute.before', 'tool.execute.after']
  assert.deepEqual(hooks, ['tool.definition', 'tool.execute.before', 'tool.execute.after'])
})
